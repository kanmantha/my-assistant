using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ConversationHistory> Conversations => Set<ConversationHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.HasOne(u => u.Settings)
                .WithOne(s => s.User)
                .HasForeignKey<UserSettings>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(u => u.Organization)
                .WithMany(o => o.Users)
                .HasForeignKey(u => u.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Note>(e =>
        {
            e.HasOne(n => n.User).WithMany(u => u.Notes).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.UserId, n.Title });
        });

        builder.Entity<TaskItem>(e =>
        {
            e.HasOne(t => t.User).WithMany(u => u.Tasks).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.UserId, t.Status });
            e.HasIndex(t => new { t.UserId, t.DueDate });
        });

        builder.Entity<Reminder>(e =>
        {
            e.HasOne(r => r.User).WithMany(u => u.Reminders).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.ReminderAt);
            e.HasIndex(r => new { r.UserId, r.IsFired });
        });

        builder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.User).WithMany(u => u.Appointments).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.UserId, a.StartDateTime });
        });

        builder.Entity<ConversationHistory>(e =>
        {
            e.HasOne(c => c.User).WithMany(u => u.Conversations).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.UserId, c.CreatedAt });
        });

        builder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User).WithMany(u => u.Notifications).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.ScheduledAt, n.IsSent });
        });

        builder.Entity<Subscription>(e =>
        {
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.UserId, s.Status });
        });

        builder.Entity<UsageRecord>(e =>
        {
            e.HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(u => new { u.UserId, u.Type, u.OccurredAt });
        });
    }
}
