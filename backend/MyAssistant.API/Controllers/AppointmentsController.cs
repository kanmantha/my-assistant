using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Appointments;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;
    private readonly ICurrentUserService _currentUser;

    public AppointmentsController(IAppointmentService appointments, ICurrentUserService currentUser)
    {
        _appointments = appointments;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _appointments.GetAllAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<IReadOnlyList<AppointmentDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _appointments.GetByIdAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse<AppointmentDto>.Ok(result));
    }

    [HttpGet("range")]
    public async Task<IActionResult> GetInRange([FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken ct)
    {
        var result = await _appointments.GetInRangeAsync(_currentUser.UserId!.Value, start, end, ct);
        return Ok(ApiResponse<IReadOnlyList<AppointmentDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request, CancellationToken ct)
    {
        var result = await _appointments.CreateAsync(_currentUser.UserId!.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<AppointmentDto>.Ok(result, "Appointment created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppointmentRequest request, CancellationToken ct)
    {
        var result = await _appointments.UpdateAsync(_currentUser.UserId!.Value, id, request, ct);
        return Ok(ApiResponse<AppointmentDto>.Ok(result, "Appointment updated."));
    }

    [HttpPatch("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentRequest request, CancellationToken ct)
    {
        var result = await _appointments.RescheduleAsync(_currentUser.UserId!.Value, id, request, ct);
        return Ok(ApiResponse<AppointmentDto>.Ok(result, "Appointment rescheduled."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _appointments.DeleteAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse.Ok("Appointment deleted."));
    }
}
