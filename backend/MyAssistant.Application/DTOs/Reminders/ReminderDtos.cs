using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.DTOs.Reminders;

public class ReminderDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ReminderAt { get; set; }
    public RecurrenceType Recurrence { get; set; }
    public string? RecurrenceRule { get; set; }
    public TaskPriority Priority { get; set; }
    public bool IsFired { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateReminderRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ReminderAt { get; set; }
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.Once;
    public string? RecurrenceRule { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
}

public class UpdateReminderRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ReminderAt { get; set; }
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.Once;
    public string? RecurrenceRule { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public bool IsAcknowledged { get; set; }
}
