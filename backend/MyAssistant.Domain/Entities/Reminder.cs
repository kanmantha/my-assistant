using MyAssistant.Domain.Common;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Domain.Entities;

public class Reminder : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ReminderAt { get; set; }
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.Once;
    public string? RecurrenceRule { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public bool IsFired { get; set; }
    public DateTime? FiredAt { get; set; }
    public bool IsAcknowledged { get; set; }
}
