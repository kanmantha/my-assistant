using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.AI;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Infrastructure.Repositories;

namespace MyAssistant.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(IUnitOfWork uow, IConfiguration config, ILogger<SubscriptionService> logger)
    {
        _uow = uow;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Plan>> GetPlansAsync(bool includeDisabled = false)
    {
        var plans = await _uow.Plans.ToListAsync(p => includeDisabled || p.IsEnabled, orderBy: p => p.DisplayOrder, descending: false);
        return plans;
    }

    public async Task<SubscriptionInfo> GetSubscriptionAsync(Guid userId)
    {
        var sub = await _uow.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        Plan? plan = null;
        if (sub is not null)
            plan = await _uow.Plans.GetByIdAsync(sub.PlanId);
        plan ??= await _uow.Plans.FirstOrDefaultAsync(p => p.Code == "FREE");

        var usage = await GetUsageAsync(userId);

        return new SubscriptionInfo(
            PlanCode: plan!.Code,
            PlanName: plan.Name,
            Status: sub?.Status.ToString() ?? "Active",
            BillingPeriod: sub?.BillingPeriod.ToString() ?? "Monthly",
            RenewalDate: sub?.RenewalDate,
            CancelAt: sub?.CancelAt,
            Provider: sub?.Provider,
            Price: sub is null ? 0 : (sub.BillingPeriod == BillingPeriod.Yearly ? plan.PriceYearly : plan.PriceMonthly),
            Currency: plan.Currency,
            Usage: usage);
    }

    public async Task<SubscriptionInfo> UpgradeAsync(Guid userId, Guid planId, string billingPeriod, string provider = "mock")
    {
        var plan = await _uow.Plans.GetByIdAsync(planId) ?? throw new AppError("Plan not found", 404, "PLAN_NOT_FOUND");
        var sub = await _uow.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        var now = DateTime.UtcNow;

        if (sub is null)
        {
            sub = new Subscription
            {
                UserId = userId,
                PlanId = planId,
                Status = SubscriptionStatus.Active,
                BillingPeriod = billingPeriod.Equals("yearly", StringComparison.OrdinalIgnoreCase) ? BillingPeriod.Yearly : BillingPeriod.Monthly,
                StartDate = now,
                CurrentPeriodEnd = now.AddMonths(1),
                RenewalDate = now.AddMonths(1),
                Provider = provider
            };
            await _uow.Subscriptions.AddAsync(sub);
        }
        else
        {
            sub.PlanId = planId;
            sub.Status = SubscriptionStatus.Active;
            sub.BillingPeriod = billingPeriod.Equals("yearly", StringComparison.OrdinalIgnoreCase) ? BillingPeriod.Yearly : BillingPeriod.Monthly;
            sub.UpdatedAt = now;
            sub.CancelAt = null;
            sub.CancelledAt = null;
            _uow.Subscriptions.Update(sub);
        }

        await _uow.SaveChangesAsync();
        _logger.LogInformation("User {UserId} upgraded to plan {PlanCode}", userId, plan.Code);
        return await GetSubscriptionAsync(userId);
    }

    public async Task<SubscriptionInfo> DowngradeAsync(Guid userId, Guid planId)
        => await UpgradeAsync(userId, planId, "monthly");

    public async Task<SubscriptionInfo> CancelAsync(Guid userId)
    {
        var sub = await _uow.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        if (sub is null) throw new AppError("No active subscription", 404, "NO_SUBSCRIPTION");

        sub.Status = SubscriptionStatus.Cancelled;
        sub.CancelAt = DateTime.UtcNow;
        sub.CancelledAt = DateTime.UtcNow;
        sub.UpdatedAt = DateTime.UtcNow;
        _uow.Subscriptions.Update(sub);
        await _uow.SaveChangesAsync();
        return await GetSubscriptionAsync(userId);
    }

    public async Task<UsageInfo> GetUsageAsync(Guid userId)
    {
        var sub = await _uow.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        var plan = sub is not null ? await _uow.Plans.GetByIdAsync(sub.PlanId) : await _uow.Plans.FirstOrDefaultAsync(p => p.Code == "FREE");
        plan ??= new Plan { Code = "FREE", MaxAiRequestsPerMonth = 20, MaxVoiceRequestsPerMonth = 20, MaxNotes = 50, MaxTasks = 50, MaxRemindersPerMonth = 20, MaxAppointments = 20 };

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var ai = await _uow.UsageRecords.CountAsync(u => u.UserId == userId && u.UsageType == "AI_Request" && u.OccurredAt >= monthStart);
        var voice = await _uow.UsageRecords.CountAsync(u => u.UserId == userId && u.UsageType == "Voice_Request" && u.OccurredAt >= monthStart);
        var notes = await _uow.Notes.CountAsync(n => n.UserId == userId);
        var tasks = await _uow.Tasks.CountAsync(t => t.UserId == userId);
        var reminders = await _uow.Reminders.CountAsync(r => r.UserId == userId && r.CreatedAt >= monthStart);
        var appointments = await _uow.Appointments.CountAsync(a => a.UserId == userId);

        return new UsageInfo(
            AiRequests: ai,
            AiLimit: plan.MaxAiRequestsPerMonth < 0 ? int.MaxValue : plan.MaxAiRequestsPerMonth,
            VoiceRequests: voice,
            VoiceLimit: plan.MaxVoiceRequestsPerMonth < 0 ? int.MaxValue : plan.MaxVoiceRequestsPerMonth,
            Notes: notes,
            Tasks: tasks,
            Reminders: reminders,
            Appointments: appointments,
            PlanCode: plan.Code);
    }

    public async Task<bool> EnforceUsageLimitAsync(Guid userId, string usageType, int quantity = 1)
    {
        var usage = await GetUsageAsync(userId);
        return usageType switch
        {
            "AI_Request" => usage.AiRequests + quantity <= usage.AiLimit,
            "Voice_Request" => usage.VoiceRequests + quantity <= usage.VoiceLimit,
            "Note" => usage.Notes + quantity <= (await GetLimitForAsync(userId, "Note")),
            "Task" => usage.Tasks + quantity <= (await GetLimitForAsync(userId, "Task")),
            "Reminder" => usage.Reminders + quantity <= (await GetLimitForAsync(userId, "Reminder")),
            "Appointment" => usage.Appointments + quantity <= (await GetLimitForAsync(userId, "Appointment")),
            _ => true
        };
    }

    public async Task RecordUsageAsync(Guid userId, string usageType, int quantity = 1, string? meta = null)
    {
        await _uow.UsageRecords.AddAsync(new UsageRecord { UserId = userId, UsageType = usageType, Quantity = quantity, Meta = meta });
        await _uow.SaveChangesAsync();
    }

    public async Task<int> GetUsageCountAsync(Guid userId, string usageType, DateTime sinceUtc)
        => await _uow.UsageRecords.CountAsync(u => u.UserId == userId && u.UsageType == usageType && u.OccurredAt >= sinceUtc);

    private async Task<int> GetLimitForAsync(Guid userId, string type)
    {
        var sub = await _uow.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        var plan = sub is not null ? await _uow.Plans.GetByIdAsync(sub.PlanId) : await _uow.Plans.FirstOrDefaultAsync(p => p.Code == "FREE");
        plan ??= new Plan { Code = "FREE", MaxNotes = 50, MaxTasks = 50, MaxRemindersPerMonth = 20, MaxAppointments = 20 };

        int value = type switch
        {
            "Note" => plan.MaxNotes,
            "Task" => plan.MaxTasks,
            "Reminder" => plan.MaxRemindersPerMonth,
            "Appointment" => plan.MaxAppointments,
            _ => -1
        };
        return value < 0 ? int.MaxValue : value;
    }
}