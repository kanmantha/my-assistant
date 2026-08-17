using MyAssistant.Domain.Common;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Domain.Entities;

public class Subscription : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RenewalAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Provider { get; set; }
    public string? ProviderPlanId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
}
