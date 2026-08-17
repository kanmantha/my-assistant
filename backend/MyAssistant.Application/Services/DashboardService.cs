using MyAssistant.Application.DTOs.Dashboard;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly INoteRepository _notes;
    private readonly ITaskRepository _tasks;
    private readonly IAppointmentRepository _appointments;
    private readonly IReminderRepository _reminders;
    private readonly IConversationRepository _conversations;
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(
        INoteRepository notes,
        ITaskRepository tasks,
        IAppointmentRepository appointments,
        IReminderRepository reminders,
        IConversationRepository conversations,
        INotificationRepository notifications,
        ICurrentUserService currentUser)
    {
        _notes = notes;
        _tasks = tasks;
        _appointments = appointments;
        _reminders = reminders;
        _conversations = conversations;
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<DashboardDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        var today = DateOnly.FromDateTime(todayLocal);
        var dayStartUtc = nowUtc.Date;
        var dayEndUtc = dayStartUtc.AddDays(1);

        var tasks = await _tasks.GetForUserAsync(userId, cancellationToken);
        var appointments = await _appointments.GetForUserAsync(userId, cancellationToken);
        var reminders = await _reminders.GetUpcomingForUserAsync(userId, nowUtc, 5, cancellationToken);
        var notes = await _notes.GetForUserAsync(userId, cancellationToken);

        var todayTasks = tasks.Where(t => t.DueDate == today && t.Status != Domain.Enums.TaskStatus.Completed).ToList();
        var todayAppointments = appointments.Where(a => a.StartDateTime >= dayStartUtc && a.StartDateTime < dayEndUtc).ToList();

        return new DashboardDto
        {
            Greeting = GetGreeting(nowUtc),
            UserName = _currentUser.UserName ?? "there",
            TasksToday = todayTasks.Count,
            TasksCompletedToday = tasks.Count(t => t.DueDate == today && t.Status == Domain.Enums.TaskStatus.Completed),
            TodayTasks = todayTasks.OrderBy(t => t.DueTime ?? TimeOnly.MaxValue).Select(TaskService.ToDto).ToList(),
            TodayAppointments = todayAppointments.OrderBy(a => a.StartDateTime).Select(AppointmentService.ToDto).ToList(),
            UpcomingReminders = reminders.Select(ReminderService.ToDto).ToList(),
            RecentNotes = notes.OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt).Take(5).Select(NoteService.ToDto).ToList(),
            PendingTasks = tasks.Count(t => t.Status != Domain.Enums.TaskStatus.Completed),
            UpcomingAppointments = appointments.Count(a => a.StartDateTime > nowUtc)
        };
    }

    public async Task DeleteAllDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notes = await _notes.GetForUserAsync(userId, cancellationToken);
        foreach (var note in notes)
        {
            await _notes.DeleteAsync(note, cancellationToken);
        }

        var tasks = await _tasks.GetForUserAsync(userId, cancellationToken);
        foreach (var task in tasks)
        {
            await _tasks.DeleteAsync(task, cancellationToken);
        }

        var appointments = await _appointments.GetForUserAsync(userId, cancellationToken);
        foreach (var appointment in appointments)
        {
            await _appointments.DeleteAsync(appointment, cancellationToken);
        }

        var reminders = await _reminders.GetForUserAsync(userId, cancellationToken);
        foreach (var reminder in reminders)
        {
            await _reminders.DeleteAsync(reminder, cancellationToken);
        }

        await _conversations.DeleteAllForUserAsync(userId, cancellationToken);

        var notifications = await _notifications.GetForUserAsync(userId, 5000, cancellationToken);
        foreach (var notification in notifications)
        {
            await _notifications.DeleteAsync(notification, cancellationToken);
        }
    }

    private static string GetGreeting(DateTime utcNow)
    {
        var hour = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")).Hour;
        return hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };
    }
}
