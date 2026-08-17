using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Tasks;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;
    private readonly ICurrentUserService _currentUser;

    public TasksController(ITaskService tasks, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _tasks.GetAllAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<IReadOnlyList<TaskDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _tasks.GetByIdAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse<TaskDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var result = await _tasks.CreateAsync(_currentUser.UserId!.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<TaskDto>.Ok(result, "Task created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken ct)
    {
        var result = await _tasks.UpdateAsync(_currentUser.UserId!.Value, id, request, ct);
        return Ok(ApiResponse<TaskDto>.Ok(result, "Task updated."));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusRequest request, CancellationToken ct)
    {
        var result = await _tasks.UpdateStatusAsync(_currentUser.UserId!.Value, id, request, ct);
        return Ok(ApiResponse<TaskDto>.Ok(result, "Task status updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tasks.DeleteAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse.Ok("Task deleted."));
    }
}
