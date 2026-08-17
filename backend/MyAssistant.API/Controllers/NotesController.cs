using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Notes;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly INoteService _notes;
    private readonly ICurrentUserService _currentUser;

    public NotesController(INoteService notes, ICurrentUserService currentUser)
    {
        _notes = notes;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _notes.GetAllAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<IReadOnlyList<NoteDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _notes.GetByIdAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse<NoteDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request, CancellationToken ct)
    {
        var result = await _notes.CreateAsync(_currentUser.UserId!.Value, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<NoteDto>.Ok(result, "Note created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request, CancellationToken ct)
    {
        var result = await _notes.UpdateAsync(_currentUser.UserId!.Value, id, request, ct);
        return Ok(ApiResponse<NoteDto>.Ok(result, "Note updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _notes.DeleteAsync(_currentUser.UserId!.Value, id, ct);
        return Ok(ApiResponse.Ok("Note deleted."));
    }
}
