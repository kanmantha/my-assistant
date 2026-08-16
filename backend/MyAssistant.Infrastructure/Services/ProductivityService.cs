using Microsoft.Extensions.Logging;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using TaskStatus = MyAssistant.Domain.Entities.TaskStatus;

namespace MyAssistant.Infrastructure.Services;

public class ProductivityService : IProductivityService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProductivityService> _logger;

    public ProductivityService(IUnitOfWork uow, ILogger<ProductivityService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    // ----- NOTES -----
    public async Task<NoteDto> CreateNoteAsync(Guid userId, CreateNoteRequest request, string timezone)
    {
        var note = new Note
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Language = request.Language ?? "en-IN",
            Tags = request.Tags ?? new List<string>()
        };
        await _uow.Notes.AddAsync(note);
        await _uow.SaveChangesAsync();
        return Map(note);
    }

    public async Task<NoteDto> UpdateNoteAsync(Guid userId, Guid id, UpdateNoteRequest request)
    {
        var note = await GetOwnedNoteAsync(userId, id);
        if (request.Title is not null) note.Title = request.Title.Trim();
        if (request.Content is not null) note.Content = request.Content.Trim();
        if (request.Tags is not null) note.Tags = request.Tags;
        note.UpdatedAt = DateTime.UtcNow;
        _uow.Notes.Update(note);
        await _uow.SaveChangesAsync();
        return Map(note);
    }

    public async Task DeleteNoteAsync(Guid userId, Guid id)
    {
        var note = await GetOwnedNoteAsync(userId, id);
        _uow.Notes.Remove(note);
        await _uow.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<NoteDto>> GetNotesAsync(Guid userId, int page = 1, int pageSize = 50)
    {
        var notes = await _uow.Notes.ToListAsync(n => n.UserId == userId, (page - 1) * pageSize, pageSize);
        return notes.Select(Map).ToList();
    }

    // ----- TASKS -----
    public async Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request, string timezone)
    {
        var task = new TaskItem
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description ?? string.Empty,
            Priority = ParseEnum(request.Priority, TaskPriority.Medium),
            Status = TaskStatus.Pending,
            DueDate = request.DueDate is null ? null : DateTime.TryParse(request.DueDate, out var d) ? d : null,
            DueTime = request.DueTime,
            DueDateTime = BuildDueDateTime(request.DueDate, request.DueTime)
        };
        await _uow.Tasks.AddAsync(task);
        await _uow.SaveChangesAsync();
        return Map(task);
    }

    public async Task<TaskDto> UpdateTaskAsync(Guid userId, Guid id, UpdateTaskRequest request)
    {
        var task = await GetOwnedTaskAsync(userId, id);
        if (request.Title is not null) task.Title = request.Title.Trim();
        if (request.Description is not null) task.Description = request.Description;
        if (request.Priority is not null) task.Priority = ParseEnum(request.Priority, task.Priority);
        if (request.Status is not null) task.Status = ParseEnum(request.Status, task.Status);
        if (request.DueDate is not null) task.DueDate = DateTime.TryParse(request.DueDate, out var d) ? d : null;
        if (request.DueTime is not null) task.DueTime = request.DueTime;
        if (request.Status is not null && ParseEnum(request.Status, task.Status) == TaskStatus.Completed)
            task.CompletedAt = DateTime.UtcNow;
        task.DueDateTime = BuildDueDateTime(task.DueDate?.ToString("yyyy-MM-dd"), task.DueTime);
        _uow.Tasks.Update(task);
        await _uow.SaveChangesAsync();
        return Map(task);
    }

    public async Task DeleteTaskAsync(Guid userId, Guid id)
    {
        var task = await GetOwnedTaskAsync(userId, id);
        _uow.Tasks.Remove(task);
        await _uow.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(Guid userId, string? status = null)
    {
        var tasks = status is null
            ? await _uow.Tasks.ToListAsync(t => t.UserId == userId)
            : await _uow.Tasks.ToListAsync(t => t.UserId == userId && t.Status == ParseEnum(status, TaskStatus.Pending));
        return tasks.Select(Map).ToList();
    }

    // ----- REMINDERS -----
    public async Task<ReminderDto> CreateReminderAsync(Guid userId, CreateReminderRequest request, string timezone)
    {
        var when = request.ReminderDateTime ??
                   (request.DateTimeString is not null && DateTime.TryParse(request.DateTimeString, out var d2)
                       ? d2
                       : DateTime.UtcNow.AddMinutes(30));

        var reminder = new Reminder
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description ?? string.Empty,
            ReminderDateTime = when,
            Timezone = request.Timezone ?? timezone,
            Recurrence = ParseEnum(request.Recurrence, ReminderRecurrence.Once),
            RecurrenceRule = request.RecurrenceRule
        };
        await _uow.Reminders.AddAsync(reminder);
        await _uow.SaveChangesAsync();
        return Map(reminder);
    }

    public async Task<ReminderDto> UpdateReminderAsync(Guid userId, Guid id, UpdateReminderRequest request)
    {
        var reminder = await GetOwnedReminderAsync(userId, id);
        if (request.Title is not null) reminder.Title = request.Title.Trim();
        if (request.Description is not null) reminder.Description = request.Description;
        if (request.ReminderDateTime is not null) reminder.ReminderDateTime = request.ReminderDateTime.Value;
        if (request.Recurrence is not null) reminder.Recurrence = ParseEnum(request.Recurrence, reminder.Recurrence);
        if (request.IsCompleted is not null) reminder.IsCompleted = request.IsCompleted.Value;
        _uow.Reminders.Update(reminder);
        await _uow.SaveChangesAsync();
        return Map(reminder);
    }

    public async Task DeleteReminderAsync(Guid userId, Guid id)
    {
        var reminder = await GetOwnedReminderAsync(userId, id);
        _uow.Reminders.Remove(reminder);
        await _uow.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ReminderDto>> GetRemindersAsync(Guid userId, bool? onlyPending = null)
    {
        var reminders = onlyPending == true
            ? await _uow.Reminders.ToListAsync(r => r.UserId == userId && !r.IsCompleted)
            : await _uow.Reminders.ToListAsync(r => r.UserId == userId);
        return reminders.Select(Map).ToList();
    }

    // ----- APPOINTMENTS -----
    public async Task<AppointmentDto> CreateAppointmentAsync(Guid userId, CreateAppointmentRequest request, string timezone)
    {
        var start = request.StartDateTime ??
                    (request.StartDateTimeString is not null && DateTime.TryParse(request.StartDateTimeString, out var d3) ? d3 : DateTime.UtcNow.AddHours(1));

        var end = request.EndDateTime ??
                  (request.EndDateTimeString is not null && DateTime.TryParse(request.EndDateTimeString, out var d4) ? d4
                      : start.AddMinutes(request.DurationMinutes ?? 30));

        var appointment = new Appointment
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description ?? string.Empty,
            StartDateTime = start,
            EndDateTime = end,
            Location = request.Location ?? string.Empty,
            Participants = request.Participants ?? new List<string>(),
            Timezone = request.Timezone ?? timezone,
            ReminderMinutes = request.ReminderMinutes ?? 15,
            ReminderAt = start.AddMinutes(-(request.ReminderMinutes ?? 15)),
            Status = AppointmentStatus.Scheduled
        };
        await _uow.Appointments.AddAsync(appointment);
        await _uow.SaveChangesAsync();
        return Map(appointment);
    }

    public async Task<AppointmentDto> UpdateAppointmentAsync(Guid userId, Guid id, UpdateAppointmentRequest request)
    {
        var appointment = await GetOwnedAppointmentAsync(userId, id);
        if (request.Title is not null) appointment.Title = request.Title.Trim();
        if (request.Description is not null) appointment.Description = request.Description;
        if (request.StartDateTime is not null) appointment.StartDateTime = request.StartDateTime.Value;
        if (request.EndDateTime is not null) appointment.EndDateTime = request.EndDateTime.Value;
        if (request.Location is not null) appointment.Location = request.Location;
        if (request.Participants is not null) appointment.Participants = request.Participants;
        if (request.ReminderMinutes is not null)
        {
            appointment.ReminderMinutes = request.ReminderMinutes.Value;
            appointment.ReminderAt = appointment.StartDateTime.AddMinutes(-request.ReminderMinutes.Value);
        }
        if (request.Status is not null) appointment.Status = ParseEnum(request.Status, appointment.Status);
        appointment.UpdatedAt = DateTime.UtcNow;
        _uow.Appointments.Update(appointment);
        await _uow.SaveChangesAsync();
        return Map(appointment);
    }

    public async Task DeleteAppointmentAsync(Guid userId, Guid id)
    {
        var appointment = await GetOwnedAppointmentAsync(userId, id);
        _uow.Appointments.Remove(appointment);
        await _uow.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetAppointmentsAsync(Guid userId, DateTime? start = null, DateTime? end = null)
    {
        var appointments = await _uow.Appointments.ToListAsync(a =>
            a.UserId == userId &&
            (!start.HasValue || a.StartDateTime >= start.Value) &&
            (!end.HasValue || a.StartDateTime <= end.Value));
        return appointments.Select(Map).ToList();
    }

    // ----- helpers -----
    private async Task<Note> GetOwnedNoteAsync(Guid userId, Guid id)
        => await _uow.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId)
           ?? throw new AppError("Note not found", 404, "NOTE_NOT_FOUND");

    private async Task<TaskItem> GetOwnedTaskAsync(Guid userId, Guid id)
        => await _uow.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId)
           ?? throw new AppError("Task not found", 404, "TASK_NOT_FOUND");

    private async Task<Reminder> GetOwnedReminderAsync(Guid userId, Guid id)
        => await _uow.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId)
           ?? throw new AppError("Reminder not found", 404, "REMINDER_NOT_FOUND");

    private async Task<Appointment> GetOwnedAppointmentAsync(Guid userId, Guid id)
        => await _uow.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId)
           ?? throw new AppError("Appointment not found", 404, "APPOINTMENT_NOT_FOUND");

    private static string? BuildDueDateTime(string? date, string? time)
    {
        if (date is null && time is null) return null;
        var d = date is not null && DateTime.TryParse(date, out var dt) ? dt : DateTime.Today;
        if (time is not null && DateTime.TryParse(time, out var tm))
            return d.Date.Add(tm.TimeOfDay).ToString("yyyy-MM-ddTHH:mm:ss");
        return d.ToString("yyyy-MM-dd");
    }

    private static T ParseEnum<T>(string? value, T fallback) where T : struct
        => Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;

    private static NoteDto Map(Note n) => new(n.Id, n.Title, n.Content, n.Language, n.Tags, n.CreatedAt, n.UpdatedAt);
    private static TaskDto Map(TaskItem t) => new(t.Id, t.Title, t.Description, t.Priority.ToString(), t.Status.ToString(), t.DueDate?.ToString("yyyy-MM-dd"), t.DueTime, t.CreatedAt, t.CompletedAt);
    private static ReminderDto Map(Reminder r) => new(r.Id, r.Title, r.Description, r.ReminderDateTime, r.Timezone, r.Recurrence.ToString(), r.IsCompleted, r.CreatedAt);
    private static AppointmentDto Map(Appointment a) => new(a.Id, a.Title, a.Description, a.StartDateTime, a.EndDateTime, a.Location, a.Participants, a.Timezone, a.ReminderMinutes, a.Status.ToString(), a.CreatedAt);
}