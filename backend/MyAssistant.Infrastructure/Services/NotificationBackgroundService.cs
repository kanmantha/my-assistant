using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminders = scope.ServiceProvider.GetRequiredService<IReminderRepository>();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var dueReminders = await reminders.GetDueAsync(DateTime.UtcNow, stoppingToken);
                foreach (var reminder in dueReminders)
                {
                    if (reminder.Recurrence != Domain.Enums.RecurrenceType.Once && !string.IsNullOrWhiteSpace(reminder.RecurrenceRule))
                    {
                        var next = ComputeNextOccurrence(reminder);
                        if (next.HasValue)
                        {
                            reminder.ReminderAt = next.Value;
                            reminder.IsFired = false;
                            await reminders.UpdateAsync(reminder, stoppingToken);
                            await notifications.CreateAsync(
                                reminder.UserId,
                                $"Reminder: {reminder.Title}",
                                reminder.Message,
                                "Reminder",
                                reminder.Id,
                                next.Value,
                                stoppingToken);
                            continue;
                        }
                    }

                    reminder.IsFired = true;
                    reminder.FiredAt = DateTime.UtcNow;
                    await reminders.UpdateAsync(reminder, stoppingToken);
                }

                var dueNotifications = await notifications.GetDueAsync(stoppingToken);
                foreach (var notification in dueNotifications)
                {
                    await notifications.MarkSentAsync(notification, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification background service error.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private static DateTime? ComputeNextOccurrence(Domain.Entities.Reminder reminder)
    {
        var now = DateTime.UtcNow;
        return reminder.Recurrence switch
        {
            Domain.Enums.RecurrenceType.Daily => now.Date.AddDays(1).Add(reminder.ReminderAt.TimeOfDay),
            Domain.Enums.RecurrenceType.Weekly => now.Date.AddDays(7).Add(reminder.ReminderAt.TimeOfDay),
            Domain.Enums.RecurrenceType.Monthly => AddMonthsKeepDay(reminder.ReminderAt, 1),
            Domain.Enums.RecurrenceType.Yearly => reminder.ReminderAt.AddYears(1),
            _ => null
        };
    }

    private static DateTime? AddMonthsKeepDay(DateTime source, int months)
    {
        var next = source.AddMonths(months);
        return next;
    }
}
