using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Reminders;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _reminders;
    private readonly ICurrentUserService _currentUser;

    public RemindersController(IReminderService reminders, ICurrentUserService currentUser)
    {
        _reminders = reminders;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _reminders.GetAllAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<IReadOnlyList<ReminderDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _reminders.GetByIdAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse<ReminderDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReminderRequest request, CancellationToken ct)
    {
        var result = await _reminders.CreateAsync(_currentUser.UserId!.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<ReminderDto>.Ok(result, "Reminder created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReminderRequest request, CancellationToken ct)
    {
        var result = await _reminders.UpdateAsync(_currentUser.UserId!.Value, id, request, ct);
        return Ok(ApiResponse<ReminderDto>.Ok(result, "Reminder updated."));
    }

    [HttpPatch("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        await _reminders.AcknowledgeAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse.Ok("Reminder acknowledged."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _reminders.DeleteAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse.Ok("Reminder deleted."));
    }
}
