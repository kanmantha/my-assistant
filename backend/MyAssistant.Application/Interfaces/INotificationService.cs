using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public interface INotificationService
{
    Task CreateAsync(Guid userId, string title, string? body, string? type, Guid? relatedEntityId, DateTime? scheduledAt = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetDueAsync(CancellationToken cancellationToken = default);
    Task MarkSentAsync(Notification notification, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
}
