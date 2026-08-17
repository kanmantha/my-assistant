using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyAssistant.Domain.Entities;
using MyAssistant.Infrastructure.Data;

namespace MyAssistant.Infrastructure;

public class DbSeeder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(IServiceScopeFactory scopeFactory, ILogger<DbSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await EnsureRolesAsync(roleManager);

        var email = "demo@example.com";
        var password = "Demo@12345";

        if (await userManager.FindByEmailAsync(email) == null)
        {
            var user = new AppUser
            {
                Email = email,
                UserName = email,
                FirstName = "Vinod",
                LastName = "Kumar",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                _logger.LogError("Seed user creation failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            await userManager.AddToRoleAsync(user, "User");

            if (!context.UserSettings.Any(s => s.UserId == user.Id))
            {
                context.UserSettings.Add(new UserSettings { UserId = user.Id, WakeWordEnabled = true, Language = Domain.Enums.AppLanguage.en });
            }

            var now = DateTime.UtcNow;
            context.Notes.AddRange(
                new Note { UserId = user.Id, Title = "Azure study notes", Content = "Learn Azure AI services for the school project.", OriginalLanguage = "en-IN", Tags = new() { "study", "azure" } },
                new Note { UserId = user.Id, Title = "Grocery list", Content = "Buy groceries: milk, vegetables, rice.", OriginalLanguage = "en-IN", Tags = new() { "home" } }
            );

            context.Tasks.AddRange(
                new TaskItem { UserId = user.Id, Title = "Complete project report", Status = Domain.Enums.TaskStatus.Pending, Priority = Domain.Enums.TaskPriority.High, DueDate = DateOnly.FromDateTime(now) },
                new TaskItem { UserId = user.Id, Title = "Review deployment", Status = Domain.Enums.TaskStatus.Pending, Priority = Domain.Enums.TaskPriority.Medium, DueDate = DateOnly.FromDateTime(now) },
                new TaskItem { UserId = user.Id, Title = "Complete project documentation", Status = Domain.Enums.TaskStatus.Pending, Priority = Domain.Enums.TaskPriority.High }
            );

            context.Reminders.AddRange(
                new Reminder { UserId = user.Id, Title = "Call Ravi", Message = "Call Ravi", ReminderAt = now.Date.AddHours(18) },
                new Reminder { UserId = user.Id, Title = "Pay electricity bill", Message = "Pay electricity bill", ReminderAt = now.Date.AddHours(8) }
            );

            context.Appointments.AddRange(
                new Appointment
                {
                    UserId = user.Id,
                    Title = "Team Meeting",
                    Description = "Weekly team sync",
                    StartDateTime = now.Date.AddHours(9),
                    EndDateTime = now.Date.AddHours(10),
                    Participants = new() { "Team" },
                    ReminderMinutes = 15
                },
                new Appointment
                {
                    UserId = user.Id,
                    Title = "Client Call",
                    StartDateTime = now.Date.AddHours(11),
                    EndDateTime = now.Date.AddHours(11).AddMinutes(30),
                    Participants = new() { "Client" },
                    ReminderMinutes = 15
                }
            );

            context.Subscriptions.Add(new Subscription { UserId = user.Id });

            await context.SaveChangesAsync();
            _logger.LogInformation("Seeded demo user {Email} with password {Password}", email, password);
        }

        var adminEmail = "admin@example.com";
        var adminPassword = "Admin@12345";

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new AppUser
            {
                Email = adminEmail,
                UserName = adminEmail,
                FirstName = "Admin",
                LastName = "Account",
                EmailConfirmed = true
            };

            var adminResult = await userManager.CreateAsync(admin, adminPassword);
            if (!adminResult.Succeeded)
            {
                _logger.LogError("Admin seed user creation failed: {Errors}", string.Join("; ", adminResult.Errors.Select(e => e.Description)));
                return;
            }
            await userManager.AddToRoleAsync(admin, "Admin");

            context.UserSettings.Add(new UserSettings { UserId = admin.Id, WakeWordEnabled = true });
            context.Subscriptions.Add(new Subscription { UserId = admin.Id });

            await context.SaveChangesAsync();
            _logger.LogInformation("Seeded admin user {Email} with password {Password}", adminEmail, adminPassword);
        }
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in new[] { "User", "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
            }
        }
    }
}
