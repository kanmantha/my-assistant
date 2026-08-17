using MyAssistant.Domain.Common;

namespace MyAssistant.Domain.Entities;

public class ConversationHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string UserMessage { get; set; } = string.Empty;
    public string AssistantResponse { get; set; } = string.Empty;
    public string Language { get; set; } = "en-IN";
    public string? Intent { get; set; }
    public bool IsVoice { get; set; }
}
