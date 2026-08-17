using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(INotificationRepository notifications, INotificationService notificationService, ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct, [FromQuery] int take = 50)
    {
        var result = await _notifications.GetForUserAsync(_currentUser.UserId!.Value, take, ct);
        return Ok(ApiResponse<IReadOnlyList<Notification>>.Ok(result));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var count = await _notifications.CountUnreadForUserAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _notificationService.MarkReadAsync(id, _currentUser.UserId!.Value, ct);
        return Ok(ApiResponse.Ok("Notification marked as read."));
    }
}
