using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Infrastructure.Data;

namespace MyAssistant.API;

public static class Seeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();

        // ---- Plans ----
        var plans = new[]
        {
            new Plan
            {
                Code = "FREE", Name = "Free", Type = PlanType.Free,
                PriceMonthly = 0, PriceYearly = 0, Currency = "INR",
                MaxNotes = 50, MaxTasks = 50, MaxRemindersPerMonth = 20, MaxAppointments = 20,
                MaxAiRequestsPerMonth = 20, MaxVoiceRequestsPerMonth = 20, MaxSpeechMinutesPerMonth = 10,
                AllowsVoice = true, AllowsCalendar = true, AllowsCloudBackup = false,
                AllowsCalendarIntegrations = false, AllowsAdvancedAi = false,
                Features = new()
                {
                    "Up to 50 notes", "Up to 50 tasks", "20 AI requests/month",
                    "Basic voice assistant", "Language: English, Hindi, Telugu"
                },
                IsEnabled = true, DisplayOrder = 1
            },
            new Plan
            {
                Code = "PRO", Name = "Pro", Type = PlanType.Pro,
                PriceMonthly = 149, PriceYearly = 1499, Currency = "INR",
                MaxNotes = -1, MaxTasks = -1, MaxRemindersPerMonth = -1, MaxAppointments = -1,
                MaxAiRequestsPerMonth = 500, MaxVoiceRequestsPerMonth = 500, MaxSpeechMinutesPerMonth = 100,
                AllowsVoice = true, AllowsCalendar = true, AllowsCloudBackup = true,
                AllowsCalendarIntegrations = true, AllowsAdvancedAi = false,
                Features = new List<string>
                {
                    "Unlimited notes & tasks", "500 AI requests/month", "Calendar + reminders",
                    "Cloud backup", "Smart scheduling", "Google & iOS calendar sync"
                },
                IsEnabled = true, DisplayOrder = 2
            },
            new Plan
            {
                Code = "PREMIUM", Name = "Premium", Type = PlanType.Premium,
                PriceMonthly = 349, PriceYearly = 3499, Currency = "INR",
                MaxNotes = -1, MaxTasks = -1, MaxRemindersPerMonth = -1, MaxAppointments = -1,
                MaxAiRequestsPerMonth = -1, MaxVoiceRequestsPerMonth = -1, MaxSpeechMinutesPerMonth = -1,
                AllowsVoice = true, AllowsCalendar = true, AllowsCloudBackup = true,
                AllowsCalendarIntegrations = true, AllowsAdvancedAi = true,
                Features = new List<string>
                {
                    "Unlimited everything", "Advanced AI assistant (GPT/Gemini)",
                    "Unlimited voice commands", "Priority support", "Advanced calendar AI"
                },
                IsEnabled = true, DisplayOrder = 3
            }
        };

        foreach (var plan in plans)
        {
            var existing = db.Plans.FirstOrDefault(p => p.Code == plan.Code);
            if (existing is null)
                db.Plans.Add(plan);
            else
            {
                existing.Name = plan.Name;
                existing.PriceMonthly = plan.PriceMonthly;
                existing.PriceYearly = plan.PriceYearly;
                existing.Currency = plan.Currency;
                existing.MaxNotes = plan.MaxNotes;
                existing.MaxTasks = plan.MaxTasks;
                existing.MaxRemindersPerMonth = plan.MaxRemindersPerMonth;
                existing.MaxAppointments = plan.MaxAppointments;
                existing.MaxAiRequestsPerMonth = plan.MaxAiRequestsPerMonth;
                existing.MaxVoiceRequestsPerMonth = plan.MaxVoiceRequestsPerMonth;
                existing.MaxSpeechMinutesPerMonth = plan.MaxSpeechMinutesPerMonth;
                existing.AllowsVoice = plan.AllowsVoice;
                existing.AllowsCalendar = plan.AllowsCalendar;
                existing.AllowsCloudBackup = plan.AllowsCloudBackup;
                existing.AllowsCalendarIntegrations = plan.AllowsCalendarIntegrations;
                existing.AllowsAdvancedAi = plan.AllowsAdvancedAi;
                existing.Features = plan.Features;
                existing.IsEnabled = plan.IsEnabled;
                existing.DisplayOrder = plan.DisplayOrder;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync();

        // ---- Demo admin + demo user ----
        await EnsureUserAsync(db, hasher, "Admin", "admin@myassistant.in", "Admin123!", UserRole.Admin, "en-IN", "Asia/Kolkata");
        await EnsureUserAsync(db, hasher, "Demo User", "demo@myassistant.in", "Demo123", UserRole.User, "en-IN", "Asia/Kolkata");
    }

    private static async Task EnsureUserAsync(
        AppDbContext db, IPasswordHasher hasher,
        string fullName, string email, string password, UserRole role,
        string language, string timezone)
    {
        var existing = db.Users.FirstOrDefault(u => u.Email == email);
        if (existing is null)
        {
            var user = new User
            {
                FullName = fullName,
                Email = email,
                PasswordHash = hasher.Hash(password),
                Role = role,
                PreferredLanguage = language,
                Timezone = timezone
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.UserSettings.Add(new UserSettings { UserId = user.Id, Language = language, Timezone = timezone });
            await db.SaveChangesAsync();
        }
    }
}