using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Admin;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Domain.Enums;
using MyAssistant.Infrastructure.Data;

namespace MyAssistant.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public AdminService(AppDbContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var monthStart = MonthStart();

        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestSubs = await _context.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .ToListAsync(cancellationToken);
        var subByUser = latestSubs
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartedAt).First());

        var monthUsage = await _context.UsageRecords
            .Where(u => u.OccurredAt >= monthStart)
            .ToListAsync(cancellationToken);
        var usageByUser = monthUsage
            .GroupBy(u => u.UserId)
            .ToDictionary(g => g.Key, g => g.ToLookup(x => x.Type, x => x.Count));

        var result = new List<UserAdminDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var sub = subByUser.GetValueOrDefault(user.Id);
            var usage = usageByUser.GetValueOrDefault(user.Id);
            result.Add(new UserAdminDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = user.DisplayName,
                Roles = roles.ToArray(),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                OrganizationId = user.OrganizationId?.ToString(),
                Tier = sub?.Tier.ToString() ?? SubscriptionTier.Free.ToString(),
                Status = sub?.Status.ToString() ?? SubscriptionStatus.Expired.ToString(),
                RenewalAt = sub?.RenewalAt,
                UsageThisMonth = BuildUsage(usage)
            });
        }
        return result;
    }

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var monthStart = MonthStart();

        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive, cancellationToken);
        var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= monthStart, cancellationToken);
        var activeUsersThisMonth = await _context.UsageRecords
            .Where(u => u.OccurredAt >= monthStart)
            .Select(u => u.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var latestSubs = await _context.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .ToListAsync(cancellationToken);
        var tiers = latestSubs
            .GroupBy(s => s.UserId)
            .Select(g => g.OrderByDescending(s => s.StartedAt).First());
        var freeUsers = tiers.Count(s => s.Tier == SubscriptionTier.Free);
        var premiumUsers = tiers.Count(s => s.Tier != SubscriptionTier.Free);

        var usageThisMonth = await _context.UsageRecords
            .Where(u => u.OccurredAt >= monthStart)
            .SumAsync(u => u.Count, cancellationToken);

        return new AdminStatsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            NewUsersThisMonth = newUsersThisMonth,
            ActiveUsersThisMonth = activeUsersThisMonth,
            FreeUsers = freeUsers,
            PremiumUsers = premiumUsers,
            TotalNotes = await _context.Notes.CountAsync(cancellationToken),
            TotalTasks = await _context.Tasks.CountAsync(cancellationToken),
            TotalReminders = await _context.Reminders.CountAsync(cancellationToken),
            TotalAppointments = await _context.Appointments.CountAsync(cancellationToken),
            UsageThisMonth = usageThisMonth
        };
    }

    public async Task<int> ResetUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("User not found.");
        }

        var monthStart = MonthStart();
        var records = await _context.UsageRecords
            .Where(u => u.UserId == userId && u.OccurredAt >= monthStart)
            .ToListAsync(cancellationToken);

        _context.UsageRecords.RemoveRange(records);
        return await _context.SaveChangesAsync(cancellationToken);
    }

    private static DateTime MonthStart()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static UserUsageDto BuildUsage(ILookup<UsageType, int>? usage)
    {
        if (usage == null) return new UserUsageDto();
        return new UserUsageDto
        {
            AiCommands = usage[UsageType.AiCommand].Sum(),
            SpeechToText = usage[UsageType.SpeechToText].Sum(),
            TextToSpeech = usage[UsageType.TextToSpeech].Sum(),
            Notes = usage[UsageType.Note].Sum(),
            Tasks = usage[UsageType.Task].Sum(),
            Reminders = usage[UsageType.Reminder].Sum(),
            Appointments = usage[UsageType.Appointment].Sum(),
            Searches = usage[UsageType.Search].Sum()
        };
    }
}
