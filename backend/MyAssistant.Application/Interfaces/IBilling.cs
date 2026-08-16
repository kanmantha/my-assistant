using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentResult> CreateCheckoutAsync(Guid userId, Guid planId, string billingPeriod, string platform);
    Task<PaymentResult> VerifyAsync(Guid userId, string providerReference, string receipt);
    Task<PaymentResult> RestoreAsync(Guid userId, string platform);
}

public interface ISubscriptionService
{
    Task<SubscriptionInfo> GetSubscriptionAsync(Guid userId);
    Task<SubscriptionInfo> UpgradeAsync(Guid userId, Guid planId, string billingPeriod);
    Task<SubscriptionInfo> DowngradeAsync(Guid userId, Guid planId);
    Task<SubscriptionInfo> CancelAsync(Guid userId);
    Task<IReadOnlyList<Plan>> GetPlansAsync(bool includeDisabled = false);
    Task<UsageInfo> GetUsageAsync(Guid userId);
    Task<bool> EnforceUsageLimitAsync(Guid userId, string usageType, int quantity = 1);
    Task RecordUsageAsync(Guid userId, string usageType, int quantity = 1, string? meta = null);
    Task<int> GetUsageCountAsync(Guid userId, string usageType, DateTime sinceUtc);
}

public record PaymentResult(bool Success, string Message, string? ProviderReference, string? ClientToken, string? ErrorCode);
public record UsageInfo(int AiRequests, int AiLimit, int VoiceRequests, int VoiceLimit, int Notes, int Tasks, int Reminders, int Appointments, string PlanCode);
public record SubscriptionInfo(
    string PlanCode,
    string PlanName,
    string Status,
    string BillingPeriod,
    DateTime? RenewalDate,
    DateTime? CancelAt,
    string? Provider,
    decimal Price,
    string Currency,
    UsageInfo Usage);