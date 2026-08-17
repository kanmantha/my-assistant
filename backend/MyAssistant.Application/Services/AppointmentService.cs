using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Appointments;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointments;
    private readonly INotificationService _notifications;
    private readonly ISubscriptionService _subscriptionService;

    public AppointmentService(IAppointmentRepository appointments, INotificationService notifications, ISubscriptionService subscriptionService)
    {
        _appointments = appointments;
        _notifications = notifications;
        _subscriptionService = subscriptionService;
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _appointments.GetForUserAsync(userId, cancellationToken);
        return items.OrderBy(a => a.StartDateTime).Select(ToDto).ToList();
    }

    public async Task<AppointmentDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _appointments.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Appointment not found.");
        return ToDto(item);
    }

    public async Task<AppointmentDto> CreateAsync(Guid userId, CreateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var end = request.EndDateTime ?? request.StartDateTime.AddMinutes(30);
        if (end <= request.StartDateTime)
        {
            throw new ValidationException("End date/time must be after the start.");
        }

        var item = new Appointment
        {
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            StartDateTime = request.StartDateTime,
            EndDateTime = end,
            Location = request.Location,
            Participants = request.Participants,
            ReminderMinutes = request.ReminderMinutes > 0 ? request.ReminderMinutes : 15
        };
        await _appointments.AddAsync(item, cancellationToken);

        if (item.ReminderMinutes > 0)
        {
            await _notifications.CreateAsync(
                userId,
                $"You have an appointment: {item.Title}",
                item.Location,
                "Appointment",
                item.Id,
                item.StartDateTime.AddMinutes(-item.ReminderMinutes),
                cancellationToken);
        }

        await _subscriptionService.RecordUsageAsync(userId, Domain.Enums.UsageType.Appointment, cancellationToken: cancellationToken);
        return ToDto(item);
    }

    public async Task<AppointmentDto> UpdateAsync(Guid userId, Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _appointments.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Appointment not found.");
        var end = request.EndDateTime ?? request.StartDateTime.AddMinutes(30);
        if (end <= request.StartDateTime)
        {
            throw new ValidationException("End date/time must be after the start.");
        }

        item.Title = request.Title;
        item.Description = request.Description;
        item.StartDateTime = request.StartDateTime;
        item.EndDateTime = end;
        item.Location = request.Location;
        item.Participants = request.Participants;
        item.ReminderMinutes = request.ReminderMinutes > 0 ? request.ReminderMinutes : 15;
        item.Status = request.Status;
        item.UpdatedAt = DateTime.UtcNow;
        await _appointments.UpdateAsync(item, cancellationToken);
        return ToDto(item);
    }

    public async Task<AppointmentDto> RescheduleAsync(Guid userId, Guid id, RescheduleAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _appointments.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Appointment not found.");
        var duration = item.EndDateTime - item.StartDateTime;
        item.StartDateTime = request.StartDateTime;
        item.EndDateTime = request.EndDateTime ?? request.StartDateTime.Add(duration);
        item.Status = Domain.Enums.AppointmentStatus.Rescheduled;
        item.UpdatedAt = DateTime.UtcNow;
        await _appointments.UpdateAsync(item, cancellationToken);
        return ToDto(item);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _appointments.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Appointment not found.");
        await _appointments.DeleteAsync(item, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetInRangeAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
    {
        var items = await _appointments.GetInRangeAsync(userId, startUtc, endUtc, cancellationToken);
        return items.Select(ToDto).ToList();
    }

    internal static AppointmentDto ToDto(Appointment item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        StartDateTime = item.StartDateTime,
        EndDateTime = item.EndDateTime,
        Location = item.Location,
        Participants = item.Participants,
        ReminderMinutes = item.ReminderMinutes,
        Status = item.Status,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };
}
