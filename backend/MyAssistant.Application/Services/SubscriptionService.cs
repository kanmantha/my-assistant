using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IUsageRepository _usage;

    private const int FreeNoteLimit = 50;
    private const int FreeTaskLimit = 50;
    private const int FreeReminderMonthlyLimit = 20;

    public SubscriptionService(ISubscriptionRepository subscriptions, IUsageRepository usage)
    {
        _subscriptions = subscriptions;
        _usage = usage;
    }

    public async Task<Subscription> GetActiveSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _subscriptions.GetActiveByUserAsync(userId, cancellationToken)
               ?? new Subscription { UserId = userId, Tier = SubscriptionTier.Free, Status = SubscriptionStatus.Active };
    }

    public async Task<bool> CanUseFeatureAsync(Guid userId, UsageType type, CancellationToken cancellationToken = default)
    {
        var subscription = await GetActiveSubscriptionAsync(userId, cancellationToken);
        if (subscription.Tier != SubscriptionTier.Free) return true;

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        switch (type)
        {
            case UsageType.Note:
                var noteCount = await _usage.CountSinceAsync(userId, UsageType.Note, monthStart, cancellationToken);
                if (noteCount >= FreeNoteLimit) return false;
                break;
            case UsageType.Task:
                var taskCount = await _usage.CountSinceAsync(userId, UsageType.Task, monthStart, cancellationToken);
                if (taskCount >= FreeTaskLimit) return false;
                break;
            case UsageType.Reminder:
                var reminderCount = await _usage.CountSinceAsync(userId, UsageType.Reminder, monthStart, cancellationToken);
                if (reminderCount >= FreeReminderMonthlyLimit) return false;
                break;
        }

        return true;
    }

    public async Task RecordUsageAsync(Guid userId, UsageType type, string? metadata = null, CancellationToken cancellationToken = default)
    {
        await _usage.AddAsync(new UsageRecord
        {
            UserId = userId,
            Type = type,
            Count = 1,
            OccurredAt = DateTime.UtcNow,
            Metadata = metadata
        }, cancellationToken);
    }
}
