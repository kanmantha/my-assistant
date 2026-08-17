using MyAssistant.Domain.Common;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Domain.Entities;

public class UsageRecord : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public UsageType Type { get; set; }
    public int Count { get; set; } = 1;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; }
}
