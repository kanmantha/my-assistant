namespace MyAssistant.Application.DTOs.Conversations;

public class ConversationDto
{
    public Guid Id { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantResponse { get; set; } = string.Empty;
    public string Language { get; set; } = "en-IN";
    public string? Intent { get; set; }
    public bool IsVoice { get; set; }
    public DateTime CreatedAt { get; set; }
}
