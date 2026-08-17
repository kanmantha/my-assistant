using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Conversations;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;
    private readonly ICurrentUserService _currentUser;

    public ConversationsController(IConversationService conversations, ICurrentUserService currentUser)
    {
        _conversations = conversations;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(CancellationToken ct, [FromQuery] int take = 100)
    {
        var result = await _conversations.GetHistoryAsync(_currentUser.UserId!.Value, take, ct);
        return Ok(ApiResponse<IReadOnlyList<ConversationDto>>.Ok(result));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        await _conversations.DeleteAllAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse.Ok("Conversation history cleared."));
    }
}
