namespace MyAssistant.Application.AI;

public class AssistantRequest
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "en-IN";
    public string Timezone { get; set; } = "Asia/Kolkata";
    public Guid UserId { get; set; }
    public List<ConversationTurn>? Context { get; set; }
    public string? Provider { get; set; }
    public string? VoiceInput { get; set; }
}

public record ConversationTurn(string Role, string Content);

public class IntentResult
{
    public string Intent { get; set; } = string.Empty;
    public string Language { get; set; } = "en-IN";
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Description { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public string? EndDateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? ReminderMinutes { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public string? Recurrence { get; set; }
    public string? Location { get; set; }
    public List<string>? Participants { get; set; }
    public List<string>? Tags { get; set; }
    public string? Query { get; set; }
    public string? TargetLanguage { get; set; }
    public string? Id { get; set; }
    public string? NewTitle { get; set; }
    public Guid? EntityId { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
    public string? RawJson { get; set; }
    public bool NeedsClarification { get; set; }
    public string? ClarificationQuestion { get; set; }
    public string? OriginalText { get; set; }
}

public class AssistantResult
{
    public bool Success { get; set; }
    public string? Intent { get; set; }
    public string? ResponseText { get; set; }
    public string? ResponseLanguage { get; set; }
    public IntentResult? IntentData { get; set; }
    public string? TtsAudioBase64 { get; set; }
    public bool NeedsClarification { get; set; }
    public string? ClarificationQuestion { get; set; }
    public Guid? EntityId { get; set; }
    public string? Error { get; set; }
    public int? UsageAiRequests { get; set; }
    public int? UsageAiLimit { get; set; }
}

public class SpeechRecognitionResult
{
    public string? Text { get; set; }
    public double Confidence { get; set; }
    public string? DetectedLanguage { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class TtsResult
{
    public string? AudioBase64 { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public static class AssistantIntents
{
    public const string CreateNote = "CreateNote";
    public const string UpdateNote = "UpdateNote";
    public const string DeleteNote = "DeleteNote";
    public const string SearchNotes = "SearchNotes";

    public const string CreateTask = "CreateTask";
    public const string UpdateTask = "UpdateTask";
    public const string CompleteTask = "CompleteTask";
    public const string DeleteTask = "DeleteTask";
    public const string ListTasks = "ListTasks";

    public const string CreateReminder = "CreateReminder";
    public const string UpdateReminder = "UpdateReminder";
    public const string DeleteReminder = "DeleteReminder";
    public const string ListReminders = "ListReminders";

    public const string CreateAppointment = "CreateAppointment";
    public const string UpdateAppointment = "UpdateAppointment";
    public const string DeleteAppointment = "DeleteAppointment";
    public const string ListAppointments = "ListAppointments";

    public const string GetTodaySchedule = "GetTodaySchedule";
    public const string GetTomorrowSchedule = "GetTomorrowSchedule";
    public const string GetUpcomingSchedule = "GetUpcomingSchedule";

    public const string Search = "Search";
    public const string Confirm = "Confirm";
    public const string Cancel = "Cancel";
    public const string Help = "Help";
    public const string ChangeLanguage = "ChangeLanguage";

    public static readonly string[] All = new[]
    {
        CreateNote, UpdateNote, DeleteNote, SearchNotes,
        CreateTask, UpdateTask, CompleteTask, DeleteTask, ListTasks,
        CreateReminder, UpdateReminder, DeleteReminder, ListReminders,
        CreateAppointment, UpdateAppointment, DeleteAppointment, ListAppointments,
        GetTodaySchedule, GetTomorrowSchedule, GetUpcomingSchedule,
        Search, Confirm, Cancel, Help, ChangeLanguage
    };
}