using System.Text.RegularExpressions;
using MyAssistant.Application.DTOs.Assistant;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.Services;

public class HeuristicAIService : IAssistantAIService
{
    private readonly IDateTimeParser _dateTimeParser;

    // Matches the start of a date/time phrase after a preposition, e.g.
    // "by tomorrow at 5 PM", "due Friday at 3 PM", "scheduled for next Monday",
    // "on Saturday", "at 5 PM". Used to strip schedule text out of task titles.
    private static readonly Regex ScheduleStartRegex = new(
        @"\b(?:by|due\s+on|due|on|at|for|from|until|scheduled(?:\s+(?:for|on))?)\s+(?=(?:tomorrow|today|tonight|next\b|this\b|(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*\b|\d{1,2}(?::\d{2})?\s*(?:a\.?m\.?|p\.?m\.?)|\d{1,2}(?:st|nd|rd|th)?\s+(?:of\s+)?(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*|\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{1,2}\s*(?:am|pm)|midnight|noon))",
        RegexOptions.IgnoreCase);

    // Matches a bare trailing date/time phrase with no preposition, e.g. "buy groceries tomorrow",
    // "meeting next monday", "call at 5 PM" (handled by ScheduleStartRegex instead when "at" present).
    private static readonly Regex BareTailRegex = new(
        @"\b(?:(?:tomorrow|today|tonight|next\s+\w+|this\s+\w+)|(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*|\d{1,2}(?::\d{2})?\s*(?:a\.?m\.?|p\.?m\.?)|\d{1,2}(?:st|nd|rd|th)?\s+(?:of\s+)?(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*|\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\s*$",
        RegexOptions.IgnoreCase);

    public HeuristicAIService(IDateTimeParser dateTimeParser)
    {
        _dateTimeParser = dateTimeParser;
    }

    /// <summary>
    /// Removes trailing date/time scheduling phrases from a title so that
    /// "buy groceries by tomorrow at 5 PM" becomes "buy groceries" (the parsed
    /// date/time is stored separately as the due date).
    /// </summary>
    private static string StripScheduleSuffix(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var cut = -1;
        var m = ScheduleStartRegex.Match(raw);
        if (m.Success) cut = m.Index;
        if (cut < 0)
        {
            var tail = BareTailRegex.Match(raw);
            if (tail.Success) cut = tail.Index;
        }
        return cut > 0 ? raw[..cut].TrimEnd(' ', ',', '-', ':') : raw;
    }

    public Task<string> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult("en-IN");
        if (text.Any(c => c >= 0x0900 && c <= 0x097F)) return Task.FromResult("hi-IN");
        if (text.Any(c => c >= 0x0C00 && c <= 0x0C7F)) return Task.FromResult("te-IN");
        return Task.FromResult("en-IN");
    }

    public async Task<ParsedCommand> ParseCommandAsync(string text, string? language, string timeZone, CancellationToken cancellationToken = default)
    {
        var parsed = new ParsedCommand { Language = language };
        var t = text.Trim();
        var lower = t.ToLowerInvariant();
        var lang = (await DetectLanguageAsync(t, cancellationToken));
        if (lang.StartsWith("hi")) lang = "hi";
        else if (lang.StartsWith("te")) lang = "te";
        else lang = "en";

        if (IsChangeLanguage(t, lang, out var newLang))
        {
            parsed.Intent = AssistantIntent.ChangeLanguage;
            parsed.Language = newLang;
            return parsed;
        }

        if (IsGreeting(lower, lang))
        {
            parsed.Intent = AssistantIntent.Greeting;
            return parsed;
        }

        if (IsHelp(lower, lang))
        {
            parsed.Intent = AssistantIntent.Help;
            return parsed;
        }

        if (IsDelete(lower, lang))
        {
            parsed.Intent = AssistantIntent.DeleteTask;
            var afterAction = ExtractAfterDelete(t, lang);
            parsed.Title = CleanTitle(afterAction, lang);
            return parsed;
        }

        if (IsCompleteTask(lower, lang))
        {
            parsed.Intent = AssistantIntent.CompleteTask;
            parsed.Title = CleanTitle(ExtractAfter(lower, lang, new[] { "mark", "mark ", "complete", "completed", "finish", "done", "पूरा", "पूरी", "పూర్తి" }, t), lang);
            return parsed;
        }

        if (IsList(lower, lang, out var listIntent, out var listScope))
        {
            parsed.Intent = listIntent;
            parsed.Scope = listScope;
            if (listIntent == AssistantIntent.ListTasks && (lower.Contains("pending") || lower.Contains("बाकी") || lower.Contains("జరగాల్సిన")))
            {
                parsed.Status = TaskStatus.Pending;
            }
            return parsed;
        }

        if (IsTodaySchedule(lower, lang, out var tomorrow))
        {
            parsed.Intent = tomorrow ? AssistantIntent.TomorrowSchedule : AssistantIntent.TodaySchedule;
            return parsed;
        }

        if (IsWeather(lower, lang))
        {
            parsed.Intent = AssistantIntent.Weather;
            parsed.Title = t;
            return parsed;
        }

        if (IsWebSearch(lower, lang))
        {
            parsed.Intent = AssistantIntent.WebSearch;
            parsed.Title = t;
            parsed.SearchQuery = ExtractSearchQuery(t, lang);
            return parsed;
        }

        if (IsSearch(lower, lang, out var searchIntent, out var searchQuery))
        {
            parsed.Intent = searchIntent;
            parsed.SearchQuery = searchQuery;
            return parsed;
        }

        var dateResult = await _dateTimeParser.ParseAsync(t, lang, timeZone, cancellationToken);
        parsed.Date = dateResult.DateTime;
        parsed.DurationMinutes = dateResult.DurationMinutes;
        parsed.Time = dateResult.HasTime && dateResult.DateTime.HasValue ? TimeOnly.FromDateTime(dateResult.DateTime.Value) : null;

        if (IsNote(lower, lang))
        {
            parsed.Intent = AssistantIntent.CreateNote;
            var (title, content) = ExtractNote(t, lang);
            parsed.Title = title;
            parsed.Content = content;
            return parsed;
        }

        if (IsAppointment(lower, lang))
        {
            parsed.Intent = AssistantIntent.CreateAppointment;
            parsed.Title = ExtractMeetingTitle(t, lang, dateResult.DateTime.HasValue && dateResult.DateTime.Value.Date != DateTime.MinValue);
            parsed.Participants = ExtractParticipants(t, lang);
            parsed.Location = ExtractLocation(lower);
            parsed.DurationMinutes = parsed.DurationMinutes ?? 30;
            parsed.NeedsConfirmation = true;
            return parsed;
        }

        if (IsReminder(lower, lang))
        {
            parsed.Intent = AssistantIntent.CreateReminder;
            parsed.Title = ExtractReminderTitle(t, lang);
            parsed.Recurrence = ExtractRecurrence(lower, lang);
            parsed.Content = parsed.Title;
            if (parsed.Time.HasValue && parsed.Date.HasValue)
            {
                parsed.Date = new DateTime(parsed.Date.Value.Year, parsed.Date.Value.Month, parsed.Date.Value.Day,
                    parsed.Time.Value.Hour, parsed.Time.Value.Minute, 0);
                parsed.Time = null;
            }
            return parsed;
        }

        if (IsTask(lower, lang))
        {
            parsed.Intent = AssistantIntent.CreateTask;
            parsed.Title = ExtractTaskTitle(t, lang);
            return parsed;
        }

        if (IsCancel(lower, lang))
        {
            parsed.Intent = AssistantIntent.CancelAction;
            return parsed;
        }

        if (IsGeneralQuestion(lower))
        {
            parsed.Intent = AssistantIntent.GeneralQuestion;
            parsed.Title = t;
            return parsed;
        }

        parsed.Intent = AssistantIntent.Unknown;
        return parsed;
    }

    public Task<string> GenerateReplyAsync(string intent, Dictionary<string, object?>? data, string language, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<string> AnswerQuestionAsync(string question, string language, CancellationToken cancellationToken = default)
    {
        var lang = language?.ToLowerInvariant().StartsWith("hi") == true ? "hi" :
                   language?.ToLowerInvariant().StartsWith("te") == true ? "te" : "en";
        return Task.FromResult(lang switch
        {
            "hi" => "मैं अभी सामान्य सवालों का जवाब नहीं दे सकता क्योंकि ऑनलाइन सहायक सेवा उपलब्ध नहीं है। आप मुझसे नोट्स, कार्य, रिमाइंडर और मीटिंग्स के बारे में बात कर सकते हैं।",
            "te" => "ఆన్‌లైన్ సహాయక సేవ అందుబాటులో లేనందున నేను ఇప్పుడు సాధారణ ప్రశ్నలకు సమాధానం ఇవ్వలేను. నోట్స్, పనులు, రిమైండర్‌లు మరియు మీటింగ్‌ల గురించి మీరు నాతో మాట్లాడవచ్చు.",
            _ => "I can't answer general questions right now because the online assistant service isn't available. You can talk to me about notes, tasks, reminders and meetings."
        });
    }

    private static bool IsGreeting(string lower, string lang) => lang switch
    {
        "hi" => lower.StartsWith("हैलो") || lower.StartsWith("नमस्ते") || lower.StartsWith("हाय"),
        "te" => lower.StartsWith("హలో") || lower.StartsWith("నమస్కారం") || lower.StartsWith("హాయ్"),
        _ => lower.StartsWith("hello") || lower.StartsWith("hi ") || lower == "hi" || lower.StartsWith("hey") ||
             lower.StartsWith("good morning") || lower.StartsWith("good afternoon") || lower.StartsWith("good evening") ||
             lower.StartsWith("good night") || lower.StartsWith("thanks") || lower.StartsWith("thank you") ||
             lower == "thanks" || lower == "thank you"
    };

    private static bool IsHelp(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("मदद") || lower.Contains("क्या कर सकते"),
        "te" => lower.Contains("సహాయం") || lower.Contains("ఏం చేయ"),
        _ => lower.Contains("help") || lower.Contains("what can you do")
    };

    private static bool IsCancel(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("रद्द"),
        "te" => lower.Contains("రద్దు"),
        _ => lower.Contains("cancel") || lower.Contains("stop") || lower.Contains("never mind")
    };

    private static bool IsWeather(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("मौसम") || lower.Contains("मौसम का हाल") || lower.Contains("तापमान") || lower.Contains("बारिश होगी"),
        "te" => lower.Contains("వాతావరణం") || lower.Contains("వాతావరణ") || lower.Contains("ఉష్ణోగ్రత") || lower.Contains("వర్షం"),
        _ => lower.Contains("weather") || lower.Contains("temperature") || lower.Contains("forecast") || lower.Contains("raining") || lower.Contains("rain today")
    };

    private static bool IsWebSearch(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("खोजो") || lower.Contains("ढूंढो") || lower.Contains("गूगल"),
        "te" => lower.Contains("వెతకండి") || lower.Contains("వెతక") || lower.Contains("గూగుల్"),
        _ => lower.Contains("search the web") || lower.Contains("google ") || lower.Contains("look up") || lower.StartsWith("what is ") || lower.StartsWith("what are ") || lower.StartsWith("who is ") || lower.StartsWith("how do ") || lower.StartsWith("how does ")
    };

    private static bool IsGeneralQuestion(string lower)
    {
        return lower.EndsWith("?") ||
               lower.StartsWith("why ") || lower.StartsWith("when ") || lower.StartsWith("where ") ||
               lower.StartsWith("which ") || lower.StartsWith("who is ") || lower.StartsWith("what is ") ||
               lower.StartsWith("explain ") || lower.StartsWith("tell me about ") ||
               lower.Contains("how many") || lower.Contains("how much") || lower.Contains("how far");
    }

    private static string ExtractSearchQuery(string text, string lang)
    {
        if (lang == "hi")
        {
            var idx = text.IndexOf("ढूंढो", StringComparison.Ordinal);
            if (idx < 0) idx = text.IndexOf("खोजो", StringComparison.Ordinal);
            return idx >= 0 ? text[(idx + 3)..].Trim() : text;
        }
        if (lang == "te")
        {
            var idx = text.IndexOf("వెతకండి", StringComparison.Ordinal);
            if (idx < 0) idx = text.IndexOf("వెతక", StringComparison.Ordinal);
            return idx >= 0 ? text[(idx + 4)..].Trim() : text;
        }
        foreach (var prefix in new[] { "search the web for ", "search for ", "look up ", "google " })
        {
            var idx = text.ToLowerInvariant().IndexOf(prefix, StringComparison.Ordinal);
            if (idx >= 0) return text[(idx + prefix.Length)..].Trim();
        }
        if (text.StartsWith("what is ", StringComparison.OrdinalIgnoreCase)) return text[8..].Trim();
        if (text.StartsWith("what are ", StringComparison.OrdinalIgnoreCase)) return text[9..].Trim();
        if (text.StartsWith("who is ", StringComparison.OrdinalIgnoreCase)) return text[7..].Trim();
        if (text.StartsWith("how do ", StringComparison.OrdinalIgnoreCase)) return text[7..].Trim();
        if (text.StartsWith("how does ", StringComparison.OrdinalIgnoreCase)) return text[9..].Trim();
        return text;
    }

    private static bool IsChangeLanguage(string text, string lang, out string newLang)
    {
        newLang = "en-IN";
        var lower = text.ToLowerInvariant();
        if (lang == "hi")
        {
            if (lower.Contains("अंग्रेजी") || lower.Contains("इंग्लिश")) { newLang = "en-IN"; return true; }
            if (lower.Contains("हिंदी") || lower.Contains("हिन्दी")) { newLang = "hi-IN"; return true; }
            if (lower.Contains("तेलुगु")) { newLang = "te-IN"; return true; }
            if (lower.Contains("भाषा")) { newLang = "hi-IN"; return true; }
        }
        else if (lang == "te")
        {
            if (lower.Contains("తెలుగు")) { newLang = "te-IN"; return true; }
            if (lower.Contains("ఇంగ్లీష్") || lower.Contains("ఆంగ్ల")) { newLang = "en-IN"; return true; }
            if (lower.Contains("హిందీ")) { newLang = "hi-IN"; return true; }
            if (lower.Contains("భాష")) { newLang = "te-IN"; return true; }
        }
        else
        {
            if (lower.Contains("speak hindi") || lower.Contains("language to hindi") || lower.Contains("switch to hindi") || lower.Contains("in hindi")) { newLang = "hi-IN"; return true; }
            if (lower.Contains("speak telugu") || lower.Contains("language to telugu") || lower.Contains("switch to telugu") || lower.Contains("in telugu")) { newLang = "te-IN"; return true; }
            if (lower.Contains("change language") || lower.Contains("switch to english") || lower.Contains("speak english") || lower.Contains("in english")) { newLang = "en-IN"; return true; }
        }
        return false;
    }

    private static bool IsDelete(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("हटाओ") || lower.Contains("हटाना") || lower.Contains("डिलीट") || lower.Contains("मिटाओ") || lower.Contains("हटा दो"),
        "te" => lower.Contains("తొలగించు") || lower.Contains("తీసేయి") || lower.Contains("తొలగించండి") || lower.Contains("డిలీట్"),
        _ => lower.Contains("delete") || lower.Contains("remove")
    };

    private static bool IsCompleteTask(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("पूरा") || lower.Contains("पूरी"),
        "te" => lower.Contains("పూర్తి") || lower.Contains("పూర్తయింది"),
        _ => lower.Contains("complete task") || lower.Contains("complete the task") ||
             ((lower.Contains("mark") || lower.Contains("finish")) && (lower.Contains("completed") || lower.Contains("complete") || lower.Contains("done"))) ||
             lower.StartsWith("done with") || lower.StartsWith("mark done")
    };

    private static bool IsNote(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("नोट") || lower.Contains("लिखो"),
        "te" => lower.Contains("నోట్") || lower.Contains("రాయి"),
        _ => lower.Contains("take a note") || lower.Contains("add a note") || lower.Contains("create a note") ||
             lower.Contains("write a note") || lower.Contains("add note") || lower.Contains("create note") ||
             lower.Contains("write note") || lower.Contains("take note") ||
             lower.Contains(" note:") || lower.StartsWith("note ") || lower.StartsWith("notes ")
    };

    private static bool IsTask(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("कार्य") || lower.Contains("काम जोड़ो") || lower.Contains("टास्क"),
        "te" => lower.Contains("పని జోడించు") || lower.Contains("టాస్క్") || lower.Contains("పని"),
        _ => lower.Contains("add a task") || lower.Contains("create a task") || lower.Contains("add task") ||
             lower.Contains("create task") || lower.Contains("to-do") || lower.Contains("todo") ||
             lower.StartsWith("task ") || lower.StartsWith("tasks ")
    };

    private static bool IsReminder(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("याद दिलाना") || lower.Contains("याद दिलाओ") || lower.Contains("रिमाइंडर") || lower.Contains("याद दिला दो"),
        "te" => lower.Contains("గుర్తు చేయి") || lower.Contains("రిమైండర్") || lower.Contains("గుర్తుచేయి"),
        _ => lower.Contains("remind me") || lower.Contains("reminder") || lower.Contains("remember")
    };

    private static bool IsAppointment(string lower, string lang) => lang switch
    {
        "hi" => lower.Contains("मीटिंग") || lower.Contains("अपॉइंटमेंट") || lower.Contains("शेड्यूल") || lower.Contains("मिलना है"),
        "te" => lower.Contains("మీటింగ్") || lower.Contains("అపాయింట్") || lower.Contains("షెడ్యూల్") || lower.Contains("కలవాలి"),
        _ => !(lower.StartsWith("add a task") || lower.StartsWith("create a task") || lower.Contains("task called") || lower.Contains("task titled")) &&
             (lower.Contains("schedule") || lower.Contains("meeting") || lower.Contains("appointment") ||
              lower.Contains("meet with"))
    };

    private static bool IsTodaySchedule(string lower, string lang, out bool tomorrow)
    {
        tomorrow = false;
        if (lower.StartsWith("add") || lower.StartsWith("create") || lower.StartsWith("remind") ||
            lower.StartsWith("mark") || lower.StartsWith("delete") || lower.StartsWith("remove") ||
            lower.StartsWith("schedule") || lower.StartsWith("set a reminder") ||
            lower.Contains("task called") || lower.Contains("task titled") || lower.Contains("meeting with"))
        {
            return false;
        }
        if (lang == "hi")
        {
            if (lower.Contains("कल का") || lower.Contains("कल मेरे")) { tomorrow = true; return true; }
            if (lower.Contains("आज")) return true;
        }
        else if (lang == "te")
        {
            if (lower.Contains("రేపు")) { tomorrow = true; return true; }
            if (lower.Contains("ఈరోజు") || lower.Contains("నేడు")) return true;
        }
        else
        {
            if (lower.Contains("tomorrow")) { tomorrow = true; return true; }
            if (lower.Contains("today") && (lower.Contains("schedule") || lower.Contains("plan") || lower.Contains("scheduled") || lower.Contains("tasks today")))
            {
                return true;
            }
            if (lower.Contains("what do i have scheduled") || lower.Contains("what is my schedule") ||
                lower.Contains("what does today look like") || lower.Contains("what's today look like") ||
                lower.Contains("whats on today") || lower.Contains("what's on today"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsList(string lower, string lang, out AssistantIntent intent, out string? scope)
    {
        intent = AssistantIntent.Unknown;
        scope = null;
        var isToday = lower.Contains("today") || lower.Contains("today's") || lower.Contains("todays");
        var isTomorrow = lower.Contains("tomorrow");
        if (lang == "hi")
        {
            if ((lower.Contains("कार्य") || lower.Contains("काम") || lower.Contains("टास्क")) && (lower.Contains("दिखाओ") || lower.Contains("सूची") || lower.StartsWith("मेरे कार्य") || lower.StartsWith("मेरी टास्क"))) { intent = AssistantIntent.ListTasks; scope = isToday ? "today" : isTomorrow ? "tomorrow" : null; return true; }
            if (lower.Contains("रिमाइंडर") && (lower.Contains("दिखाओ") || lower.StartsWith("मेरे") || lower.StartsWith("सारे"))) { intent = AssistantIntent.ListReminders; scope = isToday ? "today" : isTomorrow ? "tomorrow" : null; return true; }
            if (lower.Contains("नोट") && (lower.Contains("दिखाओ") || lower.StartsWith("मेरे") || lower.Contains("सूची"))) { intent = AssistantIntent.ListNotes; return true; }
            if (lower.Contains("मीटिंग") && (lower.Contains("दिखाओ") || lower.StartsWith("मेरे"))) { intent = AssistantIntent.ListAppointments; scope = isToday ? "today" : isTomorrow ? "tomorrow" : null; return true; }
        }
        else if (lang == "te")
        {
            if ((lower.Contains("పనులు") || lower.Contains("టాస్క్")) && (lower.Contains("చూపించు") || lower.StartsWith("నా"))) { intent = AssistantIntent.ListTasks; scope = isToday ? "today" : isTomorrow ? "tomorrow" : null; return true; }
            if (lower.Contains("రిమైండర్") && (lower.Contains("చూపించు") || lower.StartsWith("నా"))) { intent = AssistantIntent.ListReminders; scope = isToday ? "today" : isTomorrow ? "tomorrow" : null; return true; }
            if ((lower.Contains("నోట్స్") || lower.Contains("నోట్ల")) && (lower.Contains("చూపించు") || lower.StartsWith("నా"))) { intent = AssistantIntent.ListNotes; return true; }
            if (lower.Contains("మీటింగ్") && (lower.Contains("చూపించు") || lower.StartsWith("నా"))) { intent = AssistantIntent.ListAppointments; scope = isToday ? "today" : isTomorrow ? "tomorrow" : null; return true; }
        }
        else
        {
            var creating = lower.StartsWith("add") || lower.StartsWith("create") || lower.StartsWith("set") ||
                           lower.StartsWith("remind") || lower.StartsWith("book") || lower.StartsWith("schedule") ||
                           lower.StartsWith("mark") || lower.StartsWith("delete") || lower.StartsWith("remove");
            if (!creating && isToday && (lower.Contains("reminder") || lower.Contains("reminders")))
            {
                intent = AssistantIntent.ListReminders;
                scope = "today";
                return true;
            }
            if (!creating && isTomorrow && (lower.Contains("reminder") || lower.Contains("reminders")))
            {
                intent = AssistantIntent.ListReminders;
                scope = "tomorrow";
                return true;
            }
            if (!creating && isToday && (lower.Contains("appointment") || lower.Contains("appointments") || lower.Contains("meeting") || lower.Contains("meetings")))
            {
                intent = AssistantIntent.ListAppointments;
                scope = "today";
                return true;
            }
            if (!creating && isTomorrow && (lower.Contains("appointment") || lower.Contains("appointments") || lower.Contains("meeting") || lower.Contains("meetings")))
            {
                intent = AssistantIntent.ListAppointments;
                scope = "tomorrow";
                return true;
            }
            if (!creating && isToday && (lower.Contains("task") || lower.Contains("tasks")))
            {
                intent = AssistantIntent.ListTasks;
                scope = "today";
                return true;
            }
            if (!creating && isTomorrow && (lower.Contains("task") || lower.Contains("tasks")))
            {
                intent = AssistantIntent.ListTasks;
                scope = "tomorrow";
                return true;
            }
            if (!creating && lower.Contains("calendar"))
            {
                intent = AssistantIntent.ListAppointments;
                return true;
            }
            if (!creating && (lower.Contains("task") || lower.Contains("tasks")) && lower.Contains("due") &&
                (lower.Contains("today") || lower.Contains("tomorrow") || lower.Contains("week") || lower.Contains("this month")))
            {
                intent = AssistantIntent.ListTasks;
                return true;
            }
            if (lower.Contains("pending tasks") || lower.Contains("list tasks") || lower.Contains("show my tasks") ||
                lower.Contains("my tasks") || lower.Contains("what are my tasks") || lower.Contains("list my tasks"))
            {
                intent = AssistantIntent.ListTasks;
                return true;
            }
            if (lower.Contains("list reminders") || lower.Contains("show my reminders") || lower.Contains("my reminders"))
            {
                intent = AssistantIntent.ListReminders;
                return true;
            }
            if (lower.Contains("list notes") || lower.Contains("show my notes") || lower.Contains("my notes") || lower.Contains("show notes"))
            {
                intent = AssistantIntent.ListNotes;
                return true;
            }
            if (lower.Contains("list appointments") || lower.Contains("show my appointments") || lower.Contains("my appointments") ||
                lower.Contains("show my meetings") || lower.Contains("list my meetings"))
            {
                intent = AssistantIntent.ListAppointments;
                return true;
            }
        }
        return false;
    }

    private static bool IsSearch(string lower, string lang, out AssistantIntent intent, out string query)
    {
        intent = AssistantIntent.Unknown;
        query = string.Empty;
        var match = Regex.Match(lower, @"(?:search|find|look for)\s+(?:my\s+)?(notes|tasks|reminders|appointments|meetings)\s+(?:for|about|with)?\s*(.*)$");
        if (lang == "en" && match.Success)
        {
            intent = match.Groups[1].Value switch
            {
                "notes" => AssistantIntent.SearchNotes,
                "tasks" => AssistantIntent.SearchTasks,
                "reminders" => AssistantIntent.SearchReminders,
                _ => AssistantIntent.SearchAppointments
            };
            query = match.Groups[2].Value.Trim();
            return true;
        }
        var generic = Regex.Match(lower, @"(?:search|find|look for)\s+(?:my\s+)?(?:note|notes|task|tasks|appointment|appointments|meeting|meetings|reminder|reminders)?\s*[:\s]?(?:about|for)?\s*(.+)");
        if (lang == "en" && generic.Success)
        {
            var firstWord = generic.Groups[1].Value.Trim().ToLowerInvariant().Split(' ')[0].TrimEnd('s', ' ');
            intent = firstWord switch
            {
                "note" => AssistantIntent.SearchNotes,
                "task" => AssistantIntent.SearchTasks,
                "reminder" => AssistantIntent.SearchReminders,
                "appointment" or "meeting" => AssistantIntent.SearchAppointments,
                _ => AssistantIntent.SearchNotes
            };
            query = generic.Groups[1].Value.Trim();
            return true;
        }
        if (lang == "hi" && (lower.Contains("नोट") && lower.Contains("खोजो") || lower.Contains("नोट्स के बारे में")))
        {
            intent = AssistantIntent.SearchNotes;
            var idx = lower.IndexOf("के बारे में", StringComparison.Ordinal);
            query = idx > 0 ? lower[(idx + "के बारे में".Length)..].Trim() : lower;
            return true;
        }
        if (lang == "te" && lower.Contains("నోట్స్") && (lower.Contains("వెతక") || lower.Contains("గురించి")))
        {
            intent = AssistantIntent.SearchNotes;
            var idx = lower.IndexOf("గురించి", StringComparison.Ordinal);
            query = idx > 0 ? lower[(idx + "గురించి".Length)..].Trim() : lower;
            return true;
        }
        return false;
    }

    private static string ExtractAfter(string lower, string lang, string[] prefixes, string original)
    {
        var taskPrefix = Regex.Match(lower, @"(?:complete|finish|mark)\s+(?:the\s+)?task\s+(.+)");
        if (taskPrefix.Success) return CleanTitle(StripScheduleSuffix(taskPrefix.Groups[1].Value.Trim()), lang);
        foreach (var prefix in prefixes)
        {
            var idx = lower.IndexOf(prefix, StringComparison.Ordinal);
            if (idx >= 0)
            {
                return CleanTitle(StripScheduleSuffix(original.Substring(idx + prefix.Length).Trim()), lang);
            }
        }
        return original;
    }

    private static string ExtractAfterDelete(string text, string lang)
    {
        var keywords = lang switch
        {
            "hi" => new[] { "हटाओ", "हटाना", "डिलीट", "मिटाओ", "हटा दो" },
            "te" => new[] { "తొలగించు", "తీసేయి", "తొలగించండి", "డిలీట్" },
            _ => new[] { "delete", "remove" }
        };
        var lower = text.ToLowerInvariant();
        foreach (var kw in keywords)
        {
            var idx = lower.IndexOf(kw, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var after = text.Substring(idx + kw.Length).Trim();
                return CleanTitle(after, lang);
            }
        }
        return text;
    }

    private static string CleanTitle(string raw, string lang)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        raw = Regex.Replace(raw, @"^(the\s+|a\s+|this\s+)?(?:task|note|reminder|todo)\s+(?:called|titled)\s+", "", RegexOptions.IgnoreCase);
        raw = Regex.Replace(raw, @"^(?:my|the|this|that|a|an)\s+", "", RegexOptions.IgnoreCase);
        raw = Regex.Replace(raw, @"^(?:task|note|reminder|todo)\s+", "", RegexOptions.IgnoreCase);
        foreach (var stop in new[] { "my", "the", "this", "that", "a", "an", "to", "मेरे", "मेरी", "यह", "నా" })
        {
            if (raw.Equals(stop, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        }
        return raw.TrimEnd('.', '!', '?');
    }

    private static (string, string) ExtractNote(string text, string lang)
    {
        if (lang == "hi")
        {
            var idx = text.IndexOf("नोट", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var after = text[(idx + 3)..].TrimStart(':', ' ', ',', '।');
                var comma = after.IndexOfAny(new[] { ',', '।' });
                var content = comma >= 0 ? after[(comma + 1)..].Trim() : after;
                var title = comma >= 0 ? after[..comma].Trim() : after;
                return (title.Length > 60 ? title[..60] : title, content);
            }
        }
        else if (lang == "te")
        {
            var idx = text.IndexOf("నోట్", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var after = text[(idx + 5)..].TrimStart(':', ' ', ',', '।');
                var comma = after.IndexOfAny(new[] { ',', '।' });
                var content = comma >= 0 ? after[(comma + 1)..].Trim() : after;
                var title = comma >= 0 ? after[..comma].Trim() : after;
                return (title.Length > 60 ? title[..60] : title, content);
            }
        }
        else
        {
            var idx = text.IndexOfAny(new[] { ':', '—' });
            if (idx >= 0 && idx < text.Length - 1)
            {
                var after = text[(idx + 1)..].Trim();
                var comma = after.IndexOf(',');
                var title = comma >= 0 ? after[..comma].Trim() : after;
                var content = comma >= 0 ? after[(comma + 1)..].Trim() : after;
                return (title.Length > 60 ? title[..60] : title, content);
            }
            // "create a note called Grocery List saying buy milk and eggs"
            var named = Regex.Match(text, @"(?:take|add|create|write)\s+(?:a\s+)?note\s+(?:called|titled)\s+(.+?)(?:\s+saying\s+(.+))?$", RegexOptions.IgnoreCase);
            if (named.Success)
            {
                var title = CleanTitle(StripScheduleSuffix(named.Groups[1].Value.Trim()), lang);
                var content = named.Groups[2].Success ? named.Groups[2].Value.Trim() : title;
                return (title.Length > 60 ? title[..60] : title, content);
            }

            var match = Regex.Match(text, @"(?:take|add|create|write)\s+(?:a\s+)?note[:\s,]?\s*(.*)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var after = match.Groups[1].Value.Trim();
                return (after.Length > 60 ? after[..60] : after, after);
            }
        }
        return (text, text);
    }

    private static string ExtractReminderTitle(string text, string lang)
    {
        if (lang == "hi")
        {
            var idx = text.IndexOf("याद", StringComparison.Ordinal);
            if (idx >= 0)
            {
                // "मुझे <X> की याद दिलाओ" -> title is the text before "याद".
                var before = Regex.Replace(text[..idx], @"\s*(की|के|कि)\s*$", "");
                before = Regex.Replace(before, @"^\s*(?:मुझे|मैं|हमें|आपको|आप)\s+", "");
                before = Regex.Replace(before, @"\s*\d{1,2}(\s*:\s*\d{1,2})?\s*बजे\s*", " ");
                before = Regex.Replace(before, @"\s*(कल|आज|परसों)\s*", " ");
                before = Regex.Replace(before, @"\s*(सुबह|दोपहर|शाम|रात)\s*", " ");
                before = Regex.Replace(before, @"\s*मुझे\s*", " ");
                before = before.Trim();
                if (!string.IsNullOrWhiteSpace(before)) return CleanTitle(before, lang);

                // "याद दिलाओ कि <X>" -> title is the text after "याद दिलाओ".
                var after = text[(idx + 3)..].TrimStart();
                after = Regex.Replace(after, @"^(?:दिलाओ|दिलाना)\s+(?:कि\s+)?", "");
                var t = CleanTitle(StripScheduleSuffix(after), lang);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }
        }
        else if (lang == "te")
        {
            var idx = text.IndexOf("గుర్తు", StringComparison.Ordinal);
            if (idx >= 0)
            {
                // "నాకు <X> గుర్తు చేయి" -> title is the text before "గుర్తు".
                var before = Regex.Replace(text[..idx], @"\s*(కి|కోసం)\s*$", "");
                before = Regex.Replace(before, @"^\s*(?:నాకు|మీకు|నేను|మీరు)\s+", "");
                before = before.Trim();
                if (!string.IsNullOrWhiteSpace(before)) return CleanTitle(before, lang);

                // "గుర్తు చేయి <X>" -> title is the text after.
                var after = text[(idx + 6)..].TrimStart();
                after = Regex.Replace(after, @"^(?:చేయి|చేయండి)\s+", "");
                after = Regex.Replace(after, @"^\s*నేను\s*", "");
                var t = CleanTitle(StripScheduleSuffix(after), lang);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }
        }
        else
        {
            var match = Regex.Match(text, @"(?:remind me|reminder to|create a reminder|set a reminder|set reminder|add a reminder)\b.*?\bto\s+(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var t = CleanTitle(match.Groups[1].Value.Trim(), lang);
                t = StripScheduleSuffix(t);
                return t.Trim();
            }
            match = Regex.Match(text, @"(?:remind me|create a reminder|set a reminder|set reminder)\s+.*?\bat\s+\d", RegexOptions.IgnoreCase);
            if (match.Success) return string.Empty;
            match = Regex.Match(text, @"(?:remind me|reminder to|set a reminder|set reminder|create a reminder|add a reminder)\b.*?\bfor\s+(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var t = CleanTitle(match.Groups[1].Value.Trim(), lang);
                t = StripScheduleSuffix(t);
                return t.Trim();
            }
            var colon = text.IndexOf(':');
            if (colon >= 0 && text[..colon].ToLowerInvariant().Contains("remind")) return CleanTitle(text[(colon + 1)..].Trim(), lang);
        }
        return CleanTitle(text, lang);
    }

    private static string ExtractMeetingTitle(string text, string lang, bool hasDate)
    {
        if (lang == "hi")
        {
            var match = Regex.Match(text, @"मीटिंग\s+शेड्यूल");
            if (match.Success) return "Meeting";
            var withIdx = text.IndexOf("के साथ", StringComparison.Ordinal);
            if (withIdx >= 0)
            {
                var person = text[..withIdx].Trim();
                return $"Meeting with {CleanTitle(person, lang)}";
            }
            var meetingIdx = text.IndexOf("मीटिंग", StringComparison.Ordinal);
            if (meetingIdx >= 0)
            {
                var after = text[(meetingIdx + 6)..].TrimStart(' ');
                return CleanTitle(after, lang);
            }
        }
        else if (lang == "te")
        {
            var withIdx = text.IndexOf("తో", StringComparison.Ordinal);
            if (withIdx >= 0)
            {
                var person = text[..withIdx].Trim();
                return $"Meeting with {CleanTitle(person, lang)}";
            }
            var meetingIdx = text.IndexOf("మీటింగ్", StringComparison.Ordinal);
            if (meetingIdx >= 0)
            {
                var after = text[(meetingIdx + 8)..].TrimStart(' ');
                return CleanTitle(after, lang);
            }
        }
        else
        {
            var match = Regex.Match(text, @"meet with\s+([\w ]+?)(?:\s+(?:on|at|tomorrow|today|next|this|monday|tuesday|wednesday|thursday|friday|saturday|sunday)|$)", RegexOptions.IgnoreCase);
            if (match.Success) return $"Meeting with {CleanTitle(match.Groups[1].Value.Trim(), lang)}";
            match = Regex.Match(text, @"schedule\s+(?:a\s+|an\s+|the\s+)?meeting\s+(?:with\s+)?([\w ]+?)(?:\s+(?:with|on|at|tomorrow|today|next|this|for)|$)", RegexOptions.IgnoreCase);
            if (match.Success) return $"Meeting with {CleanTitle(match.Groups[1].Value.Trim(), lang)}";
            match = Regex.Match(text, @"(?:schedule|book)\s+(?:a\s+|an\s+)?([\w ]+?)(?:\s+(?:for|on|at|tomorrow|today|next|this)|$)", RegexOptions.IgnoreCase);
            if (match.Success) return Capitalize(CleanTitle(match.Groups[1].Value.Trim(), lang));
            if (hasDate)
            {
                var idx = text.IndexOf(" at ", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) return CleanTitle(text[..idx].Trim(), lang);
            }
        }
        return "Meeting";
    }

    private static List<string> ExtractParticipants(string text, string lang)
    {
        var result = new List<string>();
        if (lang == "hi")
        {
            var match = Regex.Match(text, @"के साथ\s+([\w ]+?)(?:\s+(?:सोमवार|मंगलवार|बुधवार|गुरुवार|शुक्रवार|शनिवार|रविवार|कल|आज|को|पर|सुबह|शाम)|$)", RegexOptions.IgnoreCase);
            if (match.Success) result.Add(CleanTitle(match.Groups[1].Value.Trim(), lang));
        }
        else if (lang == "te")
        {
            var match = Regex.Match(text, @"తో\s+([\w ]+?)(?:\s+(?:సోమవారం|మంగళవారం|బుధవారం|గురువారం|శుక్రవారం|శనివారం|ఆదివారం|రేపు|ఈరోజు|ఉదయం|సాయంత్రం)|$)", RegexOptions.IgnoreCase);
            if (match.Success) result.Add(CleanTitle(match.Groups[1].Value.Trim(), lang));
        }
        else
        {
            var match = Regex.Match(text, @"(?:with|meet with)\s+([\w ]+?)(?:\s+(?:on|at|tomorrow|today|next|this|for|from|\d)|$)", RegexOptions.IgnoreCase);
            if (match.Success) result.Add(CleanTitle(match.Groups[1].Value.Trim(), lang));
            match = Regex.Match(text, @"with\s+(?:john|ravi|ram|shyam|priya|sita|jane|alice|bob)\s+and\s+([\w ]+?)(?:\s+(?:on|at|tomorrow)|$)", RegexOptions.IgnoreCase);
            if (match.Success) result.Add(CleanTitle(match.Groups[1].Value.Trim(), lang));
        }
        return result;
    }

    private static string ExtractLocation(string lower)
    {
        var match = Regex.Match(lower, @"(?:at|in)\s+(?:the\s+)?(?:office|conference room|cafe|restaurant|park|home|hub|auditorium)\s*(\w*)", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : string.Empty;
    }

    private static string ExtractTaskTitle(string text, string lang)
    {
        if (lang == "hi")
        {
            var idx = text.IndexOf("कार्य", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var after = text[(idx + 4)..].TrimStart(':', ' ', ',', '।');
                return CleanTitle(after, lang);
            }
            var taskIdx = text.IndexOf("टास्क", StringComparison.Ordinal);
            if (taskIdx >= 0)
            {
                var after = text[(taskIdx + 5)..].TrimStart(':', ' ', ',', '।');
                return CleanTitle(after, lang);
            }
        }
        else if (lang == "te")
        {
            var idx = text.IndexOf("పని", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var after = text[(idx + 3)..].TrimStart(':', ' ', ',', '।');
                return CleanTitle(after, lang);
            }
            var taskIdx = text.IndexOf("టాస్క్", StringComparison.Ordinal);
            if (taskIdx >= 0)
            {
                var after = text[(taskIdx + 6)..].TrimStart(':', ' ', ',', '।');
                return CleanTitle(after, lang);
            }
        }
        else
        {
            var match = Regex.Match(text, @"(?:add|create)\s+(?:a\s+|an\s+)?task\s*(?::)?\s*(?:called|titled|to|for)?\s*(.+)", RegexOptions.IgnoreCase);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                return CleanTitle(StripScheduleSuffix(match.Groups[1].Value.Trim()), lang);
            }
            var idx = text.IndexOf(':');
            if (idx >= 0) return CleanTitle(StripScheduleSuffix(text[(idx + 1)..].Trim()), lang);
            // Bare "add task" / "create a task" with nothing after — the caller asks for the title.
            var bare = Regex.Match(text, @"(?:add|create)\s+(?:a\s+|an\s+)?task", RegexOptions.IgnoreCase);
            if (bare.Success && string.IsNullOrWhiteSpace(text[bare.Length..])) return string.Empty;
        }
        return CleanTitle(text, lang);
    }

    private static RecurrenceType ExtractRecurrence(string lower, string lang)
    {
        if (lang == "hi")
        {
            if (lower.Contains("हर दिन") || lower.Contains("रोज़")) return RecurrenceType.Daily;
            if (lower.Contains("हर हफ्ते") || lower.Contains("हर सप्ताह")) return RecurrenceType.Weekly;
            if (lower.Contains("हर महीने")) return RecurrenceType.Monthly;
            if (lower.Contains("हर साल")) return RecurrenceType.Yearly;
        }
        else if (lang == "te")
        {
            if (lower.Contains("ప్రతి రోజు") || lower.Contains("రోజూ")) return RecurrenceType.Daily;
            if (lower.Contains("ప్రతి వారం")) return RecurrenceType.Weekly;
            if (lower.Contains("ప్రతి నెల")) return RecurrenceType.Monthly;
            if (lower.Contains("ప్రతి సంవత్సరం")) return RecurrenceType.Yearly;
        }
        else
        {
            if (lower.Contains("every day") || lower.Contains("daily")) return RecurrenceType.Daily;
            if (lower.Contains("every week") || lower.Contains("weekly")) return RecurrenceType.Weekly;
            if (lower.Contains("every month") || lower.Contains("monthly")) return RecurrenceType.Monthly;
            if (lower.Contains("every year") || lower.Contains("yearly")) return RecurrenceType.Yearly;
            if (lower.Contains("every monday") || lower.Contains("every tuesday") || lower.Contains("every wednesday") ||
                lower.Contains("every thursday") || lower.Contains("every friday") || lower.Contains("every saturday") ||
                lower.Contains("every sunday")) return RecurrenceType.Weekly;
            if (Regex.IsMatch(lower, @"on the \d+(st|nd|rd|th) of every month")) return RecurrenceType.Monthly;
        }
        return RecurrenceType.Once;
    }

    private static string Capitalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpperInvariant(input[0]) + input[1..];
    }
}
