using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.DTOs.Appointments;
using MyAssistant.Application.DTOs.Assistant;
using MyAssistant.Application.DTOs.Notes;
using MyAssistant.Application.DTOs.Reminders;
using MyAssistant.Application.DTOs.Search;
using MyAssistant.Application.DTOs.Settings;
using MyAssistant.Application.DTOs.Tasks;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.Services;

/// <summary>
/// Orchestrates the full voice/chat assistant flow:
/// language detection -> AI intent extraction -> action -> database -> response.
/// Handles multi-turn confirmation context for appointments, task completion and
/// destructive operations.
/// </summary>
public class AssistantService : IAssistantIntentService
{
    private readonly IAssistantAIService _ai;
    private readonly IAssistantSessionStore _sessions;
    private readonly ITimeZoneService _time;
    private readonly IDateTimeParser _dateTimeParser;
    private readonly ISettingsService _settings;
    private readonly IConversationRepository _conversations;
    private readonly ISubscriptionService _subscription;
    private readonly ILogger<AssistantService> _logger;

    private readonly INoteService _notes;
    private readonly ITaskService _tasks;
    private readonly IReminderService _reminders;
    private readonly IAppointmentService _appointments;
    private readonly ISearchService _search;
    private readonly IExternalKnowledgeService _external;

    public AssistantService(
        IAssistantAIService ai,
        IAssistantSessionStore sessions,
        ITimeZoneService time,
        IDateTimeParser dateTimeParser,
        ISettingsService settings,
        IConversationRepository conversations,
        ISubscriptionService subscription,
        ILogger<AssistantService> logger,
        INoteService notes,
        ITaskService tasks,
        IReminderService reminders,
        IAppointmentService appointments,
        ISearchService search,
        IExternalKnowledgeService? external = null)
    {
        _ai = ai;
        _sessions = sessions;
        _time = time;
        _dateTimeParser = dateTimeParser;
        _settings = settings;
        _conversations = conversations;
        _subscription = subscription;
        _logger = logger;
        _notes = notes;
        _tasks = tasks;
        _reminders = reminders;
        _appointments = appointments;
        _search = search;
        _external = external ?? new ExternalKnowledgeService(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ExternalKnowledgeService>.Instance);
    }

    public async Task<AssistantResponse> ProcessAsync(AssistantRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? "default" : request.SessionId!;
        var settings = await _settings.GetAsync(userId, cancellationToken);
        var tz = settings.TimeZone;

        var language = request.Language;
        if (string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            language = await _ai.DetectLanguageAsync(request.Text, cancellationToken);
        }
        language = AssistantReplies.NormalizeLanguage(language);

        ParsedCommand command;
        try
        {
            command = await _ai.ParseCommandAsync(request.Text, language, tz, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider failed for: {Text}", request.Text);
            command = new ParsedCommand { Intent = AssistantIntent.Unknown, Language = language };
        }

        if (command.Intent != AssistantIntent.ChangeLanguage)
        {
            command.Language = language;
        }
        await _subscription.RecordUsageAsync(userId, UsageType.AiCommand, request.Text, cancellationToken);

        var pending = _sessions.Get(userId, sessionId);
        if (pending is not null && pending.Stage is >= 2 and <= 7)
        {
            // Multi-turn guided capture: section (6), date (4), time (7), category (5),
            // note content (2), task/appointment title (3).
            var text = request.Text?.Trim() ?? string.Empty;

            // Cancellation always wins.
            if (command.Intent == AssistantIntent.Denial || IsNegative(text))
            {
                _sessions.Clear(userId, sessionId);
                var cancelled = AssistantReplies.Cancelled(language);
                await RecordConversationAsync(userId, request, cancelled, AssistantIntent.CancelAction.ToString(), language, cancellationToken);
                return BuildResponse(cancelled, AssistantIntent.CancelAction, language);
            }

            // "skip" / "none" keeps an optional field unset and advances to the next capture.
            if (pending.Stage is 4 or 5 or 6 or 7 && IsSkip(text))
            {
                if (pending.Stage == 4) pending.Command.DateSkipped = true;
                else if (pending.Stage == 5) pending.Command.CategorySkipped = true;
                else if (pending.Stage == 6) pending.Command.SectionSkipped = true;
                else pending.Command.TimeSkipped = true;
                var nextSkip = NextCaptureStage(pending.Command);
                if (nextSkip is null)
                {
                    _sessions.Clear(userId, sessionId);
                    var doneSkip = await ExecuteCapturedAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken);
                    await RecordConversationAsync(userId, request, doneSkip.Reply ?? string.Empty, pending.Command.Intent.ToString(), language, cancellationToken);
                    return doneSkip;
                }
                pending.Stage = nextSkip.Value;
                _sessions.Set(userId, sessionId, pending);
                var askSkip = CapturePrompt(nextSkip.Value, pending.Command.Intent, language);
                return BuildResponse(askSkip, pending.Command.Intent, language, captureType: CaptureTypeForStage(nextSkip.Value));
            }

            // Section capture: choose which tab (Notes/Tasks/Appointments) to save under and echo it back.
            if (pending.Stage == 6)
            {
                var sectionIntent = ResolveSectionWord(text);
                if (sectionIntent is not null)
                {
                    var echo = AssistantReplies.SectionEcho(SectionLabel(sectionIntent.Value), language);
                    pending.Command.Intent = sectionIntent.Value;
                    pending.Command.Section = SectionLabel(sectionIntent.Value);
                    var nextSection = NextCaptureStage(pending.Command);
                    if (nextSection is null)
                    {
                        _sessions.Clear(userId, sessionId);
                        var doneSection = await ExecuteCapturedAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken);
                        await RecordConversationAsync(userId, request, doneSection.Reply ?? string.Empty, pending.Command.Intent.ToString(), language, cancellationToken);
                        return doneSection;
                    }
                    pending.Stage = nextSection.Value;
                    _sessions.Set(userId, sessionId, pending);
                    var askSection = CapturePrompt(nextSection.Value, pending.Command.Intent, language);
                    return BuildResponse($"{echo} {askSection}", pending.Command.Intent, language, captureType: CaptureTypeForStage(nextSection.Value));
                }
            }

            // Time capture: accept any time phrase even though the AI might label it a schedule intent.
            if (pending.Stage == 7 &&
                command.Intent is AssistantIntent.Unknown or AssistantIntent.TodaySchedule or AssistantIntent.TomorrowSchedule)
            {
                var parsedTime = await _dateTimeParser.ParseAsync(text, language, tz, cancellationToken);
                if (parsedTime.HasTime && parsedTime.DateTime.HasValue)
                {
                    pending.Command.Time = TimeOnly.FromDateTime(parsedTime.DateTime.Value);
                    var nextTime = NextCaptureStage(pending.Command);
                    if (nextTime is null)
                    {
                        _sessions.Clear(userId, sessionId);
                        var resultTime = await ExecuteCapturedAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken);
                        await RecordConversationAsync(userId, request, resultTime.Reply ?? string.Empty, pending.Command.Intent.ToString(), language, cancellationToken);
                        return resultTime;
                    }
                    pending.Stage = nextTime.Value;
                    _sessions.Set(userId, sessionId, pending);
                    var askTime = CapturePrompt(nextTime.Value, pending.Command.Intent, language);
                    return BuildResponse(askTime, pending.Command.Intent, language, captureType: CaptureTypeForStage(nextTime.Value));
                }
            }

            // Date capture: accept any phrase the date parser understands (e.g. "tomorrow")
            // even though the AI might label it as a schedule intent — but NOT when the phrase
            // carries a real command ("Today Tasks Reminders" lists reminders, it isn't a date).
            if (pending.Stage == 4 &&
                command.Intent is AssistantIntent.Unknown or AssistantIntent.TodaySchedule or AssistantIntent.TomorrowSchedule)
            {
                var parsedDate = await _dateTimeParser.ParseAsync(text, language, tz, cancellationToken);
                if (parsedDate.HasDate || parsedDate.HasTime)
                {
                    pending.Command.Date = parsedDate.DateTime ?? pending.Command.Date;
                    if (parsedDate.HasTime && parsedDate.DateTime.HasValue)
                    {
                        pending.Command.Time = TimeOnly.FromDateTime(parsedDate.DateTime.Value);
                    }
                    var nextDate = NextCaptureStage(pending.Command);
                    if (nextDate is null)
                    {
                        _sessions.Clear(userId, sessionId);
                        var resultDate = await ExecuteCapturedAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken);
                        await RecordConversationAsync(userId, request, resultDate.Reply ?? string.Empty, pending.Command.Intent.ToString(), language, cancellationToken);
                        return resultDate;
                    }
                    pending.Stage = nextDate.Value;
                    _sessions.Set(userId, sessionId, pending);
                    var askDate = CapturePrompt(nextDate.Value, pending.Command.Intent, language);
                    return BuildResponse(askDate, pending.Command.Intent, language, captureType: CaptureTypeForStage(nextDate.Value));
                }
            }

            // Category capture: accept known category words.
            if (pending.Stage == 5 && IsCategoryWord(text))
            {
                pending.Command.Category = NormalizeCategory(text);
                var nextCat = NextCaptureStage(pending.Command);
                if (nextCat is null)
                {
                    _sessions.Clear(userId, sessionId);
                    var resultCat = await ExecuteCapturedAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken);
                    await RecordConversationAsync(userId, request, resultCat.Reply ?? string.Empty, pending.Command.Intent.ToString(), language, cancellationToken);
                    return resultCat;
                }
                pending.Stage = nextCat.Value;
                _sessions.Set(userId, sessionId, pending);
                var askCat = CapturePrompt(nextCat.Value, pending.Command.Intent, language);
                return BuildResponse(askCat, pending.Command.Intent, language, captureType: CaptureTypeForStage(nextCat.Value));
            }

            // A real command while we're mid-capture supersedes the pending capture.
            if (command.Intent != AssistantIntent.Unknown)
            {
                _sessions.Clear(userId, sessionId);
                return await DispatchAsync(userId, command, language, tz, sessionId, request, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                var stillAsk = CapturePrompt(pending.Stage, pending.Command.Intent, language);
                return BuildResponse(stillAsk, pending.Command.Intent, language, captureType: CaptureTypeForStage(pending.Stage));
            }

            switch (pending.Stage)
            {
                case 4: // capture date (fallback — plain text that wasn't recognized above)
                    {
                        var parsed = await _dateTimeParser.ParseAsync(text, language, tz, cancellationToken);
                        pending.Command.Date = parsed.DateTime ?? pending.Command.Date;
                        if (parsed.HasTime && parsed.DateTime.HasValue)
                        {
                            pending.Command.Time = TimeOnly.FromDateTime(parsed.DateTime.Value);
                        }
                        var nextDate = NextCaptureStage(pending.Command);
                        if (nextDate is null)
                        {
                            _sessions.Clear(userId, sessionId);
                            var resultDate = await ExecuteCapturedAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken);
                            await RecordConversationAsync(userId, request, resultDate.Reply ?? string.Empty, pending.Command.Intent.ToString(), language, cancellationToken);
                            return resultDate;
                        }
                        pending.Stage = nextDate.Value;
                        _sessions.Set(userId, sessionId, pending);
                        var askDate = CapturePrompt(nextDate.Value, pending.Command.Intent, language);
                        return BuildResponse(askDate, pending.Command.Intent, language, captureType: CaptureTypeForStage(nextDate.Value));
                    }
                case 5: // capture category (fallback — a free-form answer is the actual content/title)
                    {
                        _sessions.Clear(userId, sessionId);
                        if (pending.Command.Intent == AssistantIntent.CreateNote)
                        {
                            pending.Command.Content = text;
                            pending.Command.Title = string.IsNullOrWhiteSpace(pending.Command.Title)
                                ? (text.Length > 120 ? text[..120] : text)
                                : pending.Command.Title;
                            var resultNote = await CreateNoteCoreAsync(userId, pending.Command, language, request, cancellationToken, sessionId, confirmFirst: true);
                            await RecordConversationAsync(userId, request, resultNote.Reply ?? string.Empty, AssistantIntent.CreateNote.ToString(), language, cancellationToken);
                            return resultNote;
                        }
                        pending.Command.Title = text.Length > 200 ? text[..200] : text;
                        if (pending.Command.Intent == AssistantIntent.CreateAppointment)
                        {
                            var resultAppt = await CreateAppointmentCoreAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken, confirmFirst: true);
                            await RecordConversationAsync(userId, request, resultAppt.Reply ?? string.Empty, AssistantIntent.CreateAppointment.ToString(), language, cancellationToken);
                            return resultAppt;
                        }
                        if (pending.Command.Intent == AssistantIntent.CreateReminder)
                        {
                            var resultRem = await CreateReminderCoreAsync(userId, pending.Command, language, tz, request, cancellationToken);
                            await RecordConversationAsync(userId, request, resultRem.Reply ?? string.Empty, AssistantIntent.CreateReminder.ToString(), language, cancellationToken);
                            return resultRem;
                        }
                        var resultTask = await CreateTaskCoreAsync(userId, pending.Command, language, request, cancellationToken, sessionId, confirmFirst: true);
                        await RecordConversationAsync(userId, request, resultTask.Reply ?? string.Empty, AssistantIntent.CreateTask.ToString(), language, cancellationToken);
                        return resultTask;
                    }
                case 6: // capture section (fallback — re-ask)
                    {
                        var askSectionAgain = AssistantReplies.AskSection(language);
                        return BuildResponse(askSectionAgain, pending.Command.Intent, language, captureType: CaptureTypeForStage(6));
                    }
                case 7: // capture time (fallback)
                    {
                        var parsedT = await _dateTimeParser.ParseAsync(text, language, tz, cancellationToken);
                        if (parsedT.HasTime && parsedT.DateTime.HasValue)
                        {
                            pending.Command.Time = TimeOnly.FromDateTime(parsedT.DateTime.Value);
                            var nextT = NextCaptureStage(pending.Command);
                            if (nextT is null)
                            {
                                _sessions.Clear(userId, sessionId);
                                var resultT = await ExecuteCapturedAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken);
                                await RecordConversationAsync(userId, request, resultT.Reply ?? string.Empty, pending.Command.Intent.ToString(), language, cancellationToken);
                                return resultT;
                            }
                            pending.Stage = nextT.Value;
                            _sessions.Set(userId, sessionId, pending);
                            var askT = CapturePrompt(nextT.Value, pending.Command.Intent, language);
                            return BuildResponse(askT, pending.Command.Intent, language, captureType: CaptureTypeForStage(nextT.Value));
                        }
                        var askTimeAgain = AssistantReplies.AskTime(language);
                        return BuildResponse(askTimeAgain, pending.Command.Intent, language, captureType: CaptureTypeForStage(7));
                    }
                case 2: // capture note content
                    {
                        _sessions.Clear(userId, sessionId);
                        pending.Command.Content = text;
                        pending.Command.Title = string.IsNullOrWhiteSpace(pending.Command.Title)
                            ? (text.Length > 120 ? text[..120] : text)
                            : pending.Command.Title;
                        var resultNote = await CreateNoteCoreAsync(userId, pending.Command, language, request, cancellationToken, sessionId, confirmFirst: true);
                        await RecordConversationAsync(userId, request, resultNote.Reply ?? string.Empty, AssistantIntent.CreateNote.ToString(), language, cancellationToken);
                        return resultNote;
                    }
                default: // capture task/appointment title
                    {
                        _sessions.Clear(userId, sessionId);
                        pending.Command.Title = text.Length > 200 ? text[..200] : text;
                        if (pending.Command.Intent == AssistantIntent.CreateAppointment)
                        {
                            var resultAppt = await CreateAppointmentCoreAsync(userId, pending.Command, language, tz, request, sessionId, cancellationToken, confirmFirst: true);
                            await RecordConversationAsync(userId, request, resultAppt.Reply ?? string.Empty, AssistantIntent.CreateAppointment.ToString(), language, cancellationToken);
                            return resultAppt;
                        }
                        var resultTask = await CreateTaskCoreAsync(userId, pending.Command, language, request, cancellationToken, sessionId, confirmFirst: true);
                        await RecordConversationAsync(userId, request, resultTask.Reply ?? string.Empty, AssistantIntent.CreateTask.ToString(), language, cancellationToken);
                        return resultTask;
                    }
            }
        }

        if (pending is not null && (command.Intent == AssistantIntent.Confirmation || command.Intent == AssistantIntent.Denial || IsAffirmative(request.Text) || IsNegative(request.Text)))
        {
            if (command.Intent == AssistantIntent.Denial || IsNegative(request.Text))
            {
                _sessions.Clear(userId, sessionId);
                var cancelled = AssistantReplies.Cancelled(language);
                await RecordConversationAsync(userId, request, cancelled, AssistantIntent.CancelAction.ToString(), language, cancellationToken);
                return BuildResponse(cancelled, AssistantIntent.CancelAction, language);
            }

            var executed = await ExecuteConfirmedAsync(userId, pending.Command, language, tz, cancellationToken);
            _sessions.Clear(userId, sessionId);
            await RecordConversationAsync(userId, request, executed.Reply ?? string.Empty, executed.Intent ?? pending.Command.Intent.ToString(), language, cancellationToken);
            return executed;
        }

        return await DispatchAsync(userId, command, language, tz, sessionId, request, cancellationToken);
    }

    // ---------------------------------------------------------------
    // Dispatch
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> DispatchAsync(
        Guid userId, ParsedCommand cmd, string language, string tz, string sessionId, AssistantRequest request, CancellationToken ct)
    {
        switch (cmd.Intent)
        {
            case AssistantIntent.ChangeLanguage:
                var newLang = AssistantReplies.NormalizeLanguage(cmd.Language);
                await _settings.UpdateAsync(userId, new UpdateSettingsRequest { Language = newLang }, ct);
                var langMsg = AssistantReplies.LanguageChanged(newLang);
                return await FinalizeAsync(userId, request, langMsg, newLang, cmd, ct);

            case AssistantIntent.Greeting:
                return await FinalizeAsync(userId, request, Greeting(language), language, cmd, ct);

            case AssistantIntent.Help:
                return await FinalizeAsync(userId, request, AssistantReplies.Help(language), language, cmd, ct);

            case AssistantIntent.CancelAction:
                _sessions.Clear(userId, sessionId);
                return await FinalizeAsync(userId, request, AssistantReplies.Cancelled(language), language, cmd, ct);

case AssistantIntent.CreateNote:
                if (string.IsNullOrWhiteSpace(cmd.Title) && string.IsNullOrWhiteSpace(cmd.Content))
                {
                    var noteStart = NextCaptureStage(cmd);
                    if (noteStart is not null)
                    {
                        _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = noteStart.Value });
                        return await FinalizeAsync(userId, request, CapturePrompt(noteStart.Value, cmd.Intent, language), language, cmd, ct, captureType: CaptureTypeForStage(noteStart.Value));
                    }
                }
                return await CreateNoteCoreAsync(userId, cmd, language, request, ct, sessionId, confirmFirst: true);

            case AssistantIntent.CreateTask:
                if (string.IsNullOrWhiteSpace(cmd.Title))
                {
                    var taskStart = NextCaptureStage(cmd);
                    if (taskStart is not null)
                    {
                        _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = taskStart.Value });
                        return await FinalizeAsync(userId, request, CapturePrompt(taskStart.Value, cmd.Intent, language), language, cmd, ct, captureType: CaptureTypeForStage(taskStart.Value));
                    }
                }
                return await CreateTaskCoreAsync(userId, cmd, language, request, ct, sessionId, confirmFirst: true);

            case AssistantIntent.CreateAppointment:
                {
                    var apptStart = NextCaptureStage(cmd);
                    if (apptStart is not null)
                    {
                        _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = apptStart.Value });
                        return await FinalizeAsync(userId, request, CapturePrompt(apptStart.Value, cmd.Intent, language), language, cmd, ct, captureType: CaptureTypeForStage(apptStart.Value));
                    }
                    return await CreateAppointmentCoreAsync(userId, cmd, language, tz, request, sessionId, ct, confirmFirst: true);
                }

            case AssistantIntent.CreateReminder:
                if (string.IsNullOrWhiteSpace(cmd.Title))
                {
                    var remStart = NextCaptureStage(cmd);
                    if (remStart is not null)
                    {
                        _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = remStart.Value });
                        return await FinalizeAsync(userId, request, CapturePrompt(remStart.Value, cmd.Intent, language), language, cmd, ct, captureType: CaptureTypeForStage(remStart.Value));
                    }
                }
                return await CreateReminderCoreAsync(userId, cmd, language, tz, request, ct);

            case AssistantIntent.CompleteTask:
                return await CompleteTaskCoreAsync(userId, cmd, language, request, sessionId, ct, confirmFirst: true);

            case AssistantIntent.DeleteTask:
            case AssistantIntent.DeleteNote:
            case AssistantIntent.DeleteReminder:
            case AssistantIntent.DeleteAppointment:
                return await DeleteCoreAsync(userId, cmd, language, request, sessionId, ct, confirmFirst: true);

            case AssistantIntent.ListTasks:
                {
                    var items = await _tasks.GetAllAsync(userId, ct);
                    var scoped = ScopeTasks(items, cmd.Scope, tz, language);
                    var msg = FormatTaskList(scoped, language);
                    return await FinalizeAsync(userId, request, msg, language, cmd, ct);
                }

            case AssistantIntent.ListReminders:
                {
                    var items = await _reminders.GetAllAsync(userId, ct);
                    var scoped = ScopeReminders(items, cmd.Scope, tz, language);
                    var msg = FormatReminderList(scoped, language);
                    return await FinalizeAsync(userId, request, msg, language, cmd, ct);
                }

            case AssistantIntent.ListAppointments:
                {
                    var items = await _appointments.GetAllAsync(userId, ct);
                    var scoped = ScopeAppointments(items, cmd.Scope, tz, language);
                    var msg = FormatAppointmentList(scoped, language);
                    return await FinalizeAsync(userId, request, msg, language, cmd, ct);
                }

            case AssistantIntent.ListNotes:
                {
                    var items = await _notes.GetAllAsync(userId, ct);
                    var msg = FormatNoteList(items, language);
                    return await FinalizeAsync(userId, request, msg, language, cmd, ct);
                }

            case AssistantIntent.TodaySchedule:
                return await ScheduleCoreAsync(userId, language, tz, 0, request, ct);

            case AssistantIntent.TomorrowSchedule:
                return await ScheduleCoreAsync(userId, language, tz, 1, request, ct);

            case AssistantIntent.SearchNotes:
            case AssistantIntent.SearchTasks:
            case AssistantIntent.SearchReminders:
            case AssistantIntent.SearchAppointments:
                return await SearchCoreAsync(userId, cmd, language, request, ct);

            case AssistantIntent.GeneralQuestion:
            case AssistantIntent.Weather:
            case AssistantIntent.WebSearch:
                return await GeneralQuestionCoreAsync(userId, cmd, language, request, ct);

            default:
                return await FinalizeAsync(userId, request, AssistantReplies.NotUnderstood(language), language, cmd, ct);
        }
    }

    private async Task<AssistantResponse> ExecuteConfirmedAsync(Guid userId, ParsedCommand cmd, string language, string tz, CancellationToken ct)
    {
        return cmd.Intent switch
        {
            AssistantIntent.CreateAppointment =>
                await CreateAppointmentCoreAsync(userId, cmd, language, tz, new AssistantRequest { Text = cmd.Title ?? string.Empty, SessionId = "confirm" }, "confirm", ct, confirmFirst: false),
            AssistantIntent.CreateNote =>
                await CreateNoteCoreAsync(userId, cmd, language, new AssistantRequest { Text = cmd.Title ?? string.Empty, SessionId = "confirm" }, ct, "confirm", confirmFirst: false),
            AssistantIntent.CreateTask =>
                await CreateTaskCoreAsync(userId, cmd, language, new AssistantRequest { Text = cmd.Title ?? string.Empty, SessionId = "confirm" }, ct, "confirm", confirmFirst: false),
            AssistantIntent.CreateReminder =>
                await CreateReminderCoreAsync(userId, cmd, language, tz, new AssistantRequest { Text = cmd.Title ?? string.Empty, SessionId = "confirm" }, ct),
            AssistantIntent.CompleteTask =>
                await CompleteTaskCoreAsync(userId, cmd, language, new AssistantRequest { Text = cmd.Title ?? string.Empty, SessionId = "confirm" }, "confirm", ct, confirmFirst: false),
            AssistantIntent.DeleteTask or AssistantIntent.DeleteNote or AssistantIntent.DeleteReminder or AssistantIntent.DeleteAppointment =>
                await DeleteCoreAsync(userId, cmd, language, new AssistantRequest { Text = cmd.Title ?? string.Empty, SessionId = "confirm" }, "confirm", ct, confirmFirst: false),
            _ => await DispatchAsync(userId, cmd, language, tz, "confirm", new AssistantRequest { Text = cmd.Title ?? string.Empty, SessionId = "confirm" }, ct)
        };
    }

    // ---------------------------------------------------------------
    // Create Note
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> CreateNoteCoreAsync(Guid userId, ParsedCommand cmd, string language, AssistantRequest request, CancellationToken ct, string sessionId = "confirm", bool confirmFirst = false)
    {
        var title = string.IsNullOrWhiteSpace(cmd.Title) ? cmd.Content ?? "Untitled note" : cmd.Title;
        if (title.Length > 120)
        {
            title = title[..120];
        }

        if (confirmFirst)
        {
            var section = SectionNameForPrompt(cmd, language);
            var message = ConfirmationPrompt(language, $"save this note under {section}");
            _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = 1 });
            return BuildResponse(message, AssistantIntent.CreateNote, language, requiresConfirmation: true, confirmationPrompt: message, pendingAction: AssistantIntent.CreateNote.ToString());
        }

        var created = await _notes.CreateAsync(userId, new CreateNoteRequest
        {
            Title = title,
            Content = cmd.Content ?? cmd.Title ?? title,
            OriginalLanguage = language,
            Tags = string.IsNullOrWhiteSpace(cmd.Category) ? new List<string>() : new List<string> { cmd.Category }
        }, ct);
        var msg = AssistantReplies.NoteCreated(language);
        return await FinalizeAsync(userId, request, msg, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // Create Task
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> CreateTaskCoreAsync(Guid userId, ParsedCommand cmd, string language, AssistantRequest request, CancellationToken ct, string sessionId = "confirm", bool confirmFirst = false)
    {
        if (confirmFirst)
        {
            var taskTitle = string.IsNullOrWhiteSpace(cmd.Title) ? "new task" : cmd.Title;
            var section = SectionNameForPrompt(cmd, language);
            var message = ConfirmationPrompt(language, $"add the task \"{taskTitle}\" under {section}");
            _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = 1 });
            return BuildResponse(message, AssistantIntent.CreateTask, language, requiresConfirmation: true, confirmationPrompt: message, pendingAction: AssistantIntent.CreateTask.ToString());
        }

        var created = await _tasks.CreateAsync(userId, new CreateTaskRequest
        {
            Title = string.IsNullOrWhiteSpace(cmd.Title) ? "New task" : cmd.Title,
            Description = cmd.Description,
            Priority = cmd.Priority,
            DueDate = cmd.Date.HasValue ? DateOnly.FromDateTime(cmd.Date.Value) : null,
            DueTime = cmd.Time,
            Category = cmd.Category
        }, ct);
        var msg = AssistantReplies.TaskCreated(created.Title, language);
        return await FinalizeAsync(userId, request, msg, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // Create Reminder
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> CreateReminderCoreAsync(Guid userId, ParsedCommand cmd, string language, string tz, AssistantRequest request, CancellationToken ct)
    {
        var when = cmd.Date is null
            ? _time.NowInTimeZone(tz).AddHours(1)
            : _time.ToUtc(cmd.Date.Value, tz);
        var created = await _reminders.CreateAsync(userId, new CreateReminderRequest
        {
            Title = string.IsNullOrWhiteSpace(cmd.Title) ? "Reminder" : cmd.Title,
            Message = cmd.Description,
            ReminderAt = when,
            Recurrence = cmd.Recurrence,
            Priority = cmd.Priority
        }, ct);
        var msg = AssistantReplies.ReminderCreated(created.Title, FormatWhen(language, created.ReminderAt), language);
        return await FinalizeAsync(userId, request, msg, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // Create Appointment (with confirmation)
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> CreateAppointmentCoreAsync(Guid userId, ParsedCommand cmd, string language, string tz, AssistantRequest request, string sessionId, CancellationToken ct, bool confirmFirst)
    {
        var start = cmd.Date is null
            ? _time.NowInTimeZone(tz).AddHours(1)
            : _time.ToUtc(cmd.Date.Value, tz);
        var duration = cmd.DurationMinutes ?? 30;
        var end = start.AddMinutes(duration);

        if (confirmFirst)
        {
            var description = BuildAppointmentDescription(cmd, language, start);
            var message = ConfirmationPrompt(language, description);
            _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = 1 });
            return BuildResponse(message, AssistantIntent.CreateAppointment, language, requiresConfirmation: true, confirmationPrompt: message, pendingAction: AssistantIntent.CreateAppointment.ToString());
        }

        var created = await _appointments.CreateAsync(userId, new CreateAppointmentRequest
        {
            Title = string.IsNullOrWhiteSpace(cmd.Title) ? "Appointment" : cmd.Title,
            StartDateTime = start,
            EndDateTime = end,
            Description = cmd.Description,
            Location = cmd.Location,
            Participants = cmd.Participants ?? new List<string>()
        }, ct);
        var msg = AssistantReplies.AppointmentScheduled(created.Title, FormatWhen(language, created.StartDateTime, created.EndDateTime), language);
        return await FinalizeAsync(userId, request, msg, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // Complete Task (with confirmation)
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> CompleteTaskCoreAsync(Guid userId, ParsedCommand cmd, string language, AssistantRequest request, string sessionId, CancellationToken ct, bool confirmFirst)
    {
        var title = string.IsNullOrWhiteSpace(cmd.Title) ? cmd.Description : cmd.Title;
        TaskDto? task = null;

        // "Complete task 1" -> treat the number as the 1-based index in the pending task list.
        if (int.TryParse(title, out var index) && index >= 1)
        {
            var all = await _tasks.GetAllAsync(userId, ct);
            var pending = all.Where(t => t.Status != Domain.Enums.TaskStatus.Completed)
                             .OrderBy(t => t.CreatedAt).ToList();
            if (pending.Count >= index) task = pending[index - 1];
        }
        else
        {
            task = await FindTaskByTitleAsync(userId, title ?? string.Empty, ct);
        }

        if (task is null)
        {
            var msg = NoTaskFound(language);
            return await FinalizeAsync(userId, request, msg, language, cmd, ct, success: false);
        }

        if (confirmFirst)
        {
            var message = ConfirmationPrompt(language, $"mark \"{task.Title}\" as completed");
            _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = 1 });
            return BuildResponse(message, AssistantIntent.CompleteTask, language, requiresConfirmation: true, confirmationPrompt: message, pendingAction: AssistantIntent.CompleteTask.ToString());
        }

        await _tasks.UpdateStatusAsync(userId, task.Id, new UpdateTaskStatusRequest { Status = Domain.Enums.TaskStatus.Completed }, ct);
        var msg2 = AssistantReplies.TaskCompleted(task.Title, language);
        return await FinalizeAsync(userId, request, msg2, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // Delete (with confirmation)
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> DeleteCoreAsync(Guid userId, ParsedCommand cmd, string language, AssistantRequest request, string sessionId, CancellationToken ct, bool confirmFirst = true)
    {
        var targetName = cmd.Title ?? cmd.Description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return await FinalizeAsync(userId, request, AssistantReplies.NotUnderstood(language), language, cmd, ct, success: false);
        }

        if (confirmFirst)
        {
            var confirmMsg = ConfirmationPrompt(language, BuildDeleteDescription(cmd, targetName, language));
            var repackaged = RepackageWithTitle(cmd, targetName);
            _sessions.Set(userId, sessionId, new PendingAction { Command = repackaged, SessionId = sessionId, Stage = 1 });
            return BuildResponse(confirmMsg, cmd.Intent, language, requiresConfirmation: true, confirmationPrompt: confirmMsg, pendingAction: cmd.Intent.ToString());
        }

        var deletedType = cmd.Intent switch
        {
            AssistantIntent.DeleteTask => "task",
            AssistantIntent.DeleteNote => "note",
            AssistantIntent.DeleteReminder => "reminder",
            _ => "appointment"
        };

        switch (cmd.Intent)
        {
            case AssistantIntent.DeleteTask:
                {
                    var t = await FindTaskByTitleAsync(userId, targetName, ct);
                    if (t is not null) { await _tasks.DeleteAsync(userId, t.Id, ct); }
                    break;
                }
            case AssistantIntent.DeleteNote:
                {
                    var n = await FindNoteByTitleAsync(userId, targetName, ct);
                    if (n is not null) { await _notes.DeleteAsync(userId, n.Id, ct); }
                    break;
                }
            case AssistantIntent.DeleteReminder:
                {
                    var r = await FindReminderByTitleAsync(userId, targetName, ct);
                    if (r is not null) { await _reminders.DeleteAsync(userId, r.Id, ct); }
                    break;
                }
            case AssistantIntent.DeleteAppointment:
                {
                    var a = await FindAppointmentByTitleAsync(userId, targetName, ct);
                    if (a is not null) { await _appointments.DeleteAsync(userId, a.Id, ct); }
                    break;
                }
        }

        var msg2 = Deleted(language, deletedType, targetName);
        return await FinalizeAsync(userId, request, msg2, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // Search
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> SearchCoreAsync(Guid userId, ParsedCommand cmd, string language, AssistantRequest request, CancellationToken ct)
    {
        var query = cmd.SearchQuery ?? cmd.Title ?? string.Empty;
        var scopes = cmd.Intent switch
        {
            AssistantIntent.SearchNotes => new[] { "notes" },
            AssistantIntent.SearchTasks => new[] { "tasks" },
            AssistantIntent.SearchReminders => new[] { "reminders" },
            AssistantIntent.SearchAppointments => new[] { "appointments" },
            _ => null
        };
        var results = await _search.SearchAsync(userId, new SearchRequest { Query = query, Scopes = scopes }, ct);
        var msg = FormatSearchResults(results, query, language);
        return await FinalizeAsync(userId, request, msg, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // General Q&A / weather / web search (passthrough)
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> GeneralQuestionCoreAsync(
        Guid userId, ParsedCommand cmd, string language, AssistantRequest request, CancellationToken ct)
    {
        var question = cmd.Title ?? request.Text ?? string.Empty;
        string? answer = null;

        // Live data first (no API key required) for weather and web search.
        if (cmd.Intent == AssistantIntent.Weather)
        {
            var weather = await _external.GetWeatherAsync(cmd.Location ?? question, ct);
            if (weather is not null)
            {
                answer = FormatWeather(language, weather);
            }
        }
        else if (cmd.Intent == AssistantIntent.WebSearch)
        {
            var summary = await _external.SearchWebAsync(cmd.SearchQuery ?? question, ct);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                answer = FormatSearchResult(language, summary);
            }
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            try
            {
                answer = await _ai.AnswerQuestionAsync(question, language, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider failed for general question: {Question}", question);
                answer = null;
            }
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = AssistantReplies.NotUnderstood(language);
        }
        return await FinalizeAsync(userId, request, answer, language, cmd, ct);
    }

    private static string FormatWeather(string lang, WeatherInfo w)
    {
        var temp = double.IsNaN(w.TemperatureC) ? 0 : Math.Round(w.TemperatureC);
        var wind = double.IsNaN(w.WindKmh) ? 0 : Math.Round(w.WindKmh);
        var condition = WeatherCodeText(lang, w.WeatherCode);
        return lang switch
        {
            "hi-IN" => $"अभी {w.Location} में {temp}°C है, {condition}। हवा {wind} किमी/घंटा है।",
            "te-IN" => $"ఇప్పుడు {w.Location}లో {temp}°C ఉంది, {condition}। గాలి {wind} కిమీ/గం ఉంది.",
            _ => $"Right now in {w.Location} it is {temp}°C with {condition}. Wind is {wind} km/h."
        };
    }

    private static string FormatSearchResult(string lang, string summary)
    {
        return lang switch
        {
            "hi-IN" => $"यहाँ कुछ जानकारी है: {summary}",
            "te-IN" => $"ఇక్కడ కొంత సమాచారం ఉంది: {summary}",
            _ => $"Here's what I found: {summary}"
        };
    }

    private static string WeatherCodeText(string lang, int code)
    {
        var text = code switch
        {
            0 => "clear sky",
            1 or 2 or 3 => "partly cloudy",
            45 or 48 => "foggy",
            51 or 53 or 55 or 56 or 57 => "drizzle",
            61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "rain",
            71 or 73 or 75 or 77 or 85 or 86 => "snow",
            95 or 96 or 99 => "thunderstorms",
            _ => "varied weather"
        };
        return lang switch
        {
            "hi-IN" => text switch
            {
                "clear sky" => "आसमान साफ है",
                "partly cloudy" => "आंशिक बादल हैं",
                "foggy" => "कोहरा है",
                "drizzle" => "बूंदाबांदी है",
                "rain" => "बारिश हो रही है",
                "snow" => "बर्फबारी है",
                "thunderstorms" => "आंधी-तूफान है",
                _ => "मौसम बदलता रहता है"
            },
            "te-IN" => text switch
            {
                "clear sky" => "ఆకాశం స్పష్టంగా ఉంది",
                "partly cloudy" => "పాక్షికంగా మేఘావృతమైంది",
                "foggy" => "పొగమంచు ఉంది",
                "drizzle" => "చిన్న చినుకులు",
                "rain" => "వర్షం పడుతోంది",
                "snow" => "మంచు కురుస్తోంది",
                "thunderstorms" => "ఉరుములతో కూడిన తుఫాను",
                _ => "మారుతున్న వాతావరణం"
            },
            _ => text
        };
    }

    // ---------------------------------------------------------------
    // Schedule
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> ScheduleCoreAsync(Guid userId, string language, string tz, int dayOffset, AssistantRequest request, CancellationToken ct)
    {
        var nowLocal = _time.NowInTimeZone(tz);
        var dayLocal = nowLocal.Date.AddDays(dayOffset);
        var fromUtc = _time.ToUtc(dayLocal, tz);
        var toUtc = _time.ToUtc(dayLocal.AddDays(1), tz);

        var allTasks = await _tasks.GetAllAsync(userId, ct);
        var allReminders = await _reminders.GetAllAsync(userId, ct);
        var appts = await _appointments.GetInRangeAsync(userId, fromUtc, toUtc, ct);

        var dayTasks = allTasks
            .Where(t => t.DueDate == DateOnly.FromDateTime(dayLocal) && t.Status != Domain.Enums.TaskStatus.Completed)
            .ToList();
        var dayReminders = allReminders.Where(r => r.ReminderAt >= fromUtc && r.ReminderAt < toUtc).ToList();

        var msg = FormatDailySchedule(language, dayOffset, dayTasks, appts.ToList(), dayReminders);
        return await FinalizeAsync(userId, request, msg, language, new ParsedCommand { Intent = dayOffset == 0 ? AssistantIntent.TodaySchedule : AssistantIntent.TomorrowSchedule }, ct);
    }

    // ---------------------------------------------------------------
    // Finalize
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> FinalizeAsync(
        Guid userId, AssistantRequest request, string response, string language, ParsedCommand cmd, CancellationToken ct, bool success = true, string? captureType = null)
    {
        await RecordConversationAsync(userId, request, response, cmd.Intent.ToString(), language, ct);
        return BuildResponse(response, cmd.Intent, language, captureType: captureType);
    }

    private async Task RecordConversationAsync(Guid userId, AssistantRequest request, string response, string intent, string language, CancellationToken ct)
    {
        try
        {
            await _conversations.AddAsync(new ConversationHistory
            {
                UserId = userId,
                UserMessage = request.Text,
                AssistantResponse = response,
                Language = language,
                Intent = intent,
                IsVoice = request.IsVoice
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist conversation history.");
        }
    }

    private static AssistantResponse BuildResponse(
        string reply, AssistantIntent intent, string language,
        bool requiresConfirmation = false, string? confirmationPrompt = null, string? pendingAction = null, string? captureType = null)
    {
        return new AssistantResponse
        {
            Reply = reply,
            Intent = intent.ToString(),
            Language = language,
            NeedsConfirmation = requiresConfirmation,
            ConfirmationPrompt = confirmationPrompt,
            PendingAction = pendingAction,
            CaptureType = captureType,
            TtsText = reply
        };
    }

    // ---------------------------------------------------------------
    // Guided capture helpers (multi-turn date/category/content/title)
    // ---------------------------------------------------------------
    private static int? NextCaptureStage(ParsedCommand cmd)
    {
        switch (cmd.Intent)
        {
            case AssistantIntent.CreateNote:
                // A bare "add note" asks which tab first; fully specified notes skip the section.
                if (string.IsNullOrWhiteSpace(cmd.Section) && !cmd.SectionSkipped &&
                    string.IsNullOrWhiteSpace(cmd.Title) && string.IsNullOrWhiteSpace(cmd.Content)) return 6;
                if (string.IsNullOrWhiteSpace(cmd.Category) && !cmd.CategorySkipped) return 5;
                if (string.IsNullOrWhiteSpace(cmd.Content)) return 2;
                return null;
            case AssistantIntent.CreateTask:
                if (string.IsNullOrWhiteSpace(cmd.Section) && !cmd.SectionSkipped && string.IsNullOrWhiteSpace(cmd.Title)) return 6;
                if (cmd.Date is null && !cmd.DateSkipped) return 4;
                if (cmd.Time is null && !cmd.TimeSkipped) return 7;
                if (string.IsNullOrWhiteSpace(cmd.Category) && !cmd.CategorySkipped) return 5;
                if (string.IsNullOrWhiteSpace(cmd.Title)) return 3;
                return null;
            case AssistantIntent.CreateAppointment:
                if (cmd.Date is null && !cmd.DateSkipped) return 4;
                if (cmd.Time is null && !cmd.TimeSkipped) return 7;
                if (string.IsNullOrWhiteSpace(cmd.Title)) return 3;
                return null;
            case AssistantIntent.CreateReminder:
                if (cmd.Date is null && !cmd.DateSkipped) return 4;
                if (cmd.Time is null && !cmd.TimeSkipped) return 7;
                if (string.IsNullOrWhiteSpace(cmd.Title)) return 3;
                return null;
            default:
                return null;
        }
    }

    private static string CapturePrompt(int stage, AssistantIntent intent, string lang) => stage switch
    {
        4 => AssistantReplies.AskDate(lang),
        5 => AssistantReplies.AskCategory(lang),
        6 => AssistantReplies.AskSection(lang),
        7 => AssistantReplies.AskTime(lang),
        2 => AssistantReplies.AskNoteContent(lang),
        3 => intent == AssistantIntent.CreateAppointment ? AssistantReplies.AskMeetingTitle(lang) : AssistantReplies.AskTaskTitle(lang),
        _ => AssistantReplies.NotUnderstood(lang)
    };

    private static string CaptureTypeForStage(int stage) => stage switch
    {
        4 => "date",
        5 => "category",
        6 => "section",
        7 => "time",
        _ => "text"
    };

    private async Task<AssistantResponse> ExecuteCapturedAsync(
        Guid userId, ParsedCommand cmd, string language, string tz, AssistantRequest request, string sessionId, CancellationToken ct)
    {
        return cmd.Intent switch
        {
            AssistantIntent.CreateNote => await CreateNoteCoreAsync(userId, cmd, language, request, ct, sessionId, confirmFirst: true),
            AssistantIntent.CreateTask => await CreateTaskCoreAsync(userId, cmd, language, request, ct, sessionId, confirmFirst: true),
            AssistantIntent.CreateAppointment => await CreateAppointmentCoreAsync(userId, cmd, language, tz, request, sessionId, ct, confirmFirst: true),
            AssistantIntent.CreateReminder => await CreateReminderCoreAsync(userId, cmd, language, tz, request, ct),
            _ => await FinalizeAsync(userId, request, AssistantReplies.NotUnderstood(language), language, cmd, ct)
        };
    }

    private static bool IsSkip(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower is "skip" or "none" or "no" or "nope" or "n/a" or "na" or "nothing" or "no date" or "no category" or "no thanks" ||
               lower.StartsWith("skip ") ||
               lower == "कोई नहीं" || lower.StartsWith("छोड़") || lower == "नहीं" ||
               lower == "లేదు" || lower.StartsWith("వదిలేయి") || lower == "వద్దు";
    }

    private static bool IsCategoryWord(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower is "work" or "office" or "business" or "professional" or
               "personal" or "home" or "family" or
               "shopping" or "groceries" or "grocery" or "errands" or "market" or
               "study" or "school" or "education" or "learning" or
               "health" or "fitness" or "workout" or "gym" or "medical" or
               "finance" or "money" or "bills" or "budget" or "accounts" or
               "travel" or "trip" or "vacation" or "holiday" or
               "other" or "misc" or "miscellaneous" or "general";
    }

    private static string NormalizeCategory(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower switch
        {
            "work" or "office" or "business" or "professional" => "Work",
            "personal" or "home" or "family" => "Personal",
            "shopping" or "groceries" or "grocery" or "errands" or "market" => "Shopping",
            "study" or "school" or "education" or "learning" => "Study",
            "health" or "fitness" or "workout" or "gym" or "medical" => "Health",
            "finance" or "money" or "bills" or "budget" or "accounts" => "Finance",
            "travel" or "trip" or "vacation" or "holiday" => "Travel",
            "other" or "misc" or "miscellaneous" or "general" => "Other",
            _ => CapitalizeFirst(text)
        };
    }

    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return char.ToUpperInvariant(input[0]) + input[1..];
    }

    private static AssistantIntent? ResolveSectionWord(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        if (lower is "notes" or "note" or "नोट" or "नोट्स" or "నోట్" or "నోట్స్") return AssistantIntent.CreateNote;
        if (lower is "tasks" or "task" or "to-do" or "todo" or "कार्य" or "टास्क" or "పనులు" or "టాస్క్") return AssistantIntent.CreateTask;
        if (lower is "appointments" or "appointment" or "meetings" or "meeting" or "calendar" or "मीटिंग" or "मीटिंग्स" or "అపాయింట్" or "మీటింగ్") return AssistantIntent.CreateAppointment;
        if (lower is "reminders" or "reminder" or "रिमाइंडर" or "రిమైండర్") return AssistantIntent.CreateReminder;
        return null;
    }

    private static string SectionLabel(AssistantIntent intent) => intent switch
    {
        AssistantIntent.CreateNote => "Notes",
        AssistantIntent.CreateTask => "Tasks",
        AssistantIntent.CreateAppointment => "Appointments",
        AssistantIntent.CreateReminder => "Reminders",
        _ => "Tasks"
    };

    private static string SectionNameForPrompt(ParsedCommand cmd, string language)
    {
        return string.IsNullOrWhiteSpace(cmd.Section) ? SectionLabel(cmd.Intent) : cmd.Section;
    }

    // ---------------------------------------------------------------
    // Lookup helpers
    // ---------------------------------------------------------------
    private async Task<TaskDto?> FindTaskByTitleAsync(Guid userId, string title, CancellationToken ct)
    {
        var items = await _tasks.GetAllAsync(userId, ct);
        var exact = items.FirstOrDefault(t => string.Equals(t.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // Partial match: task "Complete project report" should be found for "project report".
        var normalized = NormalizeForMatch(title);
        if (normalized.Length < 2) return null;
        return items.FirstOrDefault(t =>
        {
            var candidate = NormalizeForMatch(t.Title);
            return candidate.Length >= 2 &&
                   (candidate.Contains(normalized, StringComparison.Ordinal) ||
                    normalized.Contains(candidate, StringComparison.Ordinal));
        });
    }

    private static string NormalizeForMatch(string value)
    {
        return string.Join(" ", value.Trim().ToLowerInvariant()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !IsMatchStopWord(w)));
    }

    private static bool IsMatchStopWord(string word)
    {
        return word is "a" or "an" or "the" or "to" or "my" or "this" or "that" or "of" or "for" or "on" or "at" or "by" or "due";
    }

    private async Task<NoteDto?> FindNoteByTitleAsync(Guid userId, string title, CancellationToken ct)
    {
        var items = await _notes.GetAllAsync(userId, ct);
        return items.FirstOrDefault(n => string.Equals(n.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ReminderDto?> FindReminderByTitleAsync(Guid userId, string title, CancellationToken ct)
    {
        var items = await _reminders.GetAllAsync(userId, ct);
        return items.FirstOrDefault(r => string.Equals(r.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<AppointmentDto?> FindAppointmentByTitleAsync(Guid userId, string title, CancellationToken ct)
    {
        var items = await _appointments.GetAllAsync(userId, ct);
        return items.FirstOrDefault(a => string.Equals(a.Title.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------
    // Text helpers
    // ---------------------------------------------------------------
    private static bool IsAffirmative(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower == "yes" || lower.StartsWith("yes ") || lower == "yeah" || lower == "yep" || lower == "sure" || lower == "ok" ||
               lower == "haan" || lower.StartsWith("हाँ") || lower.StartsWith("हां") || lower.StartsWith("ठीक है") ||
               lower == "avunu" || lower.StartsWith("అవును") || lower.StartsWith("సరే");
    }

    private static bool IsNegative(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return lower == "no" || lower.StartsWith("no ") || lower == "nope" || lower == "cancel" || lower.StartsWith("never mind") ||
               lower.StartsWith("नहीं") || lower.StartsWith("रद्द") ||
               lower.StartsWith("లేదు") || lower.StartsWith("రద్దు") || lower.StartsWith("వద్దు");
    }

    private static string Greeting(string lang) => lang switch
    {
        "hi-IN" => AssistantReplies.GreetingHi,
        "te-IN" => AssistantReplies.GreetingTe,
        _ => AssistantReplies.Greeting
    };

    private static string NoTaskFound(string lang) => lang switch
    {
        "hi-IN" => "मुझे ऐसा कोई कार्य नहीं मिला।",
        "te-IN" => "నాకు అలాంటి టాస్క్ కనిపించలేదు.",
        _ => "I couldn't find a task matching that."
    };

    private static string ConfirmationPrompt(string lang, string description) => lang switch
    {
        "hi-IN" => $"क्या मैं {description} करूँ?",
        "te-IN" => $"నేను {description} చేయాలా?",
        _ => $"Should I {description}?"
    };

    private static string Deleted(string lang, string what, string name) => lang switch
    {
        "hi-IN" => $"'{name}' {what} हटा दिया गया है।",
        "te-IN" => $"'{name}' {what} తొలగించబడింది.",
        _ => $"'{name}' {what} has been deleted."
    };

    private string FormatWhen(string lang, DateTime start, DateTime? end = null)
    {
        return end is null
            ? FormatDateTime(lang, start)
            : $"{FormatTime(lang, start)} to {FormatTime(lang, end.Value)}";
    }

    private string FormatDateTime(string lang, DateTime dt)
    {
        var now = DateTime.Now.Date;
        var d = dt.ToLocalTime();
        var dateLabel = lang switch
        {
            "hi-IN" => "आज",
            "te-IN" => "ఈరోజు",
            _ => "today"
        };

        if (d.Date == now)
        {
            return lang switch
            {
                "hi-IN" => $"{FormatTime(lang, dt)} आज",
                "te-IN" => $"{FormatTime(lang, dt)} ఈరోజు",
                _ => $"{FormatTime(lang, dt)} today"
            };
        }

        if (d.Date == now.AddDays(1))
        {
            return lang switch
            {
                "hi-IN" => $"{FormatTime(lang, dt)} कल",
                "te-IN" => $"{FormatTime(lang, dt)} రేపు",
                _ => $"{FormatTime(lang, dt)} tomorrow"
            };
        }

        return $"{d:MMM d} at {FormatTime(lang, dt)}";
    }

    private string FormatTime(string lang, DateTime dt)
    {
        var d = dt.ToLocalTime();
        if (lang == "hi-IN")
        {
            return d.Hour switch
            {
                >= 4 and <= 11 => $"सुबह {d.Hour} बजे",
                12 => "दोपहर 12 बजे",
                >= 13 and <= 16 => $"दोपहर {d.Hour - 12} बजे",
                >= 17 and <= 20 => $"शाम {d.Hour - 12} बजे",
                _ => $"रात {d.Hour - 12} बजे"
            };
        }

        if (lang == "te-IN")
        {
            return d.Hour switch
            {
                >= 4 and <= 11 => $"ఉదయం {d.Hour} గంటలకు",
                12 => "మధ్యాహ్నం 12 గంటలకు",
                >= 13 and <= 16 => $"మధ్యాహ్నం {d.Hour - 12} గంటలకు",
                >= 17 and <= 20 => $"సాయంత్రం {d.Hour - 12} గంటలకు",
                _ => $"రాత్రి {d.Hour - 12} గంటలకు"
            };
        }

        return d.ToString("h:mm tt");
    }

    private string BuildAppointmentDescription(ParsedCommand cmd, string language, DateTime start)
    {
        var when = FormatWhen(language, start);
        return $"{cmd.Title} {when}";
    }

    private string BuildDeleteDescription(ParsedCommand cmd, string targetName, string language)
    {
        var what = cmd.Intent switch
        {
            AssistantIntent.DeleteTask => language == "hi-IN" ? "कार्य" : language == "te-IN" ? "టాస్క్" : "task",
            AssistantIntent.DeleteNote => language == "hi-IN" ? "नोट" : language == "te-IN" ? "నోట్" : "note",
            AssistantIntent.DeleteReminder => language == "hi-IN" ? "रिमाइंडर" : language == "te-IN" ? "రిమైండర్" : "reminder",
            _ => language == "hi-IN" ? "मीटिंग" : language == "te-IN" ? "అపాయింట్‌మెంట్" : "appointment"
        };

        return language switch
        {
            "hi-IN" => $"delete the {what} \"{targetName}\"",
            "te-IN" => $"తొలగించాలా {what} \"{targetName}\"",
            _ => $"delete the {what} \"{targetName}\""
        };
    }

    private static ParsedCommand RepackageWithTitle(ParsedCommand cmd, string title)
    {
        return new ParsedCommand
        {
            Intent = cmd.Intent,
            Title = title,
            Content = cmd.Content,
            Description = cmd.Description,
            Language = cmd.Language,
            Date = cmd.Date,
            Time = cmd.Time,
            DurationMinutes = cmd.DurationMinutes,
            Location = cmd.Location,
            Participants = cmd.Participants,
            SearchQuery = cmd.SearchQuery,
            Category = cmd.Category
        };
    }

    private static string FormatTaskList(IReadOnlyList<TaskDto> tasks, string lang)
    {
        if (tasks.Count == 0)
        {
            return AssistantReplies.NoTasks(lang);
        }

        var lines = tasks.Select((t, i) => $"{i + 1}. {t.Title} ({t.Status})").ToList();
        return string.Join("\n", lines);
    }

    private static string FormatReminderList(IReadOnlyList<ReminderDto> reminders, string lang)
    {
        if (reminders.Count == 0)
        {
            return AssistantReplies.NoReminders(lang);
        }

        var lines = reminders.Select((r, i) => $"{i + 1}. {r.Title} — {r.ReminderAt:MMM d, HH:mm}").ToList();
        return string.Join("\n", lines);
    }

    private static string FormatAppointmentList(IReadOnlyList<AppointmentDto> appointments, string lang)
    {
        if (appointments.Count == 0)
        {
            return AssistantReplies.NoAppointments(lang);
        }

        var lines = appointments.Select((a, i) => $"{i + 1}. {a.Title} — {a.StartDateTime:MMM d, HH:mm}").ToList();
        return string.Join("\n", lines);
    }

    private static (DateTime DayLocal, DateTime FromUtc, DateTime ToUtc) ScopeWindow(string? scope, string tz, ITimeZoneService time)
    {
        var nowLocal = time.NowInTimeZone(tz);
        var dayLocal = scope == "tomorrow" ? nowLocal.Date.AddDays(1) : nowLocal.Date;
        var fromUtc = time.ToUtc(dayLocal, tz);
        var toUtc = time.ToUtc(dayLocal.AddDays(1), tz);
        return (dayLocal, fromUtc, toUtc);
    }

    private List<TaskDto> ScopeTasks(IReadOnlyList<TaskDto> items, string? scope, string tz, string lang)
    {
        if (string.IsNullOrEmpty(scope)) return items.ToList();
        var (dayLocal, _, _) = ScopeWindow(scope, tz, _time);
        var day = DateOnly.FromDateTime(dayLocal);
        var scoped = items.Where(t => t.DueDate == day && t.Status != Domain.Enums.TaskStatus.Completed).ToList();
        return scoped.Count == 0 ? new List<TaskDto>() : scoped;
    }

    private List<ReminderDto> ScopeReminders(IReadOnlyList<ReminderDto> items, string? scope, string tz, string lang)
    {
        if (string.IsNullOrEmpty(scope)) return items.ToList();
        var (_, fromUtc, toUtc) = ScopeWindow(scope, tz, _time);
        return items.Where(r => r.ReminderAt >= fromUtc && r.ReminderAt < toUtc).ToList();
    }

    private List<AppointmentDto> ScopeAppointments(IReadOnlyList<AppointmentDto> items, string? scope, string tz, string lang)
    {
        if (string.IsNullOrEmpty(scope)) return items.ToList();
        var (_, fromUtc, toUtc) = ScopeWindow(scope, tz, _time);
        return items.Where(a => a.StartDateTime >= fromUtc && a.StartDateTime < toUtc).ToList();
    }

    private static string FormatNoteList(IReadOnlyList<NoteDto> notes, string lang)
    {
        if (notes.Count == 0)
        {
            return AssistantReplies.NoNotes(lang);
        }

        var lines = notes.Select((n, i) => $"{i + 1}. {n.Title}").ToList();
        return string.Join("\n", lines);
    }

    private static string FormatSearchResults(SearchResponse results, string query, string lang)
    {
        var total = results.TotalCount;
        if (total == 0)
        {
            return lang switch
            {
                "hi-IN" => $"'{query}' के लिए कोई परिणाम नहीं मिला।",
                "te-IN" => $"'{query}' కోసం ఫలితాలు లేవు.",
                _ => $"No results found for '{query}'."
            };
        }

        return lang switch
        {
            "hi-IN" => $"'{query}' के लिए {total} परिणाम मिले।",
            "te-IN" => $"'{query}' కోసం {total} ఫలితాలు కనుగొన్నాను.",
            _ => $"I found {total} results for '{query}'."
        };
    }

    private string FormatDailySchedule(string lang, int dayOffset, List<TaskDto> tasks, List<AppointmentDto> appts, List<ReminderDto> reminders)
    {
        var header = dayOffset == 0
            ? lang switch { "hi-IN" => "आज का कार्यक्रम:", "te-IN" => "ఈరోజు షెడ్యూల్:", _ => "Today's schedule:" }
            : lang switch { "hi-IN" => "कल का कार्यक्रम:", "te-IN" => "రేపు షెడ్యూల్:", _ => "Tomorrow's schedule:" };

        var parts = new List<string> { header };

        if (appts.Count > 0)
        {
            parts.Add(lang == "hi-IN" ? "मीटिंग:" : lang == "te-IN" ? "అపాయింట్‌మెంట్‌లు:" : "Appointments:");
            parts.AddRange(appts.Select(a => $"{FormatTime(lang, a.StartDateTime)} — {a.Title}"));
        }

        if (tasks.Count > 0)
        {
            parts.Add(lang == "hi-IN" ? "कार्य:" : lang == "te-IN" ? "టాస్క్‌లు:" : "Tasks:");
            parts.AddRange(tasks.Select(t => $"☐ {t.Title}"));
        }

        if (reminders.Count > 0)
        {
            parts.Add(lang == "hi-IN" ? "रिमाइंडर:" : lang == "te-IN" ? "రిమైండర్‌లు:" : "Reminders:");
            parts.AddRange(reminders.Select(r => $"⏰ {r.Title} — {FormatTime(lang, r.ReminderAt)}"));
        }

        if (parts.Count == 1)
        {
            parts.Add(lang switch
            {
                "hi-IN" => "आपके पास कुछ भी निर्धारित नहीं है।",
                "te-IN" => "మీకు ఏదీ షెడ్యూల్ చేయబడలేదు.",
                _ => "You have nothing scheduled."
            });
        }

        return string.Join("\n", parts);
    }
}
