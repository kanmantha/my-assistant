using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Reminders;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Services;

public class ReminderService : IReminderService
{
    private readonly IReminderRepository _reminders;
    private readonly INotificationService _notifications;
    private readonly ISubscriptionService _subscriptionService;

    public ReminderService(IReminderRepository reminders, INotificationService notifications, ISubscriptionService subscriptionService)
    {
        _reminders = reminders;
        _notifications = notifications;
        _subscriptionService = subscriptionService;
    }

    public async Task<IReadOnlyList<ReminderDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reminders = await _reminders.GetForUserAsync(userId, cancellationToken);
        return reminders.OrderBy(r => r.ReminderAt).Select(ToDto).ToList();
    }

    public async Task<ReminderDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var reminder = await _reminders.GetForUserByIdAsync(userId, id, cancellationToken)
                       ?? throw new NotFoundException("Reminder not found.");
        return ToDto(reminder);
    }

    public async Task<ReminderDto> CreateAsync(Guid userId, CreateReminderRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _subscriptionService.CanUseFeatureAsync(userId, Domain.Enums.UsageType.Reminder, cancellationToken))
        {
            throw new AppException("You have reached your monthly reminder limit. Please upgrade your plan.", 403);
        }

        var reminder = new Reminder
        {
            UserId = userId,
            Title = request.Title,
            Message = request.Message,
            ReminderAt = request.ReminderAt,
            Recurrence = request.Recurrence,
            RecurrenceRule = request.RecurrenceRule,
            Priority = request.Priority
        };
        await _reminders.AddAsync(reminder, cancellationToken);

        await _notifications.CreateAsync(
            userId,
            $"Reminder: {reminder.Title}",
            reminder.Message,
            "Reminder",
            reminder.Id,
            reminder.ReminderAt,
            cancellationToken);

        await _subscriptionService.RecordUsageAsync(userId, Domain.Enums.UsageType.Reminder, cancellationToken: cancellationToken);
        return ToDto(reminder);
    }

    public async Task<ReminderDto> UpdateAsync(Guid userId, Guid id, UpdateReminderRequest request, CancellationToken cancellationToken = default)
    {
        var reminder = await _reminders.GetForUserByIdAsync(userId, id, cancellationToken)
                       ?? throw new NotFoundException("Reminder not found.");
        reminder.Title = request.Title;
        reminder.Message = request.Message;
        reminder.ReminderAt = request.ReminderAt;
        reminder.Recurrence = request.Recurrence;
        reminder.RecurrenceRule = request.RecurrenceRule;
        reminder.Priority = request.Priority;
        reminder.UpdatedAt = DateTime.UtcNow;
        await _reminders.UpdateAsync(reminder, cancellationToken);
        return ToDto(reminder);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var reminder = await _reminders.GetForUserByIdAsync(userId, id, cancellationToken)
                       ?? throw new NotFoundException("Reminder not found.");
        await _reminders.DeleteAsync(reminder, cancellationToken);
    }

    public async Task AcknowledgeAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var reminder = await _reminders.GetForUserByIdAsync(userId, id, cancellationToken)
                       ?? throw new NotFoundException("Reminder not found.");
        reminder.IsAcknowledged = true;
        reminder.UpdatedAt = DateTime.UtcNow;
        await _reminders.UpdateAsync(reminder, cancellationToken);
    }

    internal static ReminderDto ToDto(Reminder reminder) => new()
    {
        Id = reminder.Id,
        Title = reminder.Title,
        Message = reminder.Message,
        ReminderAt = reminder.ReminderAt,
        Recurrence = reminder.Recurrence,
        RecurrenceRule = reminder.RecurrenceRule,
        Priority = reminder.Priority,
        IsFired = reminder.IsFired,
        IsAcknowledged = reminder.IsAcknowledged,
        CreatedAt = reminder.CreatedAt
    };
}
