using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Admin;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await _adminService.GetUsersAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<UserAdminDto>>.Ok(users));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _adminService.GetStatsAsync(ct);
        return Ok(ApiResponse<AdminStatsDto>.Ok(stats));
    }

    [HttpPost("users/{userId:guid}/reset-usage")]
    public async Task<IActionResult> ResetUsage(Guid userId, CancellationToken ct)
    {
        var removed = await _adminService.ResetUsageAsync(userId, ct);
        return Ok(ApiResponse<object>.Ok(new { removedRecords = removed }));
    }
}
