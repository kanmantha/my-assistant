using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyAssistant.Application.Common;
using MyAssistant.Domain.Entities;
using MyAssistant.Domain.Enums;
using MyAssistant.Infrastructure.Data;
using MyAssistant.Infrastructure.Services;

namespace MyAssistant.Tests;

public class AdminServiceTests
{
    private static AppDbContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static UserManager<AppUser> CreateUserManager(AppDbContext context)
    {
        var store = new UserStore<AppUser, IdentityRole<Guid>, AppDbContext, Guid>(context);
        return new UserManager<AppUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<AppUser>(),
            Array.Empty<IUserValidator<AppUser>>(),
            Array.Empty<IPasswordValidator<AppUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new NullLogger<UserManager<AppUser>>());
    }

    private static void SeedRoles(AppDbContext context)
    {
        context.Roles.Add(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" });
        context.Roles.Add(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" });
        context.SaveChanges();
    }

    private static Guid SeedUser(AppDbContext context, string email, string role, SubscriptionTier tier)
    {
        var userId = Guid.NewGuid();
        var roleId = context.Roles.Single(r => r.Name == role).Id;
        context.Users.Add(new AppUser
        {
            Id = userId,
            Email = email,
            UserName = email,
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true
        });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        context.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            Tier = tier,
            Status = SubscriptionStatus.Active,
            StartedAt = DateTime.UtcNow.AddMonths(-2)
        });
        context.SaveChanges();
        return userId;
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsRolesTierAndCurrentMonthUsage()
    {
        using var context = CreateContext(out var connection);
        using var _ = connection;
        SeedRoles(context);

        var adminId = SeedUser(context, "admin@example.com", "Admin", SubscriptionTier.Pro);
        SeedUser(context, "user@example.com", "User", SubscriptionTier.Free);

        context.UsageRecords.AddRange(
            new UsageRecord { UserId = adminId, Type = UsageType.Note, Count = 2, OccurredAt = DateTime.UtcNow },
            new UsageRecord { UserId = adminId, Type = UsageType.Note, Count = 1, OccurredAt = DateTime.UtcNow },
            new UsageRecord { UserId = adminId, Type = UsageType.AiCommand, Count = 1, OccurredAt = DateTime.UtcNow },
            new UsageRecord { UserId = adminId, Type = UsageType.Note, Count = 9, OccurredAt = DateTime.UtcNow.AddMonths(-1) }
        );
        await context.SaveChangesAsync();

        var service = new AdminService(context, CreateUserManager(context));

        var users = await service.GetUsersAsync();

        users.Should().HaveCount(2);
        var admin = users.Single(u => u.Id == adminId);
        admin.Roles.Should().Contain("Admin");
        admin.Tier.Should().Be("Pro");
        admin.Status.Should().Be("Active");
        admin.UsageThisMonth.Notes.Should().Be(3);
        admin.UsageThisMonth.AiCommands.Should().Be(1);
        admin.UsageThisMonth.Tasks.Should().Be(0);
    }

    [Fact]
    public async Task GetStatsAsync_CountsPlatformTotals()
    {
        using var context = CreateContext(out var connection);
        using var _ = connection;
        SeedRoles(context);

        var freeId = SeedUser(context, "free@example.com", "User", SubscriptionTier.Free);
        var proId = SeedUser(context, "pro@example.com", "User", SubscriptionTier.Pro);

        context.Notes.Add(new Note { UserId = freeId, Title = "N1", Content = "C1" });
        context.Tasks.Add(new TaskItem { UserId = freeId, Title = "T1" });
        context.Reminders.Add(new Reminder { UserId = freeId, Title = "R1", ReminderAt = DateTime.UtcNow.AddHours(1) });
        context.Appointments.Add(new Appointment { UserId = freeId, Title = "A1", StartDateTime = DateTime.UtcNow.AddHours(2), EndDateTime = DateTime.UtcNow.AddHours(3) });
        context.UsageRecords.AddRange(
            new UsageRecord { UserId = freeId, Type = UsageType.Note, Count = 4, OccurredAt = DateTime.UtcNow },
            new UsageRecord { UserId = proId, Type = UsageType.Task, Count = 2, OccurredAt = DateTime.UtcNow },
            new UsageRecord { UserId = proId, Type = UsageType.Task, Count = 2, OccurredAt = DateTime.UtcNow.AddMonths(-2) }
        );
        await context.SaveChangesAsync();

        var service = new AdminService(context, CreateUserManager(context));

        var stats = await service.GetStatsAsync();

        stats.TotalUsers.Should().Be(2);
        stats.ActiveUsers.Should().Be(2);
        stats.FreeUsers.Should().Be(1);
        stats.PremiumUsers.Should().Be(1);
        stats.TotalNotes.Should().Be(1);
        stats.TotalTasks.Should().Be(1);
        stats.TotalReminders.Should().Be(1);
        stats.TotalAppointments.Should().Be(1);
        stats.UsageThisMonth.Should().Be(6);
    }

    [Fact]
    public async Task ResetUsageAsync_RemovesOnlyThisMonthsRecords()
    {
        using var context = CreateContext(out var connection);
        using var _ = connection;
        SeedRoles(context);

        var userId = SeedUser(context, "user@example.com", "User", SubscriptionTier.Free);
        context.UsageRecords.AddRange(
            new UsageRecord { UserId = userId, Type = UsageType.Note, Count = 1, OccurredAt = DateTime.UtcNow },
            new UsageRecord { UserId = userId, Type = UsageType.Note, Count = 5, OccurredAt = DateTime.UtcNow.AddMonths(-1) }
        );
        await context.SaveChangesAsync();

        var service = new AdminService(context, CreateUserManager(context));

        var removed = await service.ResetUsageAsync(userId);

        removed.Should().Be(1);
        context.UsageRecords.Should().HaveCount(1);
        context.UsageRecords.Single().Count.Should().Be(5);
    }

    [Fact]
    public async Task ResetUsageAsync_UnknownUser_ThrowsNotFound()
    {
        using var context = CreateContext(out var connection);
        using var _ = connection;
        SeedRoles(context);

        var service = new AdminService(context, CreateUserManager(context));

        var act = () => service.ResetUsageAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
