using System.Text.RegularExpressions;

namespace MyAssistant.Infrastructure.Services.AI;

/// <summary>Tokenizer + matching helpers over a normalized command string.</summary>
public class CommandContext
{
    public string Text { get; set; }
    public string Language { get; }
    public bool Short => Text.Split(' ').Length <= 4;

    private static readonly string[] WakeWords = { "assistant", "hey assistant", "एसिस्टेंट", "असिस्टेंट", "అసిస్టెంట్" };

    public CommandContext(string text, string language)
    {
        Text = text;
        Language = language;
    }

    public void StripWakeWord()
    {
        foreach (var w in WakeWords)
        {
            if (Text.StartsWith(w))
            {
                Text = Text.Substring(w.Length).TrimStart(',', ' ', ':');
                return;
            }
        }
    }


    public bool HasAny(params string[] terms)
    {
        foreach (var t in terms)
            if (Text.Contains(t, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool HasWord(params string[] terms)
    {
        foreach (var t in terms)
            if (Regex.IsMatch(Text, $@"\b{Regex.Escape(t)}\b", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    /// <summary>Extract a meaningful title for the entity, stripping intent/date/time keywords.</summary>
    public string ExtractTitle()
    {
        var text = Text;
        // Handle natural phrases: "i need to call bank", "put this down as meeting notes", etc.
        var natural = Regex.Match(text, @"(?:i need to|i have to|i must|got to|need to|put this down as|write down|jot down|remember)\s+([a-z0-9 ,.!?'-]+)");
        if (natural.Success) text = natural.Groups[1].Value;
        else
        {
            var m = Regex.Match(text, "(?:to |to call |call |to complete |complete |to buy |buy |kall|for )([a-z0-9 ,.!?'-]+)");
            if (m.Success) text = m.Groups[1].Value;
        }
        foreach (var s in StopWords)
            text = Regex.Replace(text, $@"\b{Regex.Escape(s)}\b", " ");

        // remove date/time chunks
        text = Regex.Replace(text, @"\b(tomorrow|today|tonight|next monday|this friday|next week|day after tomorrow)\b", " ");
        text = Regex.Replace(text, @"\b(at|in|by|on|for|this|next|every)\b", " ");
        text = Regex.Replace(text, @"\b\d{1,2}(:\d{2})?\s*(am|pm|a\.m\.|p\.m\.)?\b", " ");
        text = Regex.Replace(text, @"\b\d+\s*(minutes|minute|mins|hours|hour|hrs|hr|day|days)\b", " ");
        text = Regex.Replace(text, @"(में|बजे|गंटों|నిమిషాలు|గంటలు)", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim().Trim(' ', ':', ',', '.', '!', '?');

        if (string.IsNullOrEmpty(text))
        {
            var tokens = Text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 3 && !IsStop(t) && !t.Any(char.IsDigit)).ToList();
            text = string.Join(" ", tokens);
        }
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return char.ToUpperInvariant(text[0]) + (text.Length > 1 ? text.Substring(1) : string.Empty);
    }

    public string ExtractContent()
    {
        var text = Text;
        var markers = new[] { "note:", "note that", "note :", "write", "take a note",
            "नोट लो", "నోట్ తీసుకో", "put this down", "jot down", "remember", "write down" };
        foreach (var mk in markers)
        {
            var idx = text.IndexOf(mk, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                text = text.Substring(idx + mk.Length);
                break;
            }
        }
        foreach (var s in new[] { "assistant", "please", "add", "make", "save", "a ", "the ",
            "i need to", "i have to", "i must", "got to" })
            text = text.Replace(" " + s, " ");
        return text.Trim().Trim(' ', ':', ',', '.', '!', '?');
    }

    public string ExtractLocation()
    {
        var m = Regex.Match(Text, @"\b(?:at|in|@)\s+([a-z0-9 ]{2,})", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    public string? ExtractQuery()
    {
        foreach (var marker in new[] { "about ", "for ", "on ", "में ", "గురించి " })
        {
            var idx = Text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var q = Text.Substring(idx + marker.Length).Trim().Trim('?', '.', '!');
                if (q.Length > 1) return q;
            }
        }
        return null;
    }

    public List<string> ExtractParticipants()
    {
        var names = new List<string>();
        foreach (Match m in Regex.Matches(Text, @"\b(?:with|and|&)\s+([a-z][a-z ]{1,30})", RegexOptions.IgnoreCase))
        {
            var name = m.Groups[1].Value.Trim();
            // cut at keywords that likely aren't part of the name
            var cut = Regex.Match(name, @"\b(at|in|for|tomorrow|today|next|this)\b");
            if (cut.Success) name = name.Substring(0, cut.Index);
            name = name.Trim();
            if (name.Length >= 2 && !names.Contains(name)) names.Add(char.ToUpper(name[0]) + name.Substring(1));
        }
        return names;
    }

    private static readonly string[] StopWords =
    {
        "assistant", "please", "create", "a", "an", "the", "new", "note", "notes", "task", "tasks",
        "remind me", "reminder", "reminders", "remind", "appointment", "appointments", "meeting",
        "meetings", "schedule", "book", "set", "add", "make", "take", "me", "to", "and", "about",
        "with", "you", "your", "for", "on", "at", "in", "by", "करने", "की", "लो", "नोट", "लेना",
        "याद", "दिलाना", "काम", "बनाओ", "ఒక", "నోట్", "తీసుకో", "గుర్తు", "చేయి", "చేయాలని", "పని", "టాస్క్"
    };

    private static bool IsStop(string w) => StopWords.Contains(w.ToLowerInvariant());
}

/// <summary>Resolves relative and absolute dates/times to yyyy-MM-dd / HH:mm.</summary>
public static class DateTimeResolver
{
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata") ?? TimeZoneInfo.Local;

    public static string? ParseDate(CommandContext ctx)
    {
        var t = ctx.Text;
        var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, Ist);

        if (Contains(t, "day after tomorrow", "परसों", "తర్వాత రోజు")) return now.AddDays(2).ToString("yyyy-MM-dd");
        if (Contains(t, "tomorrow", "कल", "రేపు")) return now.AddDays(1).ToString("yyyy-MM-dd");
        if (Contains(t, "today", "आज", "ఈరోజు", "ఈ రోజు", "tonight", "आज रात")) return now.ToString("yyyy-MM-dd");

        var nextMatch = Regex.Match(t, @"next (monday|tuesday|wednesday|thursday|friday|saturday|sunday)");
        if (nextMatch.Success)
        {
            var dow = (int)Enum.Parse<DayOfWeek>(nextMatch.Groups[1].Value, true);
            var days = (dow - (int)now.DayOfWeek + 7) % 7;
            if (days == 0) days = 7;
            return now.AddDays(days).ToString("yyyy-MM-dd");
        }

        var thisMatch = Regex.Match(t, @"this (monday|tuesday|wednesday|thursday|friday|saturday|sunday)");
        if (thisMatch.Success)
        {
            var dow = (int)Enum.Parse<DayOfWeek>(thisMatch.Groups[1].Value, true);
            var days = (dow - (int)now.DayOfWeek + 7) % 7;
            return now.AddDays(days).ToString("yyyy-MM-dd");
        }

        if (Contains(t, "next week", "अगले हफ्ते", "వచ్చే వారం")) return now.AddDays(7).ToString("yyyy-MM-dd");

        // "in an hour", "in 2 hours", "in half an hour", "in 30 minutes", "in 15 mins"
        var inHourWord = Regex.Match(t, @"in (an?|one|two|three|four|five|six) (hour|hours)");
        if (inHourWord.Success)
        {
            var word = inHourWord.Groups[1].Value.ToLowerInvariant();
            var n = word is "an" or "one" ? 1 : word == "two" ? 2 : word == "three" ? 3
                : word == "four" ? 4 : word == "five" ? 5 : 6;
            var dt = now.AddHours(n);
            return dt.ToString("yyyy-MM-dd");
        }
        var inHalfHour = Regex.Match(t, @"in half an? hour");
        if (inHalfHour.Success)
        {
            return now.AddMinutes(30).ToString("yyyy-MM-dd");
        }
        var inMatch = Regex.Match(t, @"in (\d+) (minutes?|minute|hours?|hrs?|hour)");
        if (inMatch.Success)
        {
            var n = int.Parse(inMatch.Groups[1].Value);
            var unit = inMatch.Groups[2].Value;
            var dt = unit.StartsWith("hour") ? now.AddHours(n) : now.AddMinutes(n);
            return dt.ToString("yyyy-MM-dd");
        }

        // absolute date yyyy-MM-dd or dd/MM/yyyy or dd-MM
        var abs = Regex.Match(t, @"(\d{4})-(\d{2})-(\d{2})");
        if (abs.Success) return abs.Value;

        var mdy = Regex.Match(t, @"(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?");
        if (mdy.Success)
        {
            int day = int.Parse(mdy.Groups[1].Value), mon = int.Parse(mdy.Groups[2].Value);
            var y = mdy.Groups[3].Success ? int.Parse(mdy.Groups[3].Value) : now.Year;
            if (y < 100) y += 2000;
            try { return new DateTime(y, mon, day).ToString("yyyy-MM-dd"); } catch { }
        }

        // "3rd of next month", "15th of next month"
        var nthOfMonth = Regex.Match(t, @"(\d{1,2})(?:st|nd|rd|th)?\s+(?:of\s+)?next\s+month");
        if (nthOfMonth.Success)
        {
            var day = int.Parse(nthOfMonth.Groups[1].Value);
            var nextMonth = now.AddMonths(1);
            try { return new DateTime(nextMonth.Year, nextMonth.Month, day).ToString("yyyy-MM-dd"); } catch { }
        }

        // relative with implicit "tomorrow morning"
        if (Contains(t, "morning", "सुबह", "ఉదయం") && Contains(t, "tomorrow", "कल", "రేపు"))
            return now.AddDays(1).ToString("yyyy-MM-dd");

        return null;
    }

    public static string? ParseTime(CommandContext ctx)
    {
        var t = ctx.Text;
        var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, Ist);

        // "in an hour", "in 2 hours", "in half an hour", "in 30 minutes"
        var inHourWord = Regex.Match(t, @"in (an?|one|two|three|four|five|six) (hour|hours)");
        if (inHourWord.Success)
        {
            var word = inHourWord.Groups[1].Value.ToLowerInvariant();
            var n = word is "an" or "one" ? 1 : word == "two" ? 2 : word == "three" ? 3
                : word == "four" ? 4 : word == "five" ? 5 : 6;
            var dt = now.AddHours(n);
            return dt.ToString("HH:mm");
        }
        var inHalfHour = Regex.Match(t, @"in half an? hour");
        if (inHalfHour.Success)
        {
            return now.AddMinutes(30).ToString("HH:mm");
        }
        var inMatch = Regex.Match(t, @"in (\d+) (minutes?|minute|hours?|hrs?|hour)");
        if (inMatch.Success)
        {
            var n = int.Parse(inMatch.Groups[1].Value);
            var unit = inMatch.Groups[2].Value;
            var dt = unit.StartsWith("hour") ? now.AddHours(n) : now.AddMinutes(n);
            return dt.ToString("HH:mm");
        }

        // "at 6 PM", "at 9 am", "6:30pm", "9 बजे"
        var ampm = Regex.Match(t, @"(\d{1,2})(?::(\d{2}))?\s*(am|pm|a\.m\.|p\.m\.|बजे|गंटा)", RegexOptions.IgnoreCase);
        if (ampm.Success)
        {
            var h = int.Parse(ampm.Groups[1].Value);
            var m = ampm.Groups[2].Success ? int.Parse(ampm.Groups[2].Value) : 0;
            var suffix = ampm.Groups[3].Value.ToLowerInvariant();
            var isNight = Contains(t, "रात", "रात्रि", "రాత్రి");
            if (suffix.Contains("pm") || suffix == "गंटा" && h < 12) h += 12;
            else if (suffix == "बजे" && isNight && h < 12) h += 12;
            if (suffix.Contains("am") && h == 12) h = 0;
            return new DateTime(2000, 1, 1, h, m, 0).ToString("HH:mm");
        }

        // 24-hour "14:30"
        var h24 = Regex.Match(t, @"\b(\d{1,2}):(\d{2})\b");
        if (h24.Success)
        {
            var h = int.Parse(h24.Groups[1].Value);
            var m = int.Parse(h24.Groups[2].Value);
            if (h <= 23 && m <= 59) return new DateTime(2000, 1, 1, h, m, 0).ToString("HH:mm");
        }

        // parts of day
        if (Contains(t, "morning", "सुबह", "ఉదయం")) return "09:00";
        if (Contains(t, "afternoon", "दोपहर", "మధ్యాహ్నం")) return "14:00";
        if (Contains(t, "evening", "शाम", "సాయంత్రం")) return "18:00";
        if (Contains(t, "tonight", "आज रात", "ఈ రాత్రి")) return "21:00";
        if (Contains(t, "night", "रात", "రాత్రి")) return "22:00";

        return null;
    }

    /// <summary>
    /// Combines a parsed date/time with today-relative defaults (today / 09:00) and, if the
    /// resulting moment is already in the past, rolls it forward to its next occurrence so
    /// appointments, reminders and task due dates are never scheduled retroactively.
    /// </summary>
    public static DateTime? Resolve(string? date, string? time, DateTime nowIst)
    {
        DateTime? d = null;
        if (date is not null && DateTime.TryParse(date, out var parsedDate)) d = parsedDate;
        TimeSpan? t = null;
        if (time is not null && TimeSpan.TryParse(time, out var parsedTime)) t = parsedTime;

        if (d is null && t is null) return null;
        d ??= nowIst.Date;
        t ??= TimeSpan.FromHours(9);

        var dt = d.Value.Date.Add(t.Value);
        if (dt <= nowIst) dt = dt.AddDays(1);
        return dt;
    }

    private static bool Contains(string t, params string[] terms)
        => terms.Any(x => t.Contains(x, StringComparison.OrdinalIgnoreCase));
}

public static class DurationParser
{
    public static int? Parse(CommandContext ctx)
    {
        var m = Regex.Match(ctx.Text, @"(\d+)\s*(hour|hr|hours|hrs|minute|min|minutes|mins)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var n = int.Parse(m.Groups[1].Value);
        return m.Groups[2].Value.StartsWith("hour") || m.Groups[2].Value.StartsWith("hr") ? n * 60 : n;
    }
}

public static class RecurrenceParser
{
    public static string? Parse(CommandContext ctx)
    {
        var t = ctx.Text;
        if (Contains(t, "every day", "daily", "हर दिन", "ప్రతి రోజు")) return "Daily";
        if (Contains(t, "every week", "weekly", "हर हफ्ते", "ప్రతి వారం")) return "Weekly";
        if (Contains(t, "every month", "monthly", "हर महीने", "ప్రతి నెల")) return "Monthly";
        if (Contains(t, "every year", "yearly", "हर साल", "ప్రతి సంవత్సరం")) return "Yearly";
        if (Contains(t, "every monday", "every tuesday", "every wednesday", "every thursday", "every friday", "every saturday", "every sunday",
            "हर सोमवार", "ప్రతి సోమవారం"))
            return "Weekly";
        return "Once";
    }

    private static bool Contains(string t, params string[] terms)
        => terms.Any(x => t.Contains(x, StringComparison.OrdinalIgnoreCase));
}

public static class PriorityParser
{
    public static string? Parse(CommandContext ctx)
    {
        if (ctx.HasAny("critical", "क्रिटिकल", "అత్యవసరం", "blocker")) return "Critical";
        if (ctx.HasAny("urgent", "तुरंत")) return "Urgent";
        if (ctx.HasAny("high", "high priority", "जरूरी", "అధిక", "important", "महत्वपूर्ण", "ముఖ్యమైన")) return "High";
        if (ctx.HasAny("low", "कम", "తక్కువ")) return "Low";
        return "Medium";
    }
}