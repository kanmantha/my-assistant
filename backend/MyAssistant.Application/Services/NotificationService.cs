using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;

    public NotificationService(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task CreateAsync(Guid userId, string title, string? body, string? type, Guid? relatedEntityId, DateTime? scheduledAt = null, CancellationToken cancellationToken = default)
    {
        await _notifications.AddAsync(new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            RelatedEntityId = relatedEntityId,
            ScheduledAt = scheduledAt ?? DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetDueAsync(CancellationToken cancellationToken = default)
    {
        return await _notifications.GetDueAsync(DateTime.UtcNow, cancellationToken);
    }

    public async Task MarkSentAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.IsSent = true;
        notification.SentAt = DateTime.UtcNow;
        await _notifications.UpdateAsync(notification, cancellationToken);
    }

    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || notification.UserId != userId) return;
        notification.IsRead = true;
        await _notifications.UpdateAsync(notification, cancellationToken);
    }
}
