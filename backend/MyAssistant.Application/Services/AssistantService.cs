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
    private readonly ISettingsService _settings;
    private readonly IConversationRepository _conversations;
    private readonly ISubscriptionService _subscription;
    private readonly ILogger<AssistantService> _logger;

    private readonly INoteService _notes;
    private readonly ITaskService _tasks;
    private readonly IReminderService _reminders;
    private readonly IAppointmentService _appointments;
    private readonly ISearchService _search;

    public AssistantService(
        IAssistantAIService ai,
        IAssistantSessionStore sessions,
        ITimeZoneService time,
        ISettingsService settings,
        IConversationRepository conversations,
        ISubscriptionService subscription,
        ILogger<AssistantService> logger,
        INoteService notes,
        ITaskService tasks,
        IReminderService reminders,
        IAppointmentService appointments,
        ISearchService search)
    {
        _ai = ai;
        _sessions = sessions;
        _time = time;
        _settings = settings;
        _conversations = conversations;
        _subscription = subscription;
        _logger = logger;
        _notes = notes;
        _tasks = tasks;
        _reminders = reminders;
        _appointments = appointments;
        _search = search;
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
        if (pending is not null && pending.Stage is 2 or 3)
        {
            // We're waiting for the note content / task title the user just said.
            var text = request.Text?.Trim() ?? string.Empty;
            if (command.Intent == AssistantIntent.Denial || IsNegative(text))
            {
                _sessions.Clear(userId, sessionId);
                var cancelled = AssistantReplies.Cancelled(language);
                await RecordConversationAsync(userId, request, cancelled, AssistantIntent.CancelAction.ToString(), language, cancellationToken);
                return BuildResponse(cancelled, AssistantIntent.CancelAction, language);
            }

            // A real command while we're mid-capture supersedes the pending capture.
            if (command.Intent != AssistantIntent.Unknown)
            {
                _sessions.Clear(userId, sessionId);
                return await DispatchAsync(userId, command, language, tz, sessionId, request, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                var stillAsk = pending.Stage == 2
                    ? AssistantReplies.AskNoteContent(language)
                    : AssistantReplies.AskTaskTitle(language);
                return BuildResponse(stillAsk, pending.Command.Intent, language);
            }

            _sessions.Clear(userId, sessionId);
            var captured = pending.Stage == 2
                ? new ParsedCommand
                {
                    Intent = AssistantIntent.CreateNote,
                    Title = text.Length > 120 ? text[..120] : text,
                    Content = text,
                    Language = language
                }
                : new ParsedCommand
                {
                    Intent = AssistantIntent.CreateTask,
                    Title = text.Length > 200 ? text[..200] : text,
                    Language = language
                };
            var result = pending.Stage == 2
                ? await CreateNoteCoreAsync(userId, captured, language, request, cancellationToken)
                : await CreateTaskCoreAsync(userId, captured, language, request, cancellationToken);
            await RecordConversationAsync(userId, request, result.Reply ?? string.Empty, captured.Intent.ToString(), language, cancellationToken);
            return result;
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
                    _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = 2 });
                    return await FinalizeAsync(userId, request, AssistantReplies.AskNoteContent(language), language, cmd, ct);
                }
                return await CreateNoteCoreAsync(userId, cmd, language, request, ct);

            case AssistantIntent.CreateTask:
                if (string.IsNullOrWhiteSpace(cmd.Title))
                {
                    _sessions.Set(userId, sessionId, new PendingAction { Command = cmd, SessionId = sessionId, Stage = 3 });
                    return await FinalizeAsync(userId, request, AssistantReplies.AskTaskTitle(language), language, cmd, ct);
                }
                return await CreateTaskCoreAsync(userId, cmd, language, request, ct);

            case AssistantIntent.CreateReminder:
                return await CreateReminderCoreAsync(userId, cmd, language, tz, request, ct);

            case AssistantIntent.CreateAppointment:
                return await CreateAppointmentCoreAsync(userId, cmd, language, tz, request, sessionId, ct, confirmFirst: true);

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
    private async Task<AssistantResponse> CreateNoteCoreAsync(Guid userId, ParsedCommand cmd, string language, AssistantRequest request, CancellationToken ct)
    {
        var title = string.IsNullOrWhiteSpace(cmd.Title) ? cmd.Content ?? "Untitled note" : cmd.Title;
        if (title.Length > 120)
        {
            title = title[..120];
        }

        var created = await _notes.CreateAsync(userId, new CreateNoteRequest
        {
            Title = title,
            Content = cmd.Content ?? cmd.Title ?? title,
            OriginalLanguage = language
        }, ct);
        var msg = AssistantReplies.NoteCreated(language);
        return await FinalizeAsync(userId, request, msg, language, cmd, ct);
    }

    // ---------------------------------------------------------------
    // Create Task
    // ---------------------------------------------------------------
    private async Task<AssistantResponse> CreateTaskCoreAsync(Guid userId, ParsedCommand cmd, string language, AssistantRequest request, CancellationToken ct)
    {
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
        Guid userId, AssistantRequest request, string response, string language, ParsedCommand cmd, CancellationToken ct, bool success = true)
    {
        await RecordConversationAsync(userId, request, response, cmd.Intent.ToString(), language, ct);
        return BuildResponse(response, cmd.Intent, language);
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
        bool requiresConfirmation = false, string? confirmationPrompt = null, string? pendingAction = null)
    {
        return new AssistantResponse
        {
            Reply = reply,
            Intent = intent.ToString(),
            Language = language,
            NeedsConfirmation = requiresConfirmation,
            ConfirmationPrompt = confirmationPrompt,
            PendingAction = pendingAction,
            TtsText = reply
        };
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
