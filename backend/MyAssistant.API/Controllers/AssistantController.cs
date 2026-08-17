using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Assistant;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAssistantIntentService _assistant;
    private readonly ISpeechRecognitionService _speech;
    private readonly ITextToSpeechService _tts;
    private readonly ICurrentUserService _currentUser;

    public AssistantController(
        IAssistantIntentService assistant,
        ISpeechRecognitionService speech,
        ITextToSpeechService tts,
        ICurrentUserService currentUser)
    {
        _assistant = assistant;
        _speech = speech;
        _tts = tts;
        _currentUser = currentUser;
    }

    [HttpPost("command")]
    public async Task<IActionResult> Command([FromBody] AssistantRequest request, CancellationToken ct)
    {
        var result = await _assistant.ProcessAsync(request, _currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<AssistantResponse>.Ok(result));
    }

    [HttpPost("transcribe")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Transcribe(IFormFile audio, [FromQuery] string? language, CancellationToken ct)
    {
        await using var stream = audio.OpenReadStream();
        var result = await _speech.RecognizeAsync(stream, language ?? "en-IN", ct);
        if (!result.Success)
        {
            return BadRequest(ApiResponse.Fail(result.Error ?? "Speech recognition failed."));
        }
        return Ok(ApiResponse<SpeechRecognitionResult>.Ok(result));
    }

    [HttpPost("speak")]
    public async Task<IActionResult> Speak([FromBody] SpeakRequest request, CancellationToken ct)
    {
        var audio = await _tts.SynthesizeAsync(request.Text, request.Language ?? "en-IN", request.Speed, request.Volume, ct);
        if (audio is null)
        {
            return BadRequest(ApiResponse.Fail("Text-to-speech is not configured."));
        }
        return File(audio, _tts.GetMimeType());
    }

    [HttpGet("tts/status")]
    public IActionResult TtsStatus()
    {
        return Ok(ApiResponse<bool>.Ok(_tts.IsConfigured));
    }

    [HttpGet("stt/status")]
    public IActionResult SttStatus()
    {
        return Ok(ApiResponse<bool>.Ok(_speech.IsConfigured));
    }
}

public class SpeakRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; }
    public double Speed { get; set; } = 1.0;
    public double Volume { get; set; } = 1.0;
}
