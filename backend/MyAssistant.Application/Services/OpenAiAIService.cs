using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyAssistant.Application.DTOs.Assistant;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyAssistant.Application.Services;

public class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

public class OpenAiAIService : IAssistantAIService
{
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiAIService> _logger;
    private readonly HttpClient _http;

    public OpenAiAIService(IOptions<OpenAiOptions> options, ILogger<OpenAiAIService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _http = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/")
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await CompleteAsync(
            "You detect the language of the user's message and reply with exactly one of these strings: en-IN, hi-IN, te-IN.",
            text, 0, cancellationToken);
        var normalized = result.Trim().ToLowerInvariant();
        if (normalized.StartsWith("hi")) return "hi-IN";
        if (normalized.StartsWith("te")) return "te-IN";
        return "en-IN";
    }

    public async Task<ParsedCommand> ParseCommandAsync(string text, string? language, string timeZone, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var systemPrompt = $"""
            You are an AI personal assistant that converts natural-language commands into strict JSON.
            The user's preferred language code is {language ?? "en-IN"}. The current local date and time is {now} in timezone {timeZone}.
            Use this timezone to resolve relative dates like tomorrow, next Monday, in 2 hours, at 6 PM.
            Produce JSON only with these fields:
            intent (one of: CreateNote, CreateTask, CreateReminder, CreateAppointment, UpdateTask, CompleteTask, DeleteTask, DeleteNote, DeleteReminder, DeleteAppointment, ListTasks, ListNotes, ListReminders, ListAppointments, TodaySchedule, TomorrowSchedule, SearchNotes, SearchTasks, SearchReminders, SearchAppointments, RescheduleAppointment, CancelAction, Help, ChangeLanguage, Greeting, Confirmation, Denial, Unknown),
            title (string, keep original language, never translate), content, description, date (yyyy-MM-dd), time (HH:mm), endTime, durationMinutes (number), location, participants (array), recurrence (Once|Daily|Weekly|Monthly|Yearly), priority (Low|Medium|High|Urgent), status, searchQuery, category, language (the user's language code).
            If a field is unknown or not present, omit it or use null.
            """;

        var raw = await CompleteAsync(systemPrompt, text, 0.2, cancellationToken);
        return DeserializeCommand(raw, language);
    }

    public async Task<string> GenerateReplyAsync(string intent, Dictionary<string, object?>? data, string language, CancellationToken cancellationToken = default)
    {
        var context = data != null ? JsonSerializer.Serialize(data) : "{}";
        var systemPrompt = $"""
            You are a friendly multilingual personal assistant. Reply in the user's language (code: {language}).
            The user's intent was "{intent}" with this data: {context}.
            Confirm the action naturally and concisely in one or two short sentences, in the same language the user spoke.
            """;
        return (await CompleteAsync(systemPrompt, "Please generate the confirmation reply.", 0.7, cancellationToken)).Trim();
    }

    private async Task<string> CompleteAsync(string systemPrompt, string userMessage, double temperature, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = _options.Model,
            temperature,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        return json?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private ParsedCommand DeserializeCommand(string raw, string? language)
    {
        var cleaned = StripCodeFences(raw);
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            var dto = JsonSerializer.Deserialize<CommandJson>(cleaned, options);
            if (dto == null) throw new JsonException("null");
            return dto.ToParsedCommand(language);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize AI command JSON: {Raw}", cleaned);
            return new ParsedCommand { Intent = AssistantIntent.Unknown, Language = language };
        }
    }

    private static string StripCodeFences(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('\n') + 1;
            var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (start > 0 && end > start) trimmed = trimmed[start..end].Trim();
        }
        var braceStart = trimmed.IndexOf('{');
        var braceEnd = trimmed.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            trimmed = trimmed[braceStart..(braceEnd + 1)];
        }
        return trimmed;
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }

    private class ChatMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class CommandJson
    {
        public string? Intent { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Description { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }
        public string? EndTime { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Location { get; set; }
        public List<string>? Participants { get; set; }
        public string? Recurrence { get; set; }
        public string? Priority { get; set; }
        public string? Status { get; set; }
        public string? SearchQuery { get; set; }
        public string? Category { get; set; }
        public string? Language { get; set; }

        public ParsedCommand ToParsedCommand(string? fallbackLanguage)
        {
            var parsed = new ParsedCommand { Language = Language ?? fallbackLanguage };
            parsed.Intent = Enum.TryParse<AssistantIntent>(Intent, true, out var intent) ? intent : AssistantIntent.Unknown;
            parsed.Title = Title;
            parsed.Content = Content;
            parsed.Description = Description;
            parsed.SearchQuery = SearchQuery;
            parsed.Category = Category;
            parsed.Location = Location;
            parsed.Participants = Participants;
            parsed.DurationMinutes = DurationMinutes;
            if (DateTime.TryParse(Date, out var dt)) parsed.Date = dt;
            if (TimeOnly.TryParse(Time, out var time)) parsed.Time = time;
            if (DateTime.TryParse(EndTime, out var end)) parsed.EndDateTime = end;
            if (Enum.TryParse<RecurrenceType>(Recurrence, true, out var rec)) parsed.Recurrence = rec;
            if (Enum.TryParse<TaskPriority>(Priority, true, out var pri)) parsed.Priority = pri;
            if (parsed.Date.HasValue && parsed.Time.HasValue)
            {
                parsed.Date = new DateTime(parsed.Date.Value.Year, parsed.Date.Value.Month, parsed.Date.Value.Day,
                    parsed.Time.Value.Hour, parsed.Time.Value.Minute, 0);
                parsed.Time = null;
            }
            return parsed;
        }
    }
}
