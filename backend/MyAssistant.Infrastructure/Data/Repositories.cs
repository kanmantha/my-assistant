using Microsoft.EntityFrameworkCore;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Infrastructure.Data;

public class NoteRepository : Repository<Note>, INoteRepository
{
    public NoteRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<Note>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Note>>(DbSet.AsNoTracking().Where(n => n.UserId == userId).ToList());

    public Task<Note?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(n => n.UserId == userId && n.Id == id, cancellationToken);

    public Task<IReadOnlyList<Note>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        return Task.FromResult<IReadOnlyList<Note>>(DbSet.AsNoTracking()
            .Where(n => n.UserId == userId &&
                (n.Title.ToLower().Contains(q) || n.Content.ToLower().Contains(q)))
            .ToList());
    }

    public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(n => n.UserId == userId, cancellationToken);
}

public class TaskRepository : Repository<TaskItem>, ITaskRepository
{
    public TaskRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<TaskItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(DbSet.AsNoTracking().Where(t => t.UserId == userId).ToList());

    public Task<TaskItem?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, cancellationToken);

    public Task<IReadOnlyList<TaskItem>> GetForUserByStatusAsync(Guid userId, Domain.Enums.TaskStatus status, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(DbSet.AsNoTracking().Where(t => t.UserId == userId && t.Status == status).ToList());

    public Task<IReadOnlyList<TaskItem>> GetDueOnDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(DbSet.AsNoTracking()
            .Where(t => t.UserId == userId && t.DueDate == date).ToList());

    public Task<IReadOnlyList<TaskItem>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        return Task.FromResult<IReadOnlyList<TaskItem>>(DbSet.AsNoTracking()
            .Where(t => t.UserId == userId && t.Title.ToLower().Contains(q)).ToList());
    }

    public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(t => t.UserId == userId, cancellationToken);
}

public class ReminderRepository : Repository<Reminder>, IReminderRepository
{
    public ReminderRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<Reminder>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Reminder>>(DbSet.AsNoTracking().Where(r => r.UserId == userId).ToList());

    public Task<Reminder?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(r => r.UserId == userId && r.Id == id, cancellationToken);

    public Task<IReadOnlyList<Reminder>> GetUpcomingForUserAsync(Guid userId, DateTime fromUtc, int take = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Reminder>>(DbSet.AsNoTracking()
            .Where(r => r.UserId == userId && r.ReminderAt >= fromUtc && !r.IsFired)
            .OrderBy(r => r.ReminderAt).Take(take).ToList());

    public Task<IReadOnlyList<Reminder>> GetOnDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Reminder>>(DbSet.AsNoTracking()
            .Where(r => r.UserId == userId &&
                r.ReminderAt.Year == date.Year && r.ReminderAt.Month == date.Month && r.ReminderAt.Day == date.Day)
            .ToList());

    public Task<IReadOnlyList<Reminder>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        return Task.FromResult<IReadOnlyList<Reminder>>(DbSet.AsNoTracking()
            .Where(r => r.UserId == userId && (r.Title.ToLower().Contains(q) || (r.Message != null && r.Message.ToLower().Contains(q))))
            .ToList());
    }

    public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(r => r.UserId == userId, cancellationToken);

    public Task<IReadOnlyList<Reminder>> GetDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Reminder>>(DbSet.AsNoTracking()
            .Where(r => r.ReminderAt <= utcNow && !r.IsFired).ToList());
}

public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
{
    public AppointmentRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<Appointment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Appointment>>(DbSet.AsNoTracking().Where(a => a.UserId == userId).ToList());

    public Task<Appointment?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(a => a.UserId == userId && a.Id == id, cancellationToken);

    public Task<IReadOnlyList<Appointment>> GetOnDateAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Appointment>>(DbSet.AsNoTracking()
            .Where(a => a.UserId == userId && a.StartDateTime >= startUtc && a.StartDateTime < endUtc).ToList());

    public Task<IReadOnlyList<Appointment>> GetUpcomingForUserAsync(Guid userId, DateTime fromUtc, int take = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Appointment>>(DbSet.AsNoTracking()
            .Where(a => a.UserId == userId && a.StartDateTime >= fromUtc)
            .OrderBy(a => a.StartDateTime).Take(take).ToList());

    public Task<IReadOnlyList<Appointment>> GetInRangeAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Appointment>>(DbSet.AsNoTracking()
            .Where(a => a.UserId == userId && a.StartDateTime >= startUtc && a.StartDateTime < endUtc).ToList());

    public Task<IReadOnlyList<Appointment>> SearchForUserAsync(Guid userId, string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        return Task.FromResult<IReadOnlyList<Appointment>>(DbSet.AsNoTracking()
            .Where(a => a.UserId == userId && a.Title.ToLower().Contains(q)).ToList());
    }

    public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(a => a.UserId == userId, cancellationToken);
}

public class ConversationRepository : Repository<ConversationHistory>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<ConversationHistory>> GetForUserAsync(Guid userId, int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ConversationHistory>>(DbSet.AsNoTracking()
            .Where(c => c.UserId == userId).OrderByDescending(c => c.CreatedAt).Take(take).ToList());

    public async Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await DbSet.Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        DbSet.RemoveRange(items);
        await Context.SaveChangesAsync(cancellationToken);
    }
}

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int take = 50, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Notification>>(DbSet.AsNoTracking()
            .Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(take).ToList());

    public Task<IReadOnlyList<Notification>> GetDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Notification>>(DbSet.AsNoTracking()
            .Where(n => !n.IsSent && n.ScheduledAt != null && n.ScheduledAt <= utcNow).ToList());

    public Task<int> CountUnreadForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await DbSet.Where(n => n.UserId == userId).ToListAsync(cancellationToken);
        DbSet.RemoveRange(items);
        await Context.SaveChangesAsync(cancellationToken);
    }
}

public class UserSettingsRepository : Repository<UserSettings>, IUserSettingsRepository
{
    public UserSettingsRepository(AppDbContext context) : base(context) { }

    public Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public async Task<UserSettings> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existing = await GetByUserIdAsync(userId, cancellationToken);
        if (existing != null) return existing;

        var settings = new UserSettings { UserId = userId, WakeWordEnabled = true };
        await AddAsync(settings, cancellationToken);
        return settings;
    }
}

public class SubscriptionRepository : Repository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(AppDbContext context) : base(context) { }

    public Task<Subscription?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(s => s.UserId == userId && s.Status == Domain.Enums.SubscriptionStatus.Active, cancellationToken);
}

public class UsageRepository : Repository<UsageRecord>, IUsageRepository
{
    public UsageRepository(AppDbContext context) : base(context) { }

    public Task<int> CountSinceAsync(Guid userId, Domain.Enums.UsageType type, DateTime sinceUtc, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(u => u.UserId == userId && u.Type == type && u.OccurredAt >= sinceUtc, cancellationToken);
}
