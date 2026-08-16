using FluentAssertions;
using MyAssistant.Infrastructure.Services.AI;
using Xunit;

namespace MyAssistant.Tests;

public class LanguageDetectorTests
{
    [Theory]
    [InlineData("Remind me to call the dentist", "en-IN")]
    [InlineData("मुझे डॉक्टर को बुलाना है", "hi-IN")]
    [InlineData("నేను డాక్టర్ కి కాల్ చేయాలి", "te-IN")]
    public void Detect_ReturnsCorrectLanguage(string text, string expected)
        => LanguageDetector.Detect(text).Should().Be(expected);
}

public class ParserTests
{
    [Fact]
    public void Parse_Reminder_English()
    {
        var result = Parser.Parse("Remind me to call the dentist tomorrow at 9 am", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateReminder);
        result.Date.Should().NotBeNullOrEmpty();
        result.Time.Should().Be("09:00");
        result.Recurrence.Should().Be("Once");
    }

    [Fact]
    public void Parse_Reminder_Hindi()
    {
        var result = Parser.Parse("कल सुबह 9 बजे डॉक्टर को बुलाने की याद दिलाओ", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateReminder);
        result.Language.Should().Be("hi-IN");
        result.Date.Should().NotBeNullOrEmpty();
        result.Time.Should().Be("09:00");
    }

    [Fact]
    public void Parse_Reminder_Telugu()
    {
        var result = Parser.Parse("రేపు ఉదయం డాక్టర్ కి కాల్ చేయాలని గుర్తు చేయి", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateReminder);
        result.Language.Should().Be("te-IN");
        result.Date.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_CreateNote()
    {
        var result = Parser.Parse("Take a note: buy milk and eggs", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateNote);
        result.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_CreateTask_WithPriority()
    {
        var result = Parser.Parse("Add urgent task to submit the report by tomorrow", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateTask);
        result.Priority.Should().Be("Urgent");
        result.Date.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_Complete_Task()
    {
        var result = Parser.Parse("Mark task as done - submit report", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CompleteTask);
    }

    [Fact]
    public void Parse_Create_Appointment()
    {
        var result = Parser.Parse("Schedule a meeting with Rahul tomorrow at 3 pm at office", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateAppointment);
        result.Time.Should().Be("15:00");
        result.Participants.Should().Contain("Rahul");
    }

    [Fact]
    public void Parse_Recurring_Reminder()
    {
        var result = Parser.Parse("Remind me every day to take medicine", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateReminder);
        result.Recurrence.Should().Be("Daily");
    }

    [Fact]
    public void Parse_Help()
    {
        var result = Parser.Parse("help", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.Help);
    }

    [Fact]
    public void Parse_Change_Language()
    {
        var result = Parser.Parse("change language to hindi", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.ChangeLanguage);
        result.TargetLanguage.Should().Be("hi-IN");
    }

    [Fact]
    public void Parse_WakeWord_Stripped()
    {
        var result = Parser.Parse("Hey assistant remind me to call mom tonight", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateReminder);
        result.Date.Should().NotBeNullOrEmpty();
        result.Time.Should().Be("21:00");
    }

    [Fact]
    public void Parse_List_Tasks()
    {
        var result = Parser.Parse("show my tasks", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.ListTasks);
    }

    [Fact]
    public void Parse_Schedule_Appointment_TimeOnly_HasNoPastDate()
    {
        var result = Parser.Parse("schedule appointment at 3:00PM", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateAppointment);
        result.Time.Should().Be("15:00");
        result.Date.Should().BeNull();
    }

    [Fact]
    public void Parse_Add_Task_CleansTitle()
    {
        var result = Parser.Parse("Add a task to buy groceries", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateTask);
        result.Title.Should().Be("Buy groceries");
    }

    [Fact]
    public void Parse_Take_Note_ExtractsTitleAndContent()
    {
        var result = Parser.Parse("Take a note: buy milk and eggs", Guid.NewGuid(), "Asia/Kolkata");

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateNote);
        result.Title.Should().NotBeNullOrEmpty();
        result.Content.Should().Contain("buy milk");
    }

    [Fact]
    public void Parse_Schedule_Appointment_TimeOnly_WhenPastRollsToTomorrow()
    {
        var result = Parser.Parse("schedule appointment at 3:00PM", Guid.NewGuid(), "Asia/Kolkata");
        var now = new DateTime(2026, 8, 16, 18, 0, 0); // already 6 PM

        var resolved = DateTimeResolver.Resolve(result.Date, result.Time, now);

        resolved.Should().Be(new DateTime(2026, 8, 17, 15, 0, 0));
    }

    [Fact]
    public void Parse_Reminder_PastMorning_RollsToTomorrow()
    {
        var result = Parser.Parse("remind me to call mom at 9 am", Guid.NewGuid(), "Asia/Kolkata");
        var now = new DateTime(2026, 8, 16, 10, 0, 0); // already 10 AM

        result.Intent.Should().Be(MyAssistant.Application.AI.AssistantIntents.CreateReminder);
        result.Time.Should().Be("09:00");
        var resolved = DateTimeResolver.Resolve(result.Date, result.Time, now);

        resolved.Should().Be(new DateTime(2026, 8, 17, 9, 0, 0));
    }
}

public class DateTimeResolverTests
{
    [Fact]
    public void ParseTime_AmPm()
    {
        var ctx = new CommandContext("meeting at 6 PM tomorrow", "en-IN");
        DateTimeResolver.ParseTime(ctx).Should().Be("18:00");
    }

    [Fact]
    public void ParseTime_24Hour()
    {
        var ctx = new CommandContext("reminder at 14:30", "en-IN");
        DateTimeResolver.ParseTime(ctx).Should().Be("14:30");
    }

    [Fact]
    public void ParseTime_Hindi_Baje()
    {
        var ctx = new CommandContext("रात 9 बजे", "hi-IN");
        DateTimeResolver.ParseTime(ctx).Should().Be("21:00");
    }

    [Fact]
    public void ParseDate_Tomorrow()
    {
        var ctx = new CommandContext("meeting tomorrow", "en-IN");
        var date = DateTimeResolver.ParseDate(ctx);
        date.Should().Be(DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void ParseDate_Today()
    {
        var ctx = new CommandContext("call today", "en-IN");
        var date = DateTimeResolver.ParseDate(ctx);
        date.Should().Be(ConvertToIst(DateTime.UtcNow).ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void Resolve_NoDateNoTime_ReturnsNull()
    {
        var now = new DateTime(2026, 8, 16, 18, 0, 0);
        DateTimeResolver.Resolve(null, null, now).Should().BeNull();
    }

    [Fact]
    public void Resolve_PastTime_RollsToTomorrow()
    {
        var now = new DateTime(2026, 8, 16, 18, 0, 0);
        DateTimeResolver.Resolve(null, "15:00", now).Should().Be(new DateTime(2026, 8, 17, 15, 0, 0));
    }

    [Fact]
    public void Resolve_TodayPastTime_RollsToTomorrow()
    {
        var now = new DateTime(2026, 8, 16, 18, 0, 0);
        DateTimeResolver.Resolve("2026-08-16", "15:00", now).Should().Be(new DateTime(2026, 8, 17, 15, 0, 0));
    }

    [Fact]
    public void Resolve_FutureTime_StaysSameDay()
    {
        var now = new DateTime(2026, 8, 16, 18, 0, 0);
        DateTimeResolver.Resolve(null, "19:00", now).Should().Be(new DateTime(2026, 8, 16, 19, 0, 0));
    }

    [Fact]
    public void Resolve_ExplicitFutureDate_Unchanged()
    {
        var now = new DateTime(2026, 8, 16, 18, 0, 0);
        DateTimeResolver.Resolve("2026-08-17", "09:00", now).Should().Be(new DateTime(2026, 8, 17, 9, 0, 0));
    }

    [Fact]
    public void Resolve_DateOnly_DefaultsToNineAm()
    {
        var now = new DateTime(2026, 8, 16, 18, 0, 0);
        DateTimeResolver.Resolve("2026-08-17", null, now).Should().Be(new DateTime(2026, 8, 17, 9, 0, 0));
    }

    [Fact]
    public void Resolve_ExactNow_RollsForward()
    {
        var now = new DateTime(2026, 8, 16, 18, 0, 0);
        DateTimeResolver.Resolve("2026-08-16", "18:00", now).Should().Be(new DateTime(2026, 8, 17, 18, 0, 0));
    }

    private static DateTime ConvertToIst(DateTime utc)
        => TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
}

public class RecurrenceParserTests
{
    [Theory]
    [InlineData("every day", "Daily")]
    [InlineData("every week", "Weekly")]
    [InlineData("every month", "Monthly")]
    [InlineData("", "Once")]
    public void Parse_Recurrence(string text, string expected)
        => RecurrenceParser.Parse(new CommandContext(text, "en-IN")).Should().Be(expected);
}

public class DurationParserTests
{
    [Theory]
    [InlineData("meeting for 45 minutes", 45)]
    [InlineData("meeting for 2 hours", 120)]
    [InlineData("no duration", null)]
    public void Parse_Duration(string text, int? expected)
        => DurationParser.Parse(new CommandContext(text, "en-IN")).Should().Be(expected);
}