using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.AI;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.API.Services;
using MyAssistant.Infrastructure.Services;

namespace MyAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly AssistantOrchestrator _orchestrator;
    private readonly IAssistantAiService _ai;

    public AssistantController(AssistantOrchestrator orchestrator, IAssistantAiService ai)
    {
        _orchestrator = orchestrator;
        _ai = ai;
    }

    [HttpPost("command")]
    public async Task<IActionResult> Command(AssistantCommandRequest request)
    {
        var req = new AssistantRequest
        {
            Text = request.Text,
            Language = request.Language ?? "en-IN",
            Timezone = request.Timezone ?? "Asia/Kolkata",
            UserId = this.GetUserId(),
            Context = request.Context?.Select(c => new ConversationTurn(c.Role, c.Content)).ToList()
        };
        var result = await _orchestrator.ProcessAsync(req);
        if (!result.Success && result.Error == "USAGE_LIMIT")
            return StatusCode(429, ApiResponse<AssistantResult>.Fail(result.ResponseText ?? "Usage limit reached", "USAGE_LIMIT"));
        return Ok(ApiResponse<AssistantResult>.Ok(result));
    }

    [HttpPost("intent")]
    public async Task<IActionResult> DetectIntent(AssistantCommandRequest request)
    {
        var req = new AssistantRequest
        {
            Text = request.Text,
            Language = request.Language ?? "en-IN",
            Timezone = request.Timezone ?? "Asia/Kolkata",
            UserId = this.GetUserId()
        };
        var result = await _ai.DetectIntentAsync(req);
        return Ok(ApiResponse<IntentResult>.Ok(result));
    }
}

public class AssistantCommandRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? Timezone { get; set; }
    public List<ContextTurnDto>? Context { get; set; }
}

public class ContextTurnDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}