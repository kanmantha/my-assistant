using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.DTOs.Assistant;

public class ParsedCommand
{
    public AssistantIntent Intent { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Description { get; set; }
    public DateTime? Date { get; set; }
    public TimeOnly? Time { get; set; }
    public DateTime? EndDateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public List<string>? Participants { get; set; }
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.Once;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string? Language { get; set; }
    public string? SearchQuery { get; set; }
    public string? Category { get; set; }
    public bool DateSkipped { get; set; }
    public bool CategorySkipped { get; set; }
    public List<string>? Tags { get; set; }
    public bool NeedsConfirmation { get; set; }
    public string? ConfirmationPrompt { get; set; }
    public Guid? TargetId { get; set; }
    public string? PendingAction { get; set; }
    public string? Scope { get; set; }
}
