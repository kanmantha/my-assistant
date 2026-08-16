using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.API.Services;

namespace MyAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class BillingController : ControllerBase
{
    private readonly ISubscriptionService _subscriptions;
    private readonly IPaymentService _payments;

    public BillingController(ISubscriptionService subscriptions, IPaymentService payments)
    {
        _subscriptions = subscriptions;
        _payments = payments;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans()
        => Ok(ApiResponse<object>.Ok(await _subscriptions.GetPlansAsync()));

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
        => Ok(ApiResponse<object>.Ok(await _subscriptions.GetSubscriptionAsync(this.GetUserId())));

    [HttpPost("subscription/upgrade")]
    public async Task<IActionResult> Upgrade(SubscriptionRequest body)
        => Ok(ApiResponse<object>.Ok(await _subscriptions.UpgradeAsync(this.GetUserId(), body.PlanId, body.BillingPeriod ?? "monthly"), "Subscription upgraded"));

    [HttpPost("subscription/downgrade")]
    public async Task<IActionResult> Downgrade(SubscriptionRequest body)
        => Ok(ApiResponse<object>.Ok(await _subscriptions.DowngradeAsync(this.GetUserId(), body.PlanId), "Subscription downgraded"));

    [HttpPost("subscription/cancel")]
    public async Task<IActionResult> Cancel()
        => Ok(ApiResponse<object>.Ok(await _subscriptions.CancelAsync(this.GetUserId()), "Subscription cancelled"));

    [HttpPost("subscription/verify")]
    public async Task<IActionResult> Verify(VerifyPaymentRequest body)
    {
        var result = await _payments.VerifyAsync(this.GetUserId(), body.ProviderReference, body.Receipt ?? string.Empty);
        if (!result.Success) return BadRequest(ApiResponse<object>.Fail(result.Message, result.ErrorCode));
        return Ok(ApiResponse<object>.Ok(new { result.ProviderReference, message = result.Message }));
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage()
        => Ok(ApiResponse<object>.Ok(await _subscriptions.GetUsageAsync(this.GetUserId())));
}

public class SubscriptionRequest
{
    public Guid PlanId { get; set; }
    public string? BillingPeriod { get; set; }
}

public class VerifyPaymentRequest
{
    public string ProviderReference { get; set; } = string.Empty;
    public string? Receipt { get; set; }
}