using Microsoft.Extensions.Logging;
using MyAssistant.Application.AI;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services.AI;

/// <summary>
/// Deterministic, rule-based AI used when no paid provider key is configured.
/// Handles all intents across English, Hindi and Telugu plus relative date/time
/// expressions (today, tomorrow, next Monday, in 30 minutes, tonight, tomorrow morning, etc.).
/// </summary>
public class MockAiService : IAssistantAiService
{
    private readonly ILogger<MockAiService> _logger;

    public MockAiService(ILogger<MockAiService> logger) => _logger = logger;

    public Task<IntentResult> DetectIntentAsync(AssistantRequest request)
    {
        var result = Parser.Parse(request.Text, request.UserId, request.Timezone ?? "Asia/Kolkata");
        result.OriginalText = request.Text;
        result.RawJson = "{}";
        return Task.FromResult(result);
    }

    public async Task<AssistantResult> ProcessCommandAsync(AssistantRequest request)
    {
        var intent = await DetectIntentAsync(request);
        return new AssistantResult
        {
            Success = true,
            Intent = intent.Intent,
            IntentData = intent,
            ResponseLanguage = intent.Language
        };
    }
}

/// <summary>Parses natural-language commands into structured intents.</summary>
public static class Parser
{
    public static IntentResult Parse(string text, Guid userId, string timezone)
    {
        var lang = LanguageDetector.Detect(text);
        var result = new IntentResult { Language = lang, OriginalText = text };

        var ctx = new CommandContext(Normalize(text), lang);
        ctx.StripWakeWord();

        // Confirm / cancel / help
        if (ctx.Short && ctx.HasAny("yes", "yeah", "sure", "हाँ", "ठीक है", "అవును", "ఔను"))
        {
            result.Intent = AssistantIntents.Confirm;
            return result;
        }
        if (ctx.Short && ctx.HasAny("no", "nahi", "नहीं", "కాదు"))
        {
            result.Intent = AssistantIntents.Cancel;
            return result;
        }
        if (ctx.HasAny("help", "what can you do", "सहायता", "मदद", "సహాయం"))
        {
            result.Intent = AssistantIntents.Help;
            return result;
        }

        // Language change
        if (ctx.HasAny("language", "भाषा", "భాష") && ctx.HasAny("change", "switch", "बदल", "మార్చ", "మాట్లాడు"))
        {
            result.Intent = AssistantIntents.ChangeLanguage;
            result.TargetLanguage = ResolveTargetLanguage(ctx);
            return result;
        }

        // Notes (checked before appointments — "take a note about meeting" is a note, not an appointment)
        if (ctx.HasAny("note", "नोट", "గమనిక"))
        {
            if (ctx.HasAny("delete", "remove", "हटाओ", "తొలగించు"))
            {
                result.Intent = AssistantIntents.DeleteNote;
                result.Title = ctx.ExtractTitle();
                return result;
            }
            if (ctx.HasAny("show", "list", "search", "दिखाओ", "చూపించు"))
            {
                result.Intent = AssistantIntents.SearchNotes;
                result.Query = ctx.ExtractQuery() ?? ctx.ExtractTitle();
                return result;
            }
            result.Intent = AssistantIntents.CreateNote;
            result.Title = ctx.ExtractTitle();
            result.Content = ctx.ExtractContent();
            result.Tags = ctx.ExtractParticipants();
            return result;
        }

        // Tasks (checked before appointments — "create a task for meeting" is a task, not an appointment)
        if (ctx.HasAny("task", "to-do", "todo", "काम", "కార్యం", "పని", "టాస్క్"))
        {
            if (ctx.HasAny("complete", "mark as done", "finish", "completed", "as done", "done", "पूरा", "పూర్తి"))
            {
                result.Intent = AssistantIntents.CompleteTask;
                result.Title = ctx.ExtractTitle();
                return result;
            }
            if (ctx.HasAny("delete", "remove", "हटाओ", "తొలగించు"))
            {
                result.Intent = AssistantIntents.DeleteTask;
                result.Title = ctx.ExtractTitle();
                return result;
            }
            if (ctx.HasAny("show", "list", "my", "मेरे", "జాబితా"))
            {
                result.Intent = AssistantIntents.ListTasks;
                return result;
            }
            result.Intent = AssistantIntents.CreateTask;
            result.Title = ctx.ExtractTitle();
            result.Description = ctx.ExtractContent();
            result.Date = DateTimeResolver.ParseDate(ctx);
            result.Time = DateTimeResolver.ParseTime(ctx);
            result.Priority = PriorityParser.Parse(ctx);
            return result;
        }

        // Appointments / meetings
        if (ctx.HasAny("appointment", "meeting", "schedule a", "book", "अपॉइंटमेंट", "మీటింగ్", "అపాయింట్మెంట్"))
        {
            if (ctx.HasAny("delete", "cancel the meeting", "remove", "हटाओ", "రద్దు", "తొలగించు"))
            {
                result.Intent = AssistantIntents.DeleteAppointment;
                result.Title = ctx.ExtractTitle();
                return result;
            }
            if (ctx.HasAny("update", "reschedule", "change", "बदलो", "మార్చు"))
            {
                result.Intent = AssistantIntents.UpdateAppointment;
                result.Title = ctx.ExtractTitle();
                return result;
            }
            result.Intent = AssistantIntents.CreateAppointment;
            result.Title = ctx.ExtractTitle();
            result.Date = DateTimeResolver.ParseDate(ctx);
            result.Time = DateTimeResolver.ParseTime(ctx);
            result.DurationMinutes = DurationParser.Parse(ctx) ?? 30;
            result.Location = ctx.ExtractLocation();
            result.Participants = ctx.ExtractParticipants();
            return result;
        }

        // Schedule queries
        if (ctx.HasAny("schedule", "शेड्यूल", "షెడ్యూల్"))
        {
            if (ctx.HasAny("today", "आज", "ఈరోజు", "ఈ రోజు"))
                result.Intent = AssistantIntents.GetTodaySchedule;
            else if (ctx.HasAny("tomorrow", "कल", "రేపు"))
                result.Intent = AssistantIntents.GetTomorrowSchedule;
            else if (ctx.HasAny("upcoming", "next", "आने वाला", "ముందు"))
                result.Intent = AssistantIntents.GetUpcomingSchedule;
            else
                result.Intent = AssistantIntents.GetTodaySchedule;
            return result;
        }

        // Reminders
        if (ctx.HasAny("remind", "reminder", "याद दिलाना", "याद दिलाओ", "యాద్", "రిమైండర్", "గుర్తు"))
        {
            if (ctx.HasAny("delete", "remove", "हटाओ", "తొలగించు"))
            {
                result.Intent = AssistantIntents.DeleteReminder;
                result.Title = ctx.ExtractTitle();
                return result;
            }
            if (ctx.HasAny("update", "change", "बदलो", "మార్చు"))
            {
                result.Intent = AssistantIntents.UpdateReminder;
                result.Title = ctx.ExtractTitle();
                return result;
            }
            result.Intent = AssistantIntents.CreateReminder;
            result.Title = ctx.ExtractTitle();
            result.Date = DateTimeResolver.ParseDate(ctx);
            result.Time = DateTimeResolver.ParseTime(ctx);
            result.Recurrence = RecurrenceParser.Parse(ctx);
            return result;
        }

        // Generic search
        if (ctx.HasAny("search", "find", "look for", "खोज", "వెతుకు"))
        {
            result.Intent = AssistantIntents.Search;
            result.Query = ctx.ExtractQuery() ?? ctx.ExtractTitle();
            return result;
        }

        // Fallback
        result.Intent = AssistantIntents.Help;
        return result;
    }

    private static string Normalize(string text)
        => text.Trim().ToLowerInvariant().Replace('’', '\'').Replace('‘', '\'');

    private static string ResolveTargetLanguage(CommandContext ctx)
    {
        if (ctx.HasAny("hindi", "हिन्दी", "हिंदी")) return "hi-IN";
        if (ctx.HasAny("telugu", "తెలుగు", "तेलुगु")) return "te-IN";
        if (ctx.HasAny("english", "अंग्रेजी", "ఇంగ్లీషు")) return "en-IN";
        return ctx.Language;
    }
}

public static class LanguageDetector
{
    public static string Detect(string text)
    {
        int devanagari = 0, telugu = 0, latin = 0;
        foreach (var ch in text)
        {
            if (ch is >= '\u0900' and <= '\u097F') devanagari++;
            else if (ch is >= '\u0C00' and <= '\u0C7F') telugu++;
            else if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z') latin++;
        }
        if (devanagari > telugu && devanagari > 0) return "hi-IN";
        if (telugu > devanagari && telugu > 0) return "te-IN";
        return "en-IN";
    }
}