using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.API.Services;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ISubscriptionService _subscriptions;

    public AdminController(IUnitOfWork uow, ISubscriptionService subscriptions)
    {
        _uow = uow;
        _subscriptions = subscriptions;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var users = await _uow.Users.AllAsync();
        var plans = await _uow.Plans.AllAsync();
        var payments = await _uow.Payments.AllAsync();
        var subs = await _uow.Subscriptions.AllAsync();

        string PlanCodeFor(Guid? planId)
        {
            if (planId is null) return "FREE";
            var p = plans.FirstOrDefault(x => x.Id == planId.Value);
            return p?.Code ?? "FREE";
        }

        var subCounts = subs
            .Where(s => s.Status != SubscriptionStatus.Cancelled && s.Status != SubscriptionStatus.Expired)
            .GroupBy(s => PlanCodeFor(s.PlanId))
            .ToDictionary(g => g.Key, g => g.Count());

        var revenue = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
        var aiUsage = await _uow.UsageRecords.CountAsync(u => u.UsageType == "AI_Request");
        var voiceUsage = await _uow.UsageRecords.CountAsync(u => u.UsageType == "Voice_Request");
        var dbHealthy = await IsDatabaseHealthyAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            totalUsers = users.Count,
            activeUsers = users.Count(u => u.IsActive && !u.IsSuspended),
            freeUsers = users.Count - subCounts.GetValueOrDefault("PRO") - subCounts.GetValueOrDefault("PREMIUM"),
            proUsers = subCounts.GetValueOrDefault("PRO"),
            premiumUsers = subCounts.GetValueOrDefault("PREMIUM"),
            revenue,
            aiUsage,
            voiceUsage,
            systemHealth = dbHealthy ? "Healthy" : "Degraded",
            database = dbHealthy ? "OK" : "Degraded"
        }));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = (await _uow.Users.AllAsync()).Select(u => new
        {
            u.Id, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.IsSuspended, u.CreatedAt
        });
        return Ok(ApiResponse<object>.Ok(users));
    }

    [HttpPost("users/{id}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, bool suspended = true)
    {
        var user = await _uow.Users.GetByIdAsync(id) ?? throw new AppError("User not found", 404);
        user.IsSuspended = suspended;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { id, suspended }, suspended ? "User suspended" : "User restored"));
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(bool includeDisabled = true)
        => Ok(ApiResponse<object>.Ok(await _subscriptions.GetPlansAsync(includeDisabled)));

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(AdminPlanRequest request)
    {
        var plan = new Domain.Entities.Plan
        {
            Name = request.Name,
            Code = request.Code.ToUpperInvariant(),
            PriceMonthly = request.PriceMonthly ?? 0,
            PriceYearly = request.PriceYearly ?? 0,
            Currency = request.Currency ?? "INR",
            MaxNotes = request.MaxNotes ?? -1,
            MaxTasks = request.MaxTasks ?? -1,
            MaxRemindersPerMonth = request.MaxRemindersPerMonth ?? -1,
            MaxAppointments = request.MaxAppointments ?? -1,
            MaxAiRequestsPerMonth = request.MaxAiRequestsPerMonth ?? 20,
            MaxVoiceRequestsPerMonth = request.MaxVoiceRequestsPerMonth ?? 20,
            AllowsVoice = request.AllowsVoice ?? true,
            AllowsCalendar = request.AllowsCalendar ?? true,
            AllowsCloudBackup = request.AllowsCloudBackup ?? false,
            AllowsCalendarIntegrations = request.AllowsCalendarIntegrations ?? false,
            AllowsAdvancedAi = request.AllowsAdvancedAi ?? false,
            Features = request.Features ?? new List<string>(),
            IsEnabled = request.IsEnabled ?? true,
            DisplayOrder = request.DisplayOrder ?? 0
        };
        await _uow.Plans.AddAsync(plan);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(plan, "Plan created"));
    }

    [HttpPut("plans/{id}")]
    public async Task<IActionResult> UpdatePlan(Guid id, AdminPlanRequest request)
    {
        var plan = await _uow.Plans.GetByIdAsync(id) ?? throw new AppError("Plan not found", 404);
        if (request.Name is not null) plan.Name = request.Name;
        if (request.PriceMonthly is not null) plan.PriceMonthly = request.PriceMonthly.Value;
        if (request.PriceYearly is not null) plan.PriceYearly = request.PriceYearly.Value;
        if (request.IsEnabled is not null) plan.IsEnabled = request.IsEnabled.Value;
        if (request.Features is not null) plan.Features = request.Features;
        if (request.MaxAiRequestsPerMonth is not null) plan.MaxAiRequestsPerMonth = request.MaxAiRequestsPerMonth.Value;
        plan.UpdatedAt = DateTime.UtcNow;
        _uow.Plans.Update(plan);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(plan, "Plan updated"));
    }

    private async Task<bool> IsDatabaseHealthyAsync()
    {
        try
        {
            await _uow.Users.CountAsync(u => u.Id != Guid.Empty);
            return true;
        }
        catch { return false; }
    }
}

public class AdminPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal? PriceMonthly { get; set; }
    public decimal? PriceYearly { get; set; }
    public string? Currency { get; set; }
    public int? MaxNotes { get; set; }
    public int? MaxTasks { get; set; }
    public int? MaxRemindersPerMonth { get; set; }
    public int? MaxAppointments { get; set; }
    public int? MaxAiRequestsPerMonth { get; set; }
    public int? MaxVoiceRequestsPerMonth { get; set; }
    public bool? AllowsVoice { get; set; }
    public bool? AllowsCalendar { get; set; }
    public bool? AllowsCloudBackup { get; set; }
    public bool? AllowsCalendarIntegrations { get; set; }
    public bool? AllowsAdvancedAi { get; set; }
    public List<string>? Features { get; set; }
    public bool? IsEnabled { get; set; }
    public int? DisplayOrder { get; set; }
}