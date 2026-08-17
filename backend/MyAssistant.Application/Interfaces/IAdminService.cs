using MyAssistant.Application.DTOs.Admin;

namespace MyAssistant.Application.Interfaces;

public interface IAdminService
{
    Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<int> ResetUsageAsync(Guid userId, CancellationToken cancellationToken = default);
}
