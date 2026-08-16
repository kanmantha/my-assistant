using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public record NoteDto(Guid Id, string Title, string Content, string Language, List<string> Tags, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateNoteRequest(string Title, string Content, string? Language = null, List<string>? Tags = null);
public record UpdateNoteRequest(string? Title = null, string? Content = null, List<string>? Tags = null);

public record TaskDto(Guid Id, string Title, string Description, string Priority, string Status, string? DueDate, string? DueTime, DateTime CreatedAt, DateTime? CompletedAt);
public record CreateTaskRequest(string Title, string? Description = null, string? Priority = null, string? DueDate = null, string? DueTime = null);
public record UpdateTaskRequest(string? Title = null, string? Description = null, string? Priority = null, string? Status = null, string? DueDate = null, string? DueTime = null);

public record ReminderDto(Guid Id, string Title, string Description, DateTime ReminderDateTime, string Timezone, string Recurrence, bool IsCompleted, DateTime CreatedAt);
public record CreateReminderRequest(string Title, string? Description = null, DateTime? ReminderDateTime = null, string? DateTimeString = null, string? Timezone = null, string? Recurrence = null, string? RecurrenceRule = null);
public record UpdateReminderRequest(string? Title = null, string? Description = null, DateTime? ReminderDateTime = null, string? Recurrence = null, bool? IsCompleted = null);

public record AppointmentDto(Guid Id, string Title, string Description, DateTime StartDateTime, DateTime EndDateTime, string Location, List<string> Participants, string Timezone, int ReminderMinutes, string Status, DateTime CreatedAt);
public record CreateAppointmentRequest(string Title, string? Description = null, DateTime? StartDateTime = null, string? StartDateTimeString = null, DateTime? EndDateTime = null, string? EndDateTimeString = null, int? DurationMinutes = null, string? Location = null, List<string>? Participants = null, int? ReminderMinutes = null, string? Timezone = null);
public record UpdateAppointmentRequest(string? Title = null, string? Description = null, DateTime? StartDateTime = null, DateTime? EndDateTime = null, string? Location = null, List<string>? Participants = null, int? ReminderMinutes = null, string? Status = null);

public interface IProductivityService
{
    Task<NoteDto> CreateNoteAsync(Guid userId, CreateNoteRequest request, string timezone);
    Task<NoteDto> UpdateNoteAsync(Guid userId, Guid id, UpdateNoteRequest request);
    Task DeleteNoteAsync(Guid userId, Guid id);
    Task<IReadOnlyList<NoteDto>> GetNotesAsync(Guid userId, int page = 1, int pageSize = 50);

    Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request, string timezone);
    Task<TaskDto> UpdateTaskAsync(Guid userId, Guid id, UpdateTaskRequest request);
    Task DeleteTaskAsync(Guid userId, Guid id);
    Task<IReadOnlyList<TaskDto>> GetTasksAsync(Guid userId, string? status = null);

    Task<ReminderDto> CreateReminderAsync(Guid userId, CreateReminderRequest request, string timezone);
    Task<ReminderDto> UpdateReminderAsync(Guid userId, Guid id, UpdateReminderRequest request);
    Task DeleteReminderAsync(Guid userId, Guid id);
    Task<IReadOnlyList<ReminderDto>> GetRemindersAsync(Guid userId, bool? onlyPending = null);

    Task<AppointmentDto> CreateAppointmentAsync(Guid userId, CreateAppointmentRequest request, string timezone);
    Task<AppointmentDto> UpdateAppointmentAsync(Guid userId, Guid id, UpdateAppointmentRequest request);
    Task DeleteAppointmentAsync(Guid userId, Guid id);
    Task<IReadOnlyList<AppointmentDto>> GetAppointmentsAsync(Guid userId, DateTime? start = null, DateTime? end = null);
}