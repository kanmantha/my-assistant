using MyAssistant.Domain.Common;

namespace MyAssistant.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Type { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
}
