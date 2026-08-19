using System.Globalization;
using System.Text.RegularExpressions;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Application.Services;

public partial class DateTimeParserService : IDateTimeParser
{
    private readonly ITimeZoneService _timeZoneService;

    public DateTimeParserService(ITimeZoneService timeZoneService)
    {
        _timeZoneService = timeZoneService;
    }

    public Task<ParsedDateTimeResult> ParseAsync(string text, string language, string timeZone, CancellationToken cancellationToken = default)
    {
        var result = new ParsedDateTimeResult { RawExpression = text };
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(result);
        }

        var normalized = text.Trim();
        var now = _timeZoneService.NowInTimeZone(timeZone);
        var dateFound = false;
        var timeFound = false;

        var lang = DetectScriptLanguage(normalized);
        DateTime? baseDate = null;

        (baseDate, dateFound) = ParseDate(normalized, lang, now);
        if (dateFound)
        {
            result.HasDate = true;
        }

        (var parsedTime, var timeMatch, timeFound) = ParseTime(normalized, lang, now);
        if (timeFound)
        {
            result.HasTime = true;
            result.DateTime = parsedTime;
        }
        else if (dateFound)
        {
            result.DateTime = baseDate;
        }

        if (baseDate.HasValue && timeMatch.HasValue && timeMatch.Value.Hour >= 0)
        {
            result.DateTime = baseDate.Value.Date.Add(timeMatch.Value.ToTimeSpan());
        }
        else if (timeMatch.HasValue && timeMatch.Value.Hour >= 0)
        {
            result.DateTime = now.Date.Add(timeMatch.Value.ToTimeSpan());
        }

        var duration = ParseDuration(normalized, lang);
        if (duration.HasValue)
        {
            result.DurationMinutes = duration.Value;
        }

        if (!dateFound && !timeFound)
        {
            var relative = ParseRelative(normalized, lang, now);
            if (relative.HasValue)
            {
                result.HasDate = true;
                result.HasTime = true;
                result.DateTime = relative.Value;
            }
        }

        return Task.FromResult(result);
    }

    private static string DetectScriptLanguage(string text)
    {
        if (text.Any(c => c >= 0x0900 && c <= 0x097F)) return "hi";
        if (text.Any(c => c >= 0x0C00 && c <= 0x0C7F)) return "te";
        return "en";
    }

    private static (DateTime?, bool) ParseDate(string text, string lang, DateTime now)
    {
        var lower = text.ToLowerInvariant();

        if (lang == "hi")
        {
            if (lower.Contains("परसों")) return (now.Date.AddDays(2), true);
            if (lower.Contains("आज")) return (now.Date, true);
            if (lower.Contains("कल")) return (now.Date.AddDays(1), true);
            if (lower.Contains("सोमवार")) return (NextWeekday(now, DayOfWeek.Monday, text.Contains("अगले")), true);
            if (lower.Contains("मंगलवार")) return (NextWeekday(now, DayOfWeek.Tuesday, text.Contains("अगले")), true);
            if (lower.Contains("बुधवार")) return (NextWeekday(now, DayOfWeek.Wednesday, text.Contains("अगले")), true);
            if (lower.Contains("गुरुवार")) return (NextWeekday(now, DayOfWeek.Thursday, text.Contains("अगले")), true);
            if (lower.Contains("शुक्रवार")) return (NextWeekday(now, DayOfWeek.Friday, text.Contains("अगले")), true);
            if (lower.Contains("शनिवार")) return (NextWeekday(now, DayOfWeek.Saturday, text.Contains("अगले")), true);
            if (lower.Contains("रविवार")) return (NextWeekday(now, DayOfWeek.Sunday, text.Contains("अगले")), true);
            if (lower.Contains("अगले सप्ताह") || lower.Contains("अगले हफ्ते")) return (now.Date.AddDays(7), true);
        }
        else if (lang == "te")
        {
            if (lower.Contains("ఎల్లుండి")) return (now.Date.AddDays(2), true);
            if (lower.Contains("రేపు")) return (now.Date.AddDays(1), true);
            if (lower.Contains("ఈరోజు") || lower.Contains("నేడు")) return (now.Date, true);
            if (lower.Contains("సోమవారం")) return (NextWeekday(now, DayOfWeek.Monday, text.Contains("వచ్చే")), true);
            if (lower.Contains("మంగళవారం")) return (NextWeekday(now, DayOfWeek.Tuesday, text.Contains("వచ్చే")), true);
            if (lower.Contains("బుధవారం")) return (NextWeekday(now, DayOfWeek.Wednesday, text.Contains("వచ్చే")), true);
            if (lower.Contains("గురువారం")) return (NextWeekday(now, DayOfWeek.Thursday, text.Contains("వచ్చే")), true);
            if (lower.Contains("శుక్రవారం")) return (NextWeekday(now, DayOfWeek.Friday, text.Contains("వచ్చే")), true);
            if (lower.Contains("శనివారం")) return (NextWeekday(now, DayOfWeek.Saturday, text.Contains("వచ్చే")), true);
            if (lower.Contains("ఆదివారం")) return (NextWeekday(now, DayOfWeek.Sunday, text.Contains("వచ్చే")), true);
            if (lower.Contains("వచ్చే వారం")) return (now.Date.AddDays(7), true);
        }
        else
        {
            if (lower.Contains("day after tomorrow")) return (now.Date.AddDays(2), true);
            if (lower.Contains("tomorrow")) return (now.Date.AddDays(1), true);
            if (lower.Contains("today")) return (now.Date, true);
            if (lower.Contains("tonight")) return (now.Date, true);

            foreach (var day in new[]
                     {
                         ("monday", DayOfWeek.Monday), ("tuesday", DayOfWeek.Tuesday), ("wednesday", DayOfWeek.Wednesday),
                         ("thursday", DayOfWeek.Thursday), ("friday", DayOfWeek.Friday), ("saturday", DayOfWeek.Saturday),
                         ("sunday", DayOfWeek.Sunday)
                     })
            {
                if (lower.Contains(day.Item1))
                {
                    var next = lower.Contains("next");
                    var weekOffset = next ? 1 : 0;
                    return (NextWeekday(now, day.Item2, weekOffset == 1), true);
                }
            }

            if (lower.Contains("next week")) return (now.Date.AddDays(7), true);

            var isoDate = Regex.Match(lower, @"\b(\d{4})[-/](\d{1,2})[-/](\d{1,2})\b");
            if (isoDate.Success &&
                int.TryParse(isoDate.Groups[1].Value, out var isoYear) &&
                int.TryParse(isoDate.Groups[2].Value, out var isoMonth) &&
                int.TryParse(isoDate.Groups[3].Value, out var isoDay))
            {
                if (DateTime.TryParse($"{isoYear:0000}-{isoMonth:00}-{isoDay:00}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDt))
                {
                    return (isoDt.Date, true);
                }
            }

            var explicitDate = Regex.Match(lower, @"\b(\d{1,2})[-/](\d{1,2})([-/](\d{2,4}))?\b");
            if (explicitDate.Success)
            {
                if (DateTime.TryParse(explicitDate.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return (dt.Date, true);
                }
            }

            var namedMonth = Regex.Match(lower, @"\b(\d{1,2})\s+(january|february|march|april|may|june|july|august|september|october|november|december)\b");
            if (namedMonth.Success)
            {
                if (DateTime.TryParse(namedMonth.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return (dt.Date, true);
                }
            }
        }

        return (null, false);
    }

    private static (DateTime?, TimeOnly?, bool) ParseTime(string text, string lang, DateTime now)
    {
        var lower = text.ToLowerInvariant();

        TimeOnly? periodTime = null;
        if (lang == "hi")
        {
            if (lower.Contains("सुबह")) periodTime = new TimeOnly(9, 0);
            else if (lower.Contains("दोपहर")) periodTime = new TimeOnly(13, 0);
            else if (lower.Contains("शाम")) periodTime = new TimeOnly(18, 0);
            else if (lower.Contains("रात")) periodTime = new TimeOnly(21, 0);
        }
        else if (lang == "te")
        {
            if (lower.Contains("ఉదయం")) periodTime = new TimeOnly(9, 0);
            else if (lower.Contains("మధ్యాహ్నం")) periodTime = new TimeOnly(13, 0);
            else if (lower.Contains("సాయంత్రం")) periodTime = new TimeOnly(18, 0);
            else if (lower.Contains("రాత్రి")) periodTime = new TimeOnly(21, 0);
        }
        else
        {
            if (lower.Contains("morning")) periodTime = new TimeOnly(9, 0);
            else if (lower.Contains("afternoon")) periodTime = new TimeOnly(13, 0);
            else if (lower.Contains("evening")) periodTime = new TimeOnly(18, 0);
            else if (lower.Contains("tonight")) periodTime = new TimeOnly(21, 0);
            else if (lower.Contains("after lunch")) periodTime = new TimeOnly(14, 0);
        }

        Match m;
        if (lang == "en")
        {
            m = Regex.Match(lower, @"(?<![\d\-/])\b(\d{1,2})(?::(\d{2}))?\s*(a\.?m\.?|p\.?m\.?|am|pm)?\b(?!\s*[-/]\s*\d)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var hour))
            {
                var minute = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                var meridiem = m.Groups[3].Value.Trim();
                if (meridiem.Length > 0)
                {
                    if (meridiem.StartsWith("p") && hour < 12) hour += 12;
                    if (meridiem.StartsWith("a") && hour == 12) hour = 0;
                    return (null, new TimeOnly(hour, minute), true);
                }
                if (periodTime.HasValue) return (null, periodTime, true);
                if (hour <= 12)
                {
                    return (null, new TimeOnly(hour, minute), true);
                }
                if (hour <= 23)
                {
                    // 24-hour clock (13:00 - 23:59).
                    return (null, new TimeOnly(hour, minute), true);
                }
            }
        }
        else if (lang == "hi")
        {
            m = Regex.Match(lower, @"(\d{1,2})\s*बजे");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var hour))
            {
                if (periodTime.HasValue)
                {
                    return (null, new TimeOnly(AdjustHour(hour, periodTime.Value.Hour), 0), true);
                }
                return (null, new TimeOnly(hour, 0), true);
            }
        }
        else if (lang == "te")
        {
            m = Regex.Match(lower, @"(\d{1,2})\s*గంట");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var hour))
            {
                if (periodTime.HasValue)
                {
                    return (null, new TimeOnly(AdjustHour(hour, periodTime.Value.Hour), 0), true);
                }
                return (null, new TimeOnly(hour, 0), true);
            }
        }

        if (periodTime.HasValue) return (null, periodTime, true);

        return (null, null, false);
    }

    private static int AdjustHour(int hour, int periodHour)
    {
        if (periodHour >= 13 && hour < 12) return hour + 12;
        return hour;
    }

    private static DateTime? ParseRelative(string text, string lang, DateTime now)
    {
        var lower = text.ToLowerInvariant();

        if (lang == "en")
        {
            var inHours = Regex.Match(lower, @"in\s+(\d+)\s*(?:hours?|hrs?|घंटे)");
            var inMins = Regex.Match(lower, @"in\s+(\d+)\s*(?:minutes?|mins?)");
            if (inHours.Success && int.TryParse(inHours.Groups[1].Value, out var h)) return now.AddHours(h);
            if (inMins.Success && int.TryParse(inMins.Groups[1].Value, out var min)) return now.AddMinutes(min);
        }
        else if (lang == "hi")
        {
            var inHours = Regex.Match(lower, @"(\d+)\s*घंटे?\s*बाद");
            var inMins = Regex.Match(lower, @"(\d+)\s*मिनट\s*बाद");
            if (inHours.Success && int.TryParse(inHours.Groups[1].Value, out var h)) return now.AddHours(h);
            if (inMins.Success && int.TryParse(inMins.Groups[1].Value, out var min)) return now.AddMinutes(min);
        }
        else if (lang == "te")
        {
            var inHours = Regex.Match(lower, @"(\d+)\s*గంటల?\s*(తర్వాత|లో)?");
            var inMins = Regex.Match(lower, @"(\d+)\s*నిమిషాల?\s*(తర్వాత|లో)?");
            if (inHours.Success && int.TryParse(inHours.Groups[1].Value, out var h) && h < 24) return now.AddHours(h);
            if (inMins.Success && int.TryParse(inMins.Groups[1].Value, out var min)) return now.AddMinutes(min);
        }

        return null;
    }

    private static int? ParseDuration(string text, string lang)
    {
        var lower = text.ToLowerInvariant();
        if (lang == "en")
        {
            var hours = Regex.Match(lower, @"(?:for\s+)?(\d+|one|an|a|two|half)\s+hours?");
            if (hours.Success)
            {
                var val = WordToNumber(hours.Groups[1].Value);
                return val.HasValue ? val.Value * 60 : null;
            }
            var mins = Regex.Match(lower, @"(?:for\s+)?(\d+)\s*[- ]?minutes?");
            if (mins.Success && int.TryParse(mins.Groups[1].Value, out var m)) return m;
        }
        else if (lang == "hi")
        {
            var hours = Regex.Match(lower, @"(\d+|एक|दो)\s*घंटे?");
            if (hours.Success)
            {
                var val = WordToNumberHindi(hours.Groups[1].Value);
                return val.HasValue ? val.Value * 60 : null;
            }
            var mins = Regex.Match(lower, @"(\d+)\s*मिनट");
            if (mins.Success && int.TryParse(mins.Groups[1].Value, out var m)) return m;
        }
        else if (lang == "te")
        {
            var hours = Regex.Match(lower, @"(\d+|ఒక|రెండు)\s*గంటల?");
            if (hours.Success)
            {
                var val = WordToNumberTelugu(hours.Groups[1].Value);
                return val.HasValue ? val.Value * 60 : null;
            }
            var mins = Regex.Match(lower, @"(\d+)\s*నిమిషాల");
            if (mins.Success && int.TryParse(mins.Groups[1].Value, out var m)) return m;
        }
        return null;
    }

    private static int? WordToNumber(string word)
    {
        return word switch
        {
            "one" or "a" or "an" => 1,
            "two" => 2,
            "half" => 0,
            _ => int.TryParse(word, out var n) ? n : null
        };
    }

    private static int? WordToNumberHindi(string word)
    {
        return word switch
        {
            "एक" => 1,
            "दो" => 2,
            _ => int.TryParse(word, out var n) ? n : null
        };
    }

    private static int? WordToNumberTelugu(string word)
    {
        return word switch
        {
            "ఒక" => 1,
            "రెండు" => 2,
            _ => int.TryParse(word, out var n) ? n : null
        };
    }

    private static DateTime NextWeekday(DateTime now, DayOfWeek target, bool nextWeek)
    {
        var diff = (target - now.DayOfWeek + 7) % 7;
        if (diff == 0) diff = 7;
        if (nextWeek) diff += 7;
        return now.Date.AddDays(diff);
    }
}
