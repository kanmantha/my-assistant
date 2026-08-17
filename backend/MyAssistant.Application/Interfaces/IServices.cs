using MyAssistant.Application.DTOs.Auth;
using MyAssistant.Application.DTOs.Dashboard;
using MyAssistant.Application.DTOs.Notes;
using MyAssistant.Application.DTOs.Appointments;
using MyAssistant.Application.DTOs.Conversations;
using MyAssistant.Application.DTOs.Reminders;
using MyAssistant.Application.DTOs.Search;
using MyAssistant.Application.DTOs.Settings;
using MyAssistant.Application.DTOs.Tasks;

namespace MyAssistant.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface INoteService
{
    Task<IReadOnlyList<NoteDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NoteDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<NoteDto> CreateAsync(Guid userId, CreateNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDto> UpdateAsync(Guid userId, Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}

public interface ITaskService
{
    Task<IReadOnlyList<TaskDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TaskDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<TaskDto> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateAsync(Guid userId, Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateStatusAsync(Guid userId, Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}

public interface IReminderService
{
    Task<IReadOnlyList<ReminderDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ReminderDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<ReminderDto> CreateAsync(Guid userId, CreateReminderRequest request, CancellationToken cancellationToken = default);
    Task<ReminderDto> UpdateAsync(Guid userId, Guid id, UpdateReminderRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AppointmentDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<AppointmentDto> CreateAsync(Guid userId, CreateAppointmentRequest request, CancellationToken cancellationToken = default);
    Task<AppointmentDto> UpdateAsync(Guid userId, Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default);
    Task<AppointmentDto> RescheduleAsync(Guid userId, Guid id, RescheduleAppointmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppointmentDto>> GetInRangeAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
}

public interface IConversationService
{
    Task<IReadOnlyList<ConversationDto>> GetHistoryAsync(Guid userId, int take = 100, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserSettingsDto> UpdateAsync(Guid userId, UpdateSettingsRequest request, CancellationToken cancellationToken = default);
}

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(Guid userId, SearchRequest request, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAllDataAsync(Guid userId, CancellationToken cancellationToken = default);
}
