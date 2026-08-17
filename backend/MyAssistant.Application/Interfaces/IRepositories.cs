using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public interface INoteRepository : IRepository<Note>
{
    Task<IReadOnlyList<Note>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Note?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Note>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default);
    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<IReadOnlyList<TaskItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetForUserByStatusAsync(Guid userId, Domain.Enums.TaskStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetDueOnDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default);
    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IReminderRepository : IRepository<Reminder>
{
    Task<IReadOnlyList<Reminder>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Reminder?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reminder>> GetUpcomingForUserAsync(Guid userId, DateTime fromUtc, int take = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reminder>> GetOnDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reminder>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default);
    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Reminder>> GetDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<IReadOnlyList<Appointment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Appointment?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Appointment>> GetOnDateAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Appointment>> GetUpcomingForUserAsync(Guid userId, DateTime fromUtc, int take = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Appointment>> GetInRangeAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Appointment>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default);
    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IConversationRepository : IRepository<ConversationHistory>
{
    Task<IReadOnlyList<ConversationHistory>> GetForUserAsync(Guid userId, int take = 100, CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface INotificationRepository : IRepository<Notification>
{
    Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int take = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    Task<int> CountUnreadForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUserSettingsRepository : IRepository<UserSettings>
{
    Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserSettings> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Task<Subscription?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUsageRepository : IRepository<UsageRecord>
{
    Task<int> CountSinceAsync(Guid userId, Domain.Enums.UsageType type, DateTime sinceUtc, CancellationToken cancellationToken = default);
}
