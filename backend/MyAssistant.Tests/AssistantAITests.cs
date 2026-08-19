using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MyAssistant.Application.DTOs.Assistant;
using MyAssistant.Application.DTOs.Settings;
using MyAssistant.Application.Interfaces;
using MyAssistant.Application.Services;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Tests;

public class HeuristicAIServiceTests
{
    private static HeuristicAIService CreateService()
    {
        var time = new Mock<ITimeZoneService>();
        time.Setup(t => t.NowInTimeZone(It.IsAny<string?>())).Returns(new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        var parser = new DateTimeParserService(time.Object);
        return new HeuristicAIService(parser);
    }

    private static async Task<ParsedCommand> Parse(string text, string lang)
    {
        return await CreateService().ParseCommandAsync(text, lang, "Asia/Kolkata");
    }

    [Fact]
    public async Task Recurrence_English_EveryDay_IsDaily()
    {
        var parsed = await Parse("remind me to water the plants every day", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateReminder);
        parsed.Recurrence.Should().Be(RecurrenceType.Daily);
    }

    [Fact]
    public async Task Recurrence_English_EveryMonday_IsWeekly()
    {
        var parsed = await Parse("remind me to call mom every monday", "en");
        parsed.Recurrence.Should().Be(RecurrenceType.Weekly);
    }

    [Fact]
    public async Task Recurrence_English_FirstOfEveryMonth_IsMonthly()
    {
        var parsed = await Parse("remind me to pay rent on the 1st of every month", "en");
        parsed.Recurrence.Should().Be(RecurrenceType.Monthly);
    }

    [Fact]
    public async Task Recurrence_English_EveryYear_IsYearly()
    {
        var parsed = await Parse("remind me to renew my license every year", "en");
        parsed.Recurrence.Should().Be(RecurrenceType.Yearly);
    }

    [Fact]
    public async Task Recurrence_English_NoRecurrence_IsOnce()
    {
        var parsed = await Parse("remind me to buy milk", "en");
        parsed.Recurrence.Should().Be(RecurrenceType.Once);
    }

    [Fact]
    public async Task Recurrence_Hindi_Roz_IsDaily()
    {
        var parsed = await Parse("मुझे रोज़ पानी पीने की याद दिलाना", "hi");
        parsed.Intent.Should().Be(AssistantIntent.CreateReminder);
        parsed.Recurrence.Should().Be(RecurrenceType.Daily);
    }

    [Fact]
    public async Task Recurrence_Telugu_PratiRoju_IsDaily()
    {
        var parsed = await Parse("ప్రతి రోజు మందు తీసుకోమని గుర్తు చేయి", "te");
        parsed.Intent.Should().Be(AssistantIntent.CreateReminder);
        parsed.Recurrence.Should().Be(RecurrenceType.Daily);
    }

    [Fact]
    public async Task DetectLanguage_DevanagariScript_IsHindi()
    {
        var service = CreateService();
        (await service.DetectLanguageAsync("नमस्ते, आप कैसे हैं")).Should().Be("hi-IN");
    }

    [Fact]
    public async Task DetectLanguage_TeluguScript_IsTelugu()
    {
        var service = CreateService();
        (await service.DetectLanguageAsync("నమస్కారం, మీరు ఎలా ఉన్నారు")).Should().Be("te-IN");
    }

    [Fact]
    public async Task DetectLanguage_LatinScript_IsEnglish()
    {
        var service = CreateService();
        (await service.DetectLanguageAsync("hello, how are you")).Should().Be("en-IN");
    }

    [Fact]
    public async Task Note_CalledAndSaying_SplitsTitleAndContent()
    {
        var parsed = await Parse("create a note called Grocery List saying buy milk eggs and bread", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateNote);
        parsed.Title.Should().Be("Grocery List");
        parsed.Content.Should().Be("buy milk eggs and bread");
    }

    [Fact]
    public async Task Note_CalledWithoutSaying_TitleIsName()
    {
        var parsed = await Parse("add a note called Ideas for the project", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateNote);
        parsed.Title.Should().Be("Ideas for the project");
        parsed.Content.Should().Be("Ideas for the project");
    }

    [Fact]
    public async Task Note_PlainPhrase_UsesWholePhrase()
    {
        var parsed = await Parse("take a note about the quarterly review", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateNote);
        parsed.Title.Should().Be("about the quarterly review");
    }

    [Fact]
    public async Task Note_BareAddNote_IsCreateNoteWithoutContent()
    {
        var parsed = await Parse("add note", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateNote);
        parsed.Title.Should().BeEmpty();
        parsed.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task Note_BareAddNoteWithContent_IsCreateNote()
    {
        var parsed = await Parse("add note buy milk and eggs", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateNote);
        parsed.Title.Should().Be("buy milk and eggs");
    }

    [Fact]
    public async Task Task_BareAddTask_IsCreateTaskWithoutTitle()
    {
        var parsed = await Parse("add task", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateTask);
        parsed.Title.Should().BeEmpty();
    }

    [Fact]
    public async Task Task_BareAddTaskWithContent_IsCreateTask()
    {
        var parsed = await Parse("add task prepare the monthly report", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateTask);
        parsed.Title.Should().Be("prepare the monthly report");
    }

    [Fact]
    public async Task List_TodayTasksReminders_IsListRemindersScopedToday()
    {
        var parsed = await Parse("Today Tasks Reminders", "en");
        parsed.Intent.Should().Be(AssistantIntent.ListReminders);
        parsed.Scope.Should().Be("today");
    }

    [Fact]
    public async Task List_TodayReminders_IsListRemindersScopedToday()
    {
        var parsed = await Parse("today reminders", "en");
        parsed.Intent.Should().Be(AssistantIntent.ListReminders);
        parsed.Scope.Should().Be("today");
    }

    [Fact]
    public async Task List_TodaysAppointments_IsListAppointmentsScopedToday()
    {
        var parsed = await Parse("todays appointments", "en");
        parsed.Intent.Should().Be(AssistantIntent.ListAppointments);
        parsed.Scope.Should().Be("today");
    }

    [Fact]
    public async Task List_TomorrowReminders_IsListRemindersScopedTomorrow()
    {
        var parsed = await Parse("tomorrow reminders", "en");
        parsed.Intent.Should().Be(AssistantIntent.ListReminders);
        parsed.Scope.Should().Be("tomorrow");
    }

    [Fact]
    public async Task CreateReminder_StillWorksWithTodayKeyword()
    {
        var parsed = await Parse("remind me to water the plants today at 5pm", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateReminder);
    }

    [Fact]
    public async Task CreateAppointment_StillWorksWhenSchedulingMeeting()
    {
        var parsed = await Parse("schedule a meeting with Ravi tomorrow at 10am", "en");
        parsed.Intent.Should().Be(AssistantIntent.CreateAppointment);
    }
}

public class AssistantServiceAutoLanguageTests
{
    private static AssistantService BuildService(
        Mock<IAssistantAIService> ai,
        out Mock<ISubscriptionService> subscription)
    {
        var sessions = new Mock<IAssistantSessionStore>();
        var time = new Mock<ITimeZoneService>();
        time.Setup(t => t.NowInTimeZone(It.IsAny<string>())).Returns(DateTime.Now);
        var dateTimeParser = new DateTimeParserService(time.Object);
        var conversations = new Mock<IConversationRepository>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSettingsDto { TimeZone = "Asia/Kolkata", Language = "en" });
        subscription = new Mock<ISubscriptionService>();
        var logger = new Mock<ILogger<AssistantService>>();
        var notes = new Mock<INoteService>();
        var tasks = new Mock<ITaskService>();
        var reminders = new Mock<IReminderService>();
        var appointments = new Mock<IAppointmentService>();
        var search = new Mock<ISearchService>();
        return new AssistantService(
            ai.Object, sessions.Object, time.Object, dateTimeParser, settings.Object, conversations.Object,
            subscription.Object, logger.Object, notes.Object, tasks.Object, reminders.Object,
            appointments.Object, search.Object);
    }

    [Fact]
    public async Task AutoLanguage_DetectsHindi_RepliesInHindi()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync("नमस्ते", It.IsAny<CancellationToken>())).ReturnsAsync("hi-IN");
        ai.Setup(a => a.ParseCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedCommand { Intent = AssistantIntent.Greeting, Language = "hi-IN" });
        var service = BuildService(ai, out var subscription);

        var result = await service.ProcessAsync(new AssistantRequest { Text = "नमस्ते", Language = "auto" }, Guid.NewGuid());

        result.Language.Should().Be("hi-IN");
        result.Reply.Should().Be(AssistantReplies.GreetingHi);
        ai.Verify(a => a.DetectLanguageAsync("नमस्ते", It.IsAny<CancellationToken>()), Times.Once);
        ai.Verify(a => a.ParseCommandAsync("नमस्ते", "hi-IN", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        subscription.Verify(s => s.RecordUsageAsync(It.IsAny<Guid>(), UsageType.AiCommand, "नमस्ते", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExplicitLanguage_SkipsLanguageDetection()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.ParseCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedCommand { Intent = AssistantIntent.Greeting, Language = "en-IN" });
        var service = BuildService(ai, out _);

        var result = await service.ProcessAsync(new AssistantRequest { Text = "Good morning", Language = "en" }, Guid.NewGuid());

        result.Language.Should().Be("en-IN");
        result.Reply.Should().Be(AssistantReplies.Greeting);
        ai.Verify(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        ai.Verify(a => a.ParseCommandAsync("Good morning", "en-IN", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidAiProvider_FallsBackToUnknownIntent_WithoutThrowing()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        ai.Setup(a => a.ParseCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider down"));
        var service = BuildService(ai, out _);

        var result = await service.ProcessAsync(new AssistantRequest { Text = "hello there", Language = "auto" }, Guid.NewGuid());

        result.Should().NotBeNull();
        result.Intent.Should().Be(AssistantIntent.Unknown.ToString());
        result.Language.Should().Be("en-IN");
    }
}
