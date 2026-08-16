using System.Linq.Expressions;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<List<T>> AllAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ToListAsync(Expression<Func<T, bool>>? predicate = null, int? skip = null, int? take = null, Expression<Func<T, object>>? orderBy = null, bool descending = true, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IUnitOfWork
{
    public IRepository<Note> Notes { get; }
    public IRepository<TaskItem> Tasks { get; }
    public IRepository<Reminder> Reminders { get; }
    public IRepository<Appointment> Appointments { get; }
    public IRepository<User> Users { get; }
    public IRepository<Plan> Plans { get; }
    public IRepository<Subscription> Subscriptions { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<UsageRecord> UsageRecords { get; }
    public IRepository<RefreshToken> RefreshTokens { get; }
    public IRepository<UserSettings> UserSettings { get; }
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}