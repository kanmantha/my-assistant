using MyAssistant.Domain.Common;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Domain.Entities;

public class TaskItem : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateOnly? DueDate { get; set; }
    public TimeOnly? DueTime { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Category { get; set; }
}
