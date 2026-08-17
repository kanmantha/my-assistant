using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public interface ISubscriptionService
{
    Task<Subscription> GetActiveSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanUseFeatureAsync(Guid userId, Domain.Enums.UsageType type, CancellationToken cancellationToken = default);
    Task RecordUsageAsync(Guid userId, Domain.Enums.UsageType type, string? metadata = null, CancellationToken cancellationToken = default);
}
