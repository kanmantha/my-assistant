using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeNullableConverter>();
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<NotificationItem> Notifications => Set<NotificationItem>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.HasOne(u => u.Settings).WithOne(s => s.User).HasForeignKey<UserSettings>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(u => u.Subscription).WithOne(s => s.User).HasForeignKey<Subscription>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(u => u.Devices).WithOne(d => d.User).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(u => u.RefreshTokens).WithOne(t => t.User).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.Token).IsUnique();
            e.HasIndex(t => t.UserId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.HasIndex(d => d.UserId);
            e.HasIndex(d => d.FcmToken);
            e.HasIndex(d => d.DeviceToken);
        });

        // Nested value conversions to string[]
        var notes = modelBuilder.Entity<Note>(e =>
        {
            e.HasIndex(n => n.UserId);
            e.HasIndex(n => n.CreatedAt);
            e.Property(n => n.Tags).HasColumnType("jsonb");
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasIndex(t => t.UserId);
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.DueDate);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reminder>(e =>
        {
            e.HasIndex(r => r.UserId);
            e.HasIndex(r => r.ReminderDateTime);
            e.HasIndex(r => new { r.UserId, r.IsCompleted });
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.StartDateTime);
            e.HasIndex(a => a.EndDateTime);
            e.Property(a => a.Participants).HasColumnType("jsonb");
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasIndex(c => c.UserId);
            e.HasMany(c => c.Messages).WithOne(m => m.Conversation).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.HasIndex(m => m.ConversationId);
            e.HasIndex(m => m.CreatedAt);
        });

        modelBuilder.Entity<NotificationItem>(e =>
        {
            e.HasIndex(n => n.UserId);
            e.HasIndex(n => new { n.UserId, n.IsRead });
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Plan>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Features).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.HasIndex(s => s.UserId).IsUnique();
            e.HasIndex(s => new { s.UserId, s.Status });
            e.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.HasIndex(p => p.UserId);
            e.HasIndex(p => p.ProviderReference);
            e.HasOne(p => p.Subscription).WithMany().HasForeignKey(p => p.SubscriptionId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UsageRecord>(e =>
        {
            e.HasIndex(u => u.UserId);
            e.HasIndex(u => new { u.UserId, u.UsageType, u.OccurredAt });
            e.HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Slug).IsUnique();
            e.HasMany(o => o.Members).WithOne(m => m.Organization).HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizationMember>(e =>
        {
            e.HasKey(m => new { m.OrganizationId, m.UserId });
            e.HasIndex(m => m.UserId);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSettings>().HasIndex(u => u.UserId).IsUnique();
    }
}

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    { }
}

public class UtcDateTimeNullableConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcDateTimeNullableConverter() : base(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime()) : (DateTime?)null,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null)
    { }
}