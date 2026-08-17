using MyAssistant.Application.DTOs.Conversations;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Application.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _conversations;

    public ConversationService(IConversationRepository conversations)
    {
        _conversations = conversations;
    }

    public async Task<IReadOnlyList<ConversationDto>> GetHistoryAsync(Guid userId, int take = 100, CancellationToken cancellationToken = default)
    {
        var items = await _conversations.GetForUserAsync(userId, take, cancellationToken);
        return items.Select(c => new ConversationDto
        {
            Id = c.Id,
            UserMessage = c.UserMessage,
            AssistantResponse = c.AssistantResponse,
            Language = c.Language,
            Intent = c.Intent,
            IsVoice = c.IsVoice,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _conversations.DeleteAllForUserAsync(userId, cancellationToken);
    }
}
