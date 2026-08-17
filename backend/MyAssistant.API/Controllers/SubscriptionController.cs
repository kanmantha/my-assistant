using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Domain.Enums;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IUsageRepository _usage;
    private readonly ICurrentUserService _currentUser;

    public SubscriptionController(ISubscriptionService subscriptionService, IUsageRepository usage, ICurrentUserService currentUser)
    {
        _subscriptionService = subscriptionService;
        _usage = usage;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId, ct);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var notes = await _usage.CountSinceAsync(userId, UsageType.Note, monthStart, ct);
        var tasks = await _usage.CountSinceAsync(userId, UsageType.Task, monthStart, ct);
        var reminders = await _usage.CountSinceAsync(userId, UsageType.Reminder, monthStart, ct);
        var appointments = await _usage.CountSinceAsync(userId, UsageType.Appointment, monthStart, ct);
        var aiCommands = await _usage.CountSinceAsync(userId, UsageType.AiCommand, monthStart, ct);
        var speechToText = await _usage.CountSinceAsync(userId, UsageType.SpeechToText, monthStart, ct);
        var textToSpeech = await _usage.CountSinceAsync(userId, UsageType.TextToSpeech, monthStart, ct);
        var searches = await _usage.CountSinceAsync(userId, UsageType.Search, monthStart, ct);

        var limits = subscription.Tier == SubscriptionTier.Free
            ? new { Notes = 50, Tasks = 50, Reminders = 20, Appointments = -1 }
            : new { Notes = -1, Tasks = -1, Reminders = -1, Appointments = -1 };

        var result = new
        {
            tier = subscription.Tier.ToString(),
            status = subscription.Status.ToString(),
            startedAt = subscription.StartedAt,
            renewalAt = subscription.RenewalAt,
            usage = new { notes, tasks, reminders, appointments, aiCommands, speechToText, textToSpeech, searches },
            limits
        };
        return Ok(ApiResponse<object>.Ok(result));
    }
}
