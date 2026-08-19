using MyAssistant.Application.DTOs.Assistant;
using MyAssistant.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MyAssistant.Application.Services;

public class ConfigurableAIService : IAssistantAIService
{
    private readonly HeuristicAIService _local;
    private readonly OpenAiAIService? _remote;
    private readonly bool _useRemote;

    public ConfigurableAIService(HeuristicAIService local, OpenAiAIService? remote, IConfiguration configuration)
    {
        _local = local;
        _remote = remote;
        var provider = configuration["AI_PROVIDER"]?.Trim().ToLowerInvariant();
        var apiKey = configuration["AI_API_KEY"]?.Trim();
        _useRemote = remote != null && (provider is "openai" or "azure" or "gemini" || !string.IsNullOrEmpty(apiKey));
    }

    public Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default) =>
        _useRemote ? _remote!.DetectLanguageAsync(text, cancellationToken) : _local.DetectLanguageAsync(text, cancellationToken);

    public Task<ParsedCommand> ParseCommandAsync(string text, string? language, string timeZone, CancellationToken cancellationToken = default) =>
        _useRemote
            ? _remote!.ParseCommandAsync(text, language, timeZone, cancellationToken)
            : _local.ParseCommandAsync(text, language, timeZone, cancellationToken);

    public Task<string> GenerateReplyAsync(string intent, Dictionary<string, object?>? data, string language, CancellationToken cancellationToken = default) =>
        _useRemote
            ? _remote!.GenerateReplyAsync(intent, data, language, cancellationToken)
            : _local.GenerateReplyAsync(intent, data, language, cancellationToken);

    public Task<string> AnswerQuestionAsync(string question, string language, CancellationToken cancellationToken = default) =>
        _useRemote
            ? _remote!.AnswerQuestionAsync(question, language, cancellationToken)
            : _local.AnswerQuestionAsync(question, language, cancellationToken);
}
