namespace MyAssistant.Application.DTOs.Assistant;

public class AssistantRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? SessionId { get; set; }
    public bool IsVoice { get; set; }
}

public class AssistantResponse
{
    public string? Reply { get; set; }
    public string? Intent { get; set; }
    public string? Language { get; set; }
    public bool NeedsConfirmation { get; set; }
    public string? ConfirmationPrompt { get; set; }
    public string? PendingAction { get; set; }
    public Dictionary<string, object?>? Data { get; set; }
    public string? TtsText { get; set; }
}

public class CommandResponse
{
    public AssistantResponse Assistant { get; set; } = new();
    public object? CreatedEntity { get; set; }
}
