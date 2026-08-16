using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Infrastructure.Data;

namespace MyAssistant.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Db;

    public Repository(AppDbContext db) => Db = db;

    public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default) => await Db.Set<T>().FindAsync(new[] { id }, ct);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await Db.Set<T>().FirstOrDefaultAsync(predicate, ct);

    public async Task<List<T>> AllAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null ? await Db.Set<T>().ToListAsync(ct) : await Db.Set<T>().Where(predicate).ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ToListAsync(
        Expression<Func<T, bool>>? predicate = null,
        int? skip = null,
        int? take = null,
        Expression<Func<T, object>>? orderBy = null,
        bool descending = true,
        CancellationToken ct = default)
    {
        IQueryable<T> q = Db.Set<T>();
        if (predicate is not null) q = q.Where(predicate);
        if (orderBy is not null)
            q = descending ? q.OrderByDescending(orderBy) : q.OrderBy(orderBy);
        else if (typeof(T).GetProperty("CreatedAt") is not null)
            q = descending ? q.OrderByDescending(t => EF.Property<DateTime>(t, "CreatedAt")) : q.OrderBy(t => EF.Property<DateTime>(t, "CreatedAt"));

        if (skip.HasValue) q = q.Skip(skip.Value);
        if (take.HasValue) q = q.Take(take.Value);
        return await q.ToListAsync(ct);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null ? await Db.Set<T>().CountAsync(ct) : await Db.Set<T>().CountAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Db.Set<T>().AddAsync(entity, ct);

    public void Update(T entity) => Db.Set<T>().Update(entity);

    public void Remove(T entity) => Db.Set<T>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db)
    {
        _db = db;
        Notes = new Repository<Note>(db);
        Tasks = new Repository<TaskItem>(db);
        Reminders = new Repository<Reminder>(db);
        Appointments = new Repository<Appointment>(db);
        Users = new Repository<User>(db);
        Plans = new Repository<Plan>(db);
        Subscriptions = new Repository<Subscription>(db);
        Payments = new Repository<Payment>(db);
        UsageRecords = new Repository<UsageRecord>(db);
        RefreshTokens = new Repository<RefreshToken>(db);
        UserSettings = new Repository<UserSettings>(db);
    }

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

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}