using Microsoft.Extensions.Logging;
using MyAssistant.Application.AI;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Infrastructure.Services.AI;

namespace MyAssistant.Infrastructure.Services;

/// <summary>
/// Executes detected intents against the productivity services, enforces usage limits
/// and generates localized assistant responses.
/// </summary>
public class AssistantOrchestrator
{
    private readonly IAssistantAiService _ai;
    private readonly IProductivityService _productivity;
    private readonly ISubscriptionService _subscriptions;
    private readonly ILogger<AssistantOrchestrator> _logger;

    public AssistantOrchestrator(
        IAssistantAiService ai,
        IProductivityService productivity,
        ISubscriptionService subscriptions,
        ILogger<AssistantOrchestrator> logger)
    {
        _ai = ai;
        _productivity = productivity;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task<AssistantResult> ProcessAsync(AssistantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new AppError("Command text is required.", 400, "EMPTY_COMMAND");

        IntentResult? intent = null;
        try
        {
            intent = await _ai.DetectIntentAsync(request);

            if (intent.NeedsClarification)
            {
                var question = intent.ClarificationQuestion ?? Localize("Could you please provide more details?", intent.Language);
                return new AssistantResult
                {
                    Success = true,
                    Intent = intent.Intent,
                    IntentData = intent,
                    NeedsClarification = true,
                    ClarificationQuestion = question,
                    ResponseText = question,
                    ResponseLanguage = intent.Language
                };
            }

            var executed = await ExecuteAsync(request.UserId, intent, request.Timezone);

            if (!IsReadOnlyIntent(intent.Intent))
            {
                bool allowed = await _subscriptions.EnforceUsageLimitAsync(request.UserId, "AI_Request");
                if (!allowed)
                    return new AssistantResult
                    {
                        Success = false,
                        ResponseText = Localize("You've reached your free AI usage limit. Please upgrade your plan.", request.Language),
                        ResponseLanguage = request.Language,
                        Error = "USAGE_LIMIT"
                    };
                await _subscriptions.RecordUsageAsync(request.UserId, "AI_Request");
            }

            var usage = await _subscriptions.GetUsageAsync(request.UserId);
            return new AssistantResult
            {
                Success = true,
                Intent = intent.Intent,
                IntentData = intent,
                ResponseText = executed,
                ResponseLanguage = intent.Language,
                EntityId = intent.EntityId,
                UsageAiRequests = usage.AiRequests,
                UsageAiLimit = usage.AiLimit == int.MaxValue ? -1 : usage.AiLimit
            };
        }
        catch (AppError ex)
        {
            return new AssistantResult
            {
                Success = false,
                Intent = intent?.Intent,
                Error = ex.ErrorCode ?? "error",
                ResponseText = ex.Message,
                ResponseLanguage = request.Language
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assistant command processing failed");
            return new AssistantResult
            {
                Success = false,
                Error = "internal_error",
                ResponseText = Localize("Sorry, something went wrong. Please try again.", request.Language),
                ResponseLanguage = request.Language
            };
        }
    }

    private static bool IsReadOnlyIntent(string intent) => intent switch
    {
        AssistantIntents.Help or AssistantIntents.Confirm or AssistantIntents.Cancel
            or AssistantIntents.GetTodaySchedule or AssistantIntents.GetTomorrowSchedule
            or AssistantIntents.GetUpcomingSchedule or AssistantIntents.SearchNotes
            or AssistantIntents.Search or AssistantIntents.ListTasks
            or AssistantIntents.ListReminders or AssistantIntents.ListAppointments => true,
        _ => false
    };

    private async Task<string> ExecuteAsync(Guid userId, IntentResult intent, string timezone)
    {
        switch (intent.Intent)
        {
            case AssistantIntents.CreateNote: return await CreateNoteAsync(userId, intent);
            case AssistantIntents.DeleteNote: return await DeleteNoteAsync(userId, intent);
            case AssistantIntents.SearchNotes: return await SearchNotesAsync(userId, intent);
            case AssistantIntents.CreateTask: return await CreateTaskAsync(userId, intent);
            case AssistantIntents.CompleteTask: return await CompleteTaskAsync(userId, intent);
            case AssistantIntents.DeleteTask: return await DeleteTaskAsync(userId, intent);
            case AssistantIntents.CreateReminder: return await CreateReminderAsync(userId, intent);
            case AssistantIntents.DeleteReminder: return await DeleteReminderAsync(userId, intent);
            case AssistantIntents.CreateAppointment: return await CreateAppointmentAsync(userId, intent);
            case AssistantIntents.DeleteAppointment: return await DeleteAppointmentAsync(userId, intent);
            case AssistantIntents.GetTodaySchedule: return await ScheduleAsync(userId, 0, timezone);
            case AssistantIntents.GetTomorrowSchedule: return await ScheduleAsync(userId, 1, timezone);
            case AssistantIntents.GetUpcomingSchedule: return await UpcomingScheduleAsync(userId, timezone);
            case AssistantIntents.Help: return HelpText(intent.Language);
            case AssistantIntents.Confirm: return Localize("Great, action confirmed.", intent.Language);
            case AssistantIntents.Cancel: return Localize("Cancelled. How else can I help you?", intent.Language);
            case AssistantIntents.ChangeLanguage: return Localize("Language switched. How can I help you?", intent.Language);
            case AssistantIntents.Search: return await SearchAsync(userId, intent);
            case AssistantIntents.ListTasks: return await ListTasksAsync(userId, intent);
            case AssistantIntents.ListReminders: return await ListRemindersAsync(userId, intent);
            case AssistantIntents.ListAppointments: return await ListAppointmentsAsync(userId, intent);
            case AssistantIntents.UpdateAppointment: return await UpdateAppointmentAsync(userId, intent);
            case AssistantIntents.UpdateReminder: return await UpdateReminderAsync(userId, intent);
            default: return HelpText(intent.Language);
        }
    }

    // ----- notes -----
    private async Task<string> CreateNoteAsync(Guid userId, IntentResult intent)
    {
        await EnsureAllowedAsync(userId, "Note", "notes_limit");
        var title = TitleOr(intent, Localize("Note", intent.Language));
        var content = intent.Content ?? title;
        var dto = await _productivity.CreateNoteAsync(userId,
            new CreateNoteRequest(title, content, intent.Language, intent.Tags), "Asia/Kolkata");
        intent.EntityId = dto.Id;
        return Localize($"Done. I've saved your note \"{title}\".", intent.Language);
    }

    private async Task<string> DeleteNoteAsync(Guid userId, IntentResult intent)
    {
        if (intent.Id is null || !Guid.TryParse(intent.Id, out var id))
            return Localize("Which note would you like me to delete?", intent.Language);
        await _productivity.DeleteNoteAsync(userId, id);
        return Localize("The note has been deleted.", intent.Language);
    }

    private async Task<string> SearchNotesAsync(Guid userId, IntentResult intent)
    {
        var notes = await _productivity.GetNotesAsync(userId);
        var q = intent.Query?.ToLowerInvariant();
        var matched = string.IsNullOrWhiteSpace(q)
            ? notes
            : notes.Where(n => n.Title.ToLowerInvariant().Contains(q) || n.Content.ToLowerInvariant().Contains(q)).ToList();

        if (matched.Count == 0) return Localize("I couldn't find any matching notes.", intent.Language);
        if (matched.Count <= 3)
            return Localize($"I found {matched.Count} {(matched.Count == 1 ? "note" : "notes")}: {string.Join("; ", matched.Select(n => $"\"{n.Title}\""))}.", intent.Language);
        return Localize($"I found {matched.Count} notes matching your search.", intent.Language);
    }

    // ----- tasks -----
    private async Task<string> CreateTaskAsync(Guid userId, IntentResult intent)
    {
        await EnsureAllowedAsync(userId, "Task", "tasks_limit");
        var title = TitleOr(intent, Localize("Task", intent.Language));
        var due = ResolveDateTime(intent);
        var dto = await _productivity.CreateTaskAsync(userId, new CreateTaskRequest(
            title, intent.Description, intent.Priority,
            due?.ToString("yyyy-MM-dd"), due?.ToString("HH:mm")), "Asia/Kolkata");
        intent.EntityId = dto.Id;
        return CreatedResponse(intent.Language, "task", title, due?.ToString("yyyy-MM-dd"), due?.ToString("HH:mm"));
    }

    private async Task<string> CompleteTaskAsync(Guid userId, IntentResult intent)
    {
        var tasks = await _productivity.GetTasksAsync(userId);
        var q = (intent.Title ?? intent.Query)?.ToLowerInvariant();
        var target = string.IsNullOrWhiteSpace(q)
            ? tasks.FirstOrDefault(t => t.Status == "Pending")
            : tasks.FirstOrDefault(t => t.Title.ToLowerInvariant().Contains(q!));
        if (target is null) return Localize("I couldn't find that task.", intent.Language);
        var updated = await _productivity.UpdateTaskAsync(userId, target.Id, new UpdateTaskRequest(Status: "Completed"));
        return Localize($"Marked \"{updated.Title}\" as completed.", intent.Language);
    }

    private async Task<string> DeleteTaskAsync(Guid userId, IntentResult intent)
    {
        var tasks = await _productivity.GetTasksAsync(userId);
        var q = (intent.Title ?? intent.Query)?.ToLowerInvariant();
        var target = string.IsNullOrWhiteSpace(q)
            ? tasks.FirstOrDefault()
            : tasks.FirstOrDefault(t => t.Title.ToLowerInvariant().Contains(q!));
        if (target is null) return Localize("I couldn't find a matching task to delete.", intent.Language);
        await _productivity.DeleteTaskAsync(userId, target.Id);
        return Localize($"Deleted the task \"{target.Title}\".", intent.Language);
    }

    private async Task<string> ListTasksAsync(Guid userId, IntentResult intent)
    {
        var tasks = await _productivity.GetTasksAsync(userId);
        var pending = tasks.Where(t => t.Status == "Pending").ToList();
        if (pending.Count == 0) return Localize("You have no pending tasks. Great job!", intent.Language);
        var items = pending.Take(10).Select(t => $"• {t.Title}");
        return Localize($"You have {pending.Count} pending {(pending.Count == 1 ? "task" : "tasks")}:\n{string.Join("\n", items)}", intent.Language);
    }

    // ----- reminders -----
    private async Task<string> CreateReminderAsync(Guid userId, IntentResult intent)
    {
        await EnsureAllowedAsync(userId, "Reminder", "reminders_limit");
        var title = TitleOr(intent, Localize("Reminder", intent.Language));
        var when = ResolveDateTime(intent) ?? TimeZoneInfo.ConvertTime(DateTime.UtcNow, Ist()).AddMinutes(30);
        var dto = await _productivity.CreateReminderAsync(userId, new CreateReminderRequest(
            title, intent.Description, when, Timezone: "Asia/Kolkata", Recurrence: intent.Recurrence ?? "Once"), "Asia/Kolkata");
        intent.EntityId = dto.Id;
        return ConfirmedReminder(intent.Language, title, when, intent.Recurrence ?? "Once");
    }

    private async Task<string> DeleteReminderAsync(Guid userId, IntentResult intent)
    {
        if (intent.Id is not null && Guid.TryParse(intent.Id, out var id))
        {
            await _productivity.DeleteReminderAsync(userId, id);
            return Localize("The reminder has been deleted.", intent.Language);
        }
        var reminders = await _productivity.GetRemindersAsync(userId, onlyPending: true);
        var q = (intent.Title ?? intent.Query)?.ToLowerInvariant();
        var target = string.IsNullOrWhiteSpace(q)
            ? reminders.FirstOrDefault()
            : reminders.FirstOrDefault(r => r.Title.ToLowerInvariant().Contains(q!));
        if (target is null) return Localize("I couldn't find that reminder.", intent.Language);
        await _productivity.DeleteReminderAsync(userId, target.Id);
        return Localize($"The reminder \"{target.Title}\" has been deleted.", intent.Language);
    }

    private async Task<string> ListRemindersAsync(Guid userId, IntentResult intent)
    {
        var reminders = await _productivity.GetRemindersAsync(userId, onlyPending: true);
        if (reminders.Count == 0) return Localize("You have no pending reminders.", intent.Language);
        var items = reminders.Take(10).Select(r => $"• {r.Title} — {r.ReminderDateTime:hh:mm tt}");
        return Localize($"You have {reminders.Count} pending {(reminders.Count == 1 ? "reminder" : "reminders")}:\n{string.Join("\n", items)}", intent.Language);
    }

    // ----- appointments -----
    private async Task<string> CreateAppointmentAsync(Guid userId, IntentResult intent)
    {
        await EnsureAllowedAsync(userId, "Appointment", "appointments_limit");
        var title = TitleOr(intent, Localize("Meeting", intent.Language));
        var duration = intent.DurationMinutes ?? 30;
        var start = ResolveDateTime(intent) ?? NowIst().AddHours(1);

        var dto = await _productivity.CreateAppointmentAsync(userId, new CreateAppointmentRequest(
            title, intent.Description, start, DurationMinutes: duration,
            Location: intent.Location, Participants: intent.Participants,
            ReminderMinutes: intent.ReminderMinutes is not null && int.TryParse(intent.ReminderMinutes, out var rm) ? rm : 15,
            Timezone: "Asia/Kolkata"), "Asia/Kolkata");

        intent.EntityId = dto.Id;
        return Localize($"Done. {title} is scheduled from {dto.StartDateTime:hh:mm tt} to {dto.EndDateTime:hh:mm tt}.", intent.Language);
    }

    private async Task<string> DeleteAppointmentAsync(Guid userId, IntentResult intent)
    {
        var appointments = await _productivity.GetAppointmentsAsync(userId, DateTime.Today, DateTime.Today.AddYears(1));
        var q = intent.Title?.ToLowerInvariant();
        var target = string.IsNullOrWhiteSpace(q)
            ? appointments.FirstOrDefault(a => a.Status == "Scheduled")
            : appointments.FirstOrDefault(a => a.Title.ToLowerInvariant().Contains(q!));
        if (target is null) return Localize("I couldn't find a matching appointment.", intent.Language);
        await _productivity.DeleteAppointmentAsync(userId, target.Id);
        return Localize($"The appointment \"{target.Title}\" has been deleted.", intent.Language);
    }

    private async Task<string> ListAppointmentsAsync(Guid userId, IntentResult intent)
    {
        var appointments = await _productivity.GetAppointmentsAsync(userId, DateTime.Today, DateTime.Today.AddDays(30));
        if (appointments.Count == 0) return Localize("You have no upcoming appointments.", intent.Language);
        var items = appointments.Take(10).Select(a => $"• {a.StartDateTime:ddd dd MMM hh:mm tt} — {a.Title}");
        return Localize($"You have {appointments.Count} upcoming {(appointments.Count == 1 ? "appointment" : "appointments")}:\n{string.Join("\n", items)}", intent.Language);
    }

    private async Task<string> UpdateAppointmentAsync(Guid userId, IntentResult intent)
    {
        var appointments = await _productivity.GetAppointmentsAsync(userId, DateTime.Today, DateTime.Today.AddYears(1));
        var q = intent.Title?.ToLowerInvariant();
        var target = string.IsNullOrWhiteSpace(q)
            ? appointments.FirstOrDefault(a => a.Status == "Scheduled")
            : appointments.FirstOrDefault(a => a.Title.ToLowerInvariant().Contains(q!));
        if (target is null) return Localize("I couldn't find a matching appointment to update.", intent.Language);
        var newStart = ResolveDateTime(intent);
        if (newStart is not null)
        {
            var dto = await _productivity.UpdateAppointmentAsync(userId, target.Id,
                new UpdateAppointmentRequest(StartDateTime: newStart));
            return Localize($"Updated \"{target.Title}\" to {newStart:ddd dd MMM hh:mm tt}.", intent.Language);
        }
        return Localize("When would you like to reschedule it to?", intent.Language);
    }

    private async Task<string> UpdateReminderAsync(Guid userId, IntentResult intent)
    {
        var reminders = await _productivity.GetRemindersAsync(userId, onlyPending: true);
        var q = (intent.Title ?? intent.Query)?.ToLowerInvariant();
        var target = string.IsNullOrWhiteSpace(q)
            ? reminders.FirstOrDefault()
            : reminders.FirstOrDefault(r => r.Title.ToLowerInvariant().Contains(q!));
        if (target is null) return Localize("I couldn't find a matching reminder to update.", intent.Language);
        var newTime = ResolveDateTime(intent);
        if (newTime is not null)
        {
            var dto = await _productivity.UpdateReminderAsync(userId, target.Id,
                new UpdateReminderRequest(ReminderDateTime: newTime));
            return Localize($"Updated \"{target.Title}\" to {newTime:ddd dd MMM hh:mm tt}.", intent.Language);
        }
        return Localize("When would you like to reschedule the reminder to?", intent.Language);
    }

    // ----- schedules -----
    private async Task<string> ScheduleAsync(Guid userId, int dayOffset, string timezone)
    {
        var tz = Ist(timezone);
        var targetDate = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date.AddDays(dayOffset);
        var nextDate = targetDate.AddDays(1);
        var lang = "en-IN";

        var appointments = await _productivity.GetAppointmentsAsync(userId, targetDate, nextDate);
        var tasks = await _productivity.GetTasksAsync(userId);
        var reminders = await _productivity.GetRemindersAsync(userId, onlyPending: true);

        var appts = appointments.Where(a => a.StartDateTime.Date == targetDate).OrderBy(a => a.StartDateTime).ToList();
        var pendingTasks = tasks.Where(t => t.Status == "Pending").ToList();
        var dayReminders = reminders.Where(r => r.ReminderDateTime.Date == targetDate).ToList();

        var dayWord = dayOffset == 0 ? "today" : "tomorrow";
        if (appts.Count == 0 && pendingTasks.Count == 0 && dayReminders.Count == 0)
            return Localize($"You have no scheduled items {dayWord}.", lang);

        var parts = new List<string>();
        if (appts.Count > 0)
        {
            parts.Add(Localize($"You have {appts.Count} {(appts.Count == 1 ? "appointment" : "appointments")} {dayWord}.", lang));
            foreach (var a in appts)
                parts.Add(Localize($"{a.StartDateTime:hh:mm tt} — {a.Title}", lang));
        }
        if (pendingTasks.Count > 0)
        {
            parts.Add(Localize($"You also have {pendingTasks.Count} pending {(pendingTasks.Count == 1 ? "task" : "tasks")}.", lang));
            foreach (var t in pendingTasks.Take(5))
                parts.Add(Localize($"• {t.Title}", lang));
        }
        if (dayReminders.Count > 0)
        {
            parts.Add(Localize("Reminders:", lang));
            foreach (var r in dayReminders.Take(5))
                parts.Add(Localize($"⏰ {r.Title} at {r.ReminderDateTime:HH:mm}", lang));
        }
        return string.Join("\n", parts);
    }

    private async Task<string> UpcomingScheduleAsync(Guid userId, string timezone)
    {
        var tz = Ist(timezone);
        var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        var lang = "en-IN";

        var appointments = await _productivity.GetAppointmentsAsync(userId, now);
        var tasks = await _productivity.GetTasksAsync(userId);
        var reminders = await _productivity.GetRemindersAsync(userId, onlyPending: true);

        var upcoming = appointments.Where(a => a.StartDateTime >= now).OrderBy(a => a.StartDateTime).Take(5).ToList();
        var pendingTasks = tasks.Where(t => t.Status == "Pending").Take(5).ToList();
        var pendingReminders = reminders.Where(r => r.ReminderDateTime >= now).OrderBy(r => r.ReminderDateTime).Take(5).ToList();

        if (upcoming.Count == 0 && pendingTasks.Count == 0 && pendingReminders.Count == 0)
            return Localize("You have nothing scheduled right now.", lang);

        var parts = new List<string>();
        if (upcoming.Count > 0)
        {
            parts.Add(Localize($"You have {upcoming.Count} upcoming {(upcoming.Count == 1 ? "appointment" : "appointments")}.", lang));
            foreach (var a in upcoming)
                parts.Add(Localize($"{a.StartDateTime:ddd MMM dd h:mm tt} — {a.Title}", lang));
        }
        if (pendingTasks.Count > 0)
        {
            parts.Add(Localize($"{pendingTasks.Count} pending {(pendingTasks.Count == 1 ? "task" : "tasks")}:", lang));
            foreach (var t in pendingTasks) parts.Add(Localize($"• {t.Title}", lang));
        }
        if (pendingReminders.Count > 0)
        {
            parts.Add(Localize($"{pendingReminders.Count} upcoming {(pendingReminders.Count == 1 ? "reminder" : "reminders")}:", lang));
            foreach (var r in pendingReminders) parts.Add(Localize($"⏰ {r.Title} at {r.ReminderDateTime:hh:mm tt}", lang));
        }
        return string.Join("\n", parts);
    }

    // ----- search -----
    private async Task<string> SearchAsync(Guid userId, IntentResult intent)
    {
        var q = intent.Query;
        var lang = "en-IN";

        var notes = await _productivity.GetNotesAsync(userId);
        var tasks = await _productivity.GetTasksAsync(userId);
        var reminders = await _productivity.GetRemindersAsync(userId);

        var noteHits = Filter(q, notes.Select(n => (Key: (n.Title + " " + n.Content).ToLowerInvariant(), Display: $"\"{n.Title}\"")).ToList());
        var taskHits = Filter(q, tasks.Select(t => (Key: (t.Title + " " + t.Description).ToLowerInvariant(), Display: $"\"{t.Title}\"")).ToList());
        var reminderHits = Filter(q, reminders.Select(r => (Key: (r.Title + " " + r.Description).ToLowerInvariant(), Display: $"\"{r.Title}\"")).ToList());

        if (noteHits.Count == 0 && taskHits.Count == 0 && reminderHits.Count == 0)
            return Localize($"No results found{(string.IsNullOrWhiteSpace(q) ? "." : $" for \"{q}\".")}", lang);

        var results = new List<string>();
        if (noteHits.Count > 0) results.Add($"Notes: {string.Join("; ", noteHits)}");
        if (taskHits.Count > 0) results.Add($"Tasks: {string.Join("; ", taskHits)}");
        if (reminderHits.Count > 0) results.Add($"Reminders: {string.Join("; ", reminderHits)}");
        return string.Join("\n", results);
    }

    private static List<string> Filter(string? q, List<(string Key, string Display)> items)
    {
        var matches = string.IsNullOrWhiteSpace(q)
            ? items.Take(3)
            : items.Where(i => i.Key.Contains(q.ToLowerInvariant())).Take(3);
        return matches.Select(i => i.Display).ToList();
    }

    // ----- helpers -----
    private async Task EnsureAllowedAsync(Guid userId, string entity, string errorCode)
    {
        var allowed = await _subscriptions.EnforceUsageLimitAsync(userId, entity);
        if (!allowed)
            throw new AppError($"You've reached your limit for {entity.ToLowerInvariant()}s. Please upgrade your plan.", 429, errorCode);
    }

    private static string TitleOr(IntentResult intent, string fallback)
        => !string.IsNullOrWhiteSpace(intent.Title) ? intent.Title : fallback;

    private static TimeZoneInfo Ist(string? timezone = null)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timezone) ? "Asia/Kolkata" : timezone); }
        catch { return TimeZoneInfo.Local; }
    }

    private static DateTime NowIst() => TimeZoneInfo.ConvertTime(DateTime.UtcNow, Ist());

    private static DateTime? ResolveDateTime(IntentResult intent)
        => DateTimeResolver.Resolve(intent.Date, intent.Time, NowIst());

    private static string CreatedResponse(string lang, string kind, string title, string? date, string? time)
    {
        var due = date is null ? "" : $" for {date}" + (time is null ? "" : $" at {time}");
        return Localize($"Done. I've created the {kind} \"{title}\"{due}.", lang);
    }

    private static string ConfirmedReminder(string lang, string title, DateTime when, string recurrence)
    {
        var rec = recurrence.Equals("Once", StringComparison.OrdinalIgnoreCase) ? "" : $" ({recurrence})";
        return Localize($"Done. Reminder \"{title}\" is set for {when:ddd, MMM d, yyyy h:mm tt}{rec}.", lang);
    }

    private static string Localize(string message, string language) => message;

    private static string HelpText(string language) => language switch
    {
        "hi-IN" => "मैं आपकी मदद कर सकता हूँ: नोट्स, कार्य, रिमाइंडर, अपॉइंटमेंट और दैनिक शेड्यूल।",
        "te-IN" => "నాకు సహాయం చేయగలను: గమనికలు, పనులు, రిమైండర్లు, అపాయింట్మెంట్లు మరియు రోజువారీ షెడ్యూల్.",
        _ => "I can help you with notes, tasks, reminders, appointments and your daily schedule."
    };
}