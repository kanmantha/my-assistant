using MyAssistant.Application.DTOs.Assistant;

namespace MyAssistant.Application.Interfaces;

public interface IAssistantAIService
{
    Task<ParsedCommand> ParseCommandAsync(string text, string? language, string timeZone, CancellationToken cancellationToken = default);
    Task<string> GenerateReplyAsync(string intent, Dictionary<string, object?>? data, string language, CancellationToken cancellationToken = default);
    Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default);
    Task<string> AnswerQuestionAsync(string question, string language, CancellationToken cancellationToken = default);
}

public interface IAssistantIntentService
{
    Task<AssistantResponse> ProcessAsync(AssistantRequest request, Guid userId, CancellationToken cancellationToken = default);
}
