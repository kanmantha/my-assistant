using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.AI;
using MyAssistant.Application.Interfaces;
using MyAssistant.Infrastructure.Services.AI;

namespace MyAssistant.Infrastructure.Services.AI;

/// <summary>OpenAI-compatible chat completion provider (works with OpenAI and Azure OpenAI).</summary>
public class OpenAiService : IAssistantAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(HttpClient http, string apiKey, string model, string endpoint, ILogger<OpenAiService> logger)
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
        _endpoint = endpoint;
        _logger = logger;
    }

    public async Task<IntentResult> DetectIntentAsync(AssistantRequest request)
    {
        var system = BuildSystemPrompt(request);
        var body = new
        {
            model = _model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = request.Text }
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = content;

        var resp = await _http.SendAsync(req);
        var respBody = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("AI provider returned {Status}: {Body}", (int)resp.StatusCode, respBody);
            return Fallback(request);
        }

        try
        {
            using var doc = JsonDocument.Parse(respBody);
            var contentStr = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            var parsed = JsonSerializer.Deserialize<IntentResult>(contentStr ?? "{}") ?? Fallback(request);
            parsed.RawJson = contentStr;
            parsed.OriginalText = request.Text;
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI provider response");
            return Fallback(request);
        }
    }

    public async Task<AssistantResult> ProcessCommandAsync(AssistantRequest request)
    {
        var intent = await DetectIntentAsync(request);
        return new AssistantResult
        {
            Success = true,
            Intent = intent.Intent,
            IntentData = intent,
            ResponseLanguage = intent.Language,
            NeedsClarification = intent.NeedsClarification,
            ClarificationQuestion = intent.ClarificationQuestion
        };
    }

    private static IntentResult Fallback(AssistantRequest request) => Parser.Parse(request.Text, request.UserId, request.Timezone);

    private static string BuildSystemPrompt(AssistantRequest request)
    {
        var now = DateTime.UtcNow;
        var supportedIntents = string.Join(", ", AssistantIntents.All);
        return $@"
You are the AI engine of 'My Assistant', a personal productivity assistant for India.
You MUST respond with STRICT JSON only, matching this schema exactly:
{{
  ""intent"": one of [{supportedIntents}],
  ""language"": ""en-IN|hi-IN|te-IN"",
  ""title"": ""entity title or null"",
  ""content"": ""note content or null"",
  ""description"": ""description or null"",
  ""date"": ""yyyy-MM-dd or null"",
  ""time"": ""HH:mm (24h, Asia/Kolkata) or null"",
  ""endDateTime"": ""ISO date or null"",
  ""durationMinutes"": int or null,
  ""reminderMinutes"": string or null,
  ""priority"": ""Low|Medium|High|Urgent or null"",
  ""status"": ""Pending|InProgress|Completed|Cancelled or null"",
  ""recurrence"": ""Once|Daily|Weekly|Monthly|Yearly|Custom or null"",
  ""location"": ""string or null"",
  ""participants"": [""name1"",""name2""] or null,
  ""tags"": [""tag1""] or null,
  ""query"": ""search query or null"",
  ""targetLanguage"": ""en-IN|hi-IN|te-IN or null"",
  ""needsClarification"": false,
  ""clarificationQuestion"": ""question or null"",
  ""id"": ""guid or null"",
  ""newTitle"": ""string or null""
}}

Rules:
- Current date (Asia/Kolkata): {now:yyyy-MM-dd}. Today is {now:dddd}.
- Resolve relative dates/times (tomorrow, next monday, in 30 minutes, tonight, tomorrow morning, at 6 PM) to concrete yyyy-MM-dd and HH:mm.
- Default appointment duration = 30 minutes when unspecified.
- If essential data is missing (e.g. a meeting with no time, or a reminder with no time), set needsClarification=true and ask ONE concise question in the user's language.
- Preserve user content verbatim (do not translate note/task text).
- Determine language from the user's message: Hindi script -> hi-IN, Telugu script -> te-IN, otherwise en-IN.
- For reminders and appointments, reminderMinutes defaults to ""15"".
Reply ONLY with the JSON object. No markdown, no commentary.";
    }
}

public class GeminiService : IAssistantAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(HttpClient http, string apiKey, string model, ILogger<GeminiService> logger)
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
    }

    public async Task<IntentResult> DetectIntentAsync(AssistantRequest request)
    {
        var system = OpenAiServiceBuildPrompt(request);
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var body = new
        {
            contents = new object[]
            {
                new { parts = new object[] { new { text = system + "\n\nUSER: " + request.Text } } }
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(endpoint, content);
        var respBody = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini returned {Status}", (int)resp.StatusCode);
            return Parser.Parse(request.Text, request.UserId, request.Timezone);
        }

        try
        {
            using var doc = JsonDocument.Parse(respBody);
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            text = CleanJson(text);
            var parsed = JsonSerializer.Deserialize<IntentResult>(text ?? "{}") ?? Parser.Parse(request.Text, request.UserId, request.Timezone);
            parsed.RawJson = text;
            parsed.OriginalText = request.Text;
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini parse failure");
            return Parser.Parse(request.Text, request.UserId, request.Timezone);
        }
    }

    public async Task<AssistantResult> ProcessCommandAsync(AssistantRequest request)
    {
        var intent = await DetectIntentAsync(request);
        return new AssistantResult { Success = true, Intent = intent.Intent, IntentData = intent, ResponseLanguage = intent.Language };
    }

    private static string CleanJson(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "{}";
        s = s.Trim();
        if (s.StartsWith("```")) s = s.Replace("```json", "").Replace("```", "").Trim();
        return s;
    }

    private static string OpenAiServiceBuildPrompt(AssistantRequest request)
        => "You convert natural language to JSON. Current date Asia/Kolkata: " + DateTime.UtcNow.ToString("yyyy-MM-dd") +
           ". Respond only with JSON: {\"intent\": string, \"language\": \"en-IN|hi-IN|te-IN\", \"title\": string|null, \"content\": string|null, \"description\": string|null, \"date\": \"yyyy-MM-dd\"|null, \"time\": \"HH:mm\"|null, \"durationMinutes\": int|null, \"priority\": string|null, \"status\": string|null, \"recurrence\": string|null, \"location\": string|null, \"participants\": [string]|null, \"query\": string|null, \"targetLanguage\": string|null, \"needsClarification\": bool, \"clarificationQuestion\": string|null}. Intents: " + string.Join(", ", AssistantIntents.All);
}

public class AzureOpenAiService : IAssistantAiService
{
    private readonly IAssistantAiService _inner;
    public AzureOpenAiService(IAssistantAiService inner) => _inner = inner;
    public Task<IntentResult> DetectIntentAsync(AssistantRequest request) => _inner.DetectIntentAsync(request);
    public Task<AssistantResult> ProcessCommandAsync(AssistantRequest request) => _inner.ProcessCommandAsync(request);
}