using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Dashboard;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IDashboardService dashboard, ICurrentUserService currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _dashboard.GetAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<DashboardDto>.Ok(result));
    }

    [HttpDelete("data")]
    public async Task<IActionResult> DeleteAllData(CancellationToken ct)
    {
        await _dashboard.DeleteAllDataAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse.Ok("All your data has been deleted."));
    }
}
