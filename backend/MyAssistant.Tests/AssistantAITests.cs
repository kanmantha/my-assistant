using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MyAssistant.Application.DTOs.Appointments;
using MyAssistant.Application.DTOs.Assistant;
using MyAssistant.Application.DTOs.Notes;
using MyAssistant.Application.DTOs.Reminders;
using MyAssistant.Application.DTOs.Settings;
using MyAssistant.Application.DTOs.Tasks;
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

    [Fact]
    public async Task Weather_English_IsWeatherIntent()
    {
        var parsed = await Parse("what is the weather in Hyderabad", "en");
        parsed.Intent.Should().Be(AssistantIntent.Weather);
    }

    [Fact]
    public async Task Weather_Hindi_IsWeatherIntent()
    {
        var parsed = await Parse("हैदराबाद का मौसम कैसा है", "hi");
        parsed.Intent.Should().Be(AssistantIntent.Weather);
    }

    [Fact]
    public async Task WebSearch_English_IsWebSearchIntent()
    {
        var parsed = await Parse("search the web for the latest AI news", "en");
        parsed.Intent.Should().Be(AssistantIntent.WebSearch);
        parsed.SearchQuery.Should().Contain("AI");
    }

    [Fact]
    public async Task GeneralQuestion_EndsWithQuestionMark_IsGeneralQuestion()
    {
        var parsed = await Parse("why is the sky blue?", "en");
        parsed.Intent.Should().Be(AssistantIntent.GeneralQuestion);
    }

    [Fact]
    public async Task GeneralQuestion_ExplainPhrase_IsGeneralQuestion()
    {
        var parsed = await Parse("explain photosynthesis", "en");
        parsed.Intent.Should().Be(AssistantIntent.GeneralQuestion);
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

    [Fact]
    public async Task GeneralQuestion_PassesThroughToAiAnswer()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        ai.Setup(a => a.ParseCommandAsync("What is the capital of India?", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedCommand { Intent = AssistantIntent.GeneralQuestion, Language = "en-IN", Title = "What is the capital of India?" });
        ai.Setup(a => a.AnswerQuestionAsync("What is the capital of India?", "en-IN", It.IsAny<CancellationToken>()))
            .ReturnsAsync("The capital of India is New Delhi.");
        var service = BuildService(ai, out var subscription);

        var result = await service.ProcessAsync(new AssistantRequest { Text = "What is the capital of India?", Language = "en" }, Guid.NewGuid());

        result.Intent.Should().Be(AssistantIntent.GeneralQuestion.ToString());
        result.Reply.Should().Be("The capital of India is New Delhi.");
        ai.Verify(a => a.AnswerQuestionAsync("What is the capital of India?", "en-IN", It.IsAny<CancellationToken>()), Times.Once);
        subscription.Verify(s => s.RecordUsageAsync(It.IsAny<Guid>(), UsageType.AiCommand, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GeneralQuestion_AiFails_FallsBackToNotUnderstood()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        ai.Setup(a => a.ParseCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedCommand { Intent = AssistantIntent.WebSearch, Language = "en-IN", Title = "search for something" });
        ai.Setup(a => a.AnswerQuestionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider down"));
        var service = BuildService(ai, out _);

        var result = await service.ProcessAsync(new AssistantRequest { Text = "search for something", Language = "en" }, Guid.NewGuid());

        result.Reply.Should().Be(AssistantReplies.NotUnderstood("en-IN"));
        result.Intent.Should().Be(AssistantIntent.WebSearch.ToString());
    }
}

public class AssistantServiceGuidedCaptureTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _session = "capture-test";

    // Canned intents: a bare "Add Task"/"Add Note"/"Schedule meeting" plus date/category/title responses.
    private static ParsedCommand IntentFor(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        if (lower.StartsWith("add task")) return new ParsedCommand { Intent = AssistantIntent.CreateTask };
        if (lower.StartsWith("add note")) return new ParsedCommand { Intent = AssistantIntent.CreateNote };
        if (lower.StartsWith("schedule meeting")) return new ParsedCommand { Intent = AssistantIntent.CreateAppointment };
        if (lower.Contains("tomorrow")) return new ParsedCommand { Intent = AssistantIntent.TomorrowSchedule };
        if (lower.Contains("reminders") || lower.Contains("tasks") || lower.Contains("appointments")) return new ParsedCommand { Intent = AssistantIntent.ListReminders };
        return new ParsedCommand { Intent = AssistantIntent.Unknown };
    }

    private AssistantService BuildService(
        Mock<IAssistantAIService> ai,
        out InMemoryAssistantSessionStore sessions,
        Mock<INoteService>? notes = null,
        Mock<ITaskService>? tasks = null,
        Mock<IAppointmentService>? appointments = null)
    {
        sessions = new InMemoryAssistantSessionStore();
        var time = new Mock<ITimeZoneService>();
        time.Setup(t => t.NowInTimeZone(It.IsAny<string>())).Returns(new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        var dateTimeParser = new DateTimeParserService(time.Object);
        var conversations = new Mock<IConversationRepository>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSettingsDto { TimeZone = "Asia/Kolkata", Language = "en" });
        var subscription = new Mock<ISubscriptionService>();
        var logger = new Mock<ILogger<AssistantService>>();
        notes ??= new Mock<INoteService>();
        tasks ??= new Mock<ITaskService>();
        var reminders = new Mock<IReminderService>();
        reminders.Setup(r => r.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ReminderDto>());
        appointments ??= new Mock<IAppointmentService>();
        var search = new Mock<ISearchService>();
        return new AssistantService(
            ai.Object, sessions, time.Object, dateTimeParser, settings.Object, conversations.Object,
            subscription.Object, logger.Object, notes.Object, tasks.Object, reminders.Object,
            appointments.Object, search.Object);
    }

    private AssistantRequest Req(string text) => new() { Text = text, Language = "en", SessionId = _session };

    private async Task<AssistantResponse> Turn(Mock<IAssistantAIService> ai, AssistantService service, string text)
    {
        ai.Setup(a => a.ParseCommandAsync(text, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentFor(text));
        return await service.ProcessAsync(Req(text), _userId);
    }

    [Fact]
    public async Task AddNote_Bare_AsksCategory_ThenContent_ThenCreatesNoteWithTag()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateNoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid uid, CreateNoteRequest req, CancellationToken _) => new NoteDto
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                Content = req.Content,
                Tags = req.Tags
            });
        var service = BuildService(ai, out var sessions, notes: notes);

        var r1 = await Turn(ai, service, "Add Note");
        r1.CaptureType.Should().Be("category");
        r1.Reply.Should().Contain("category");

        var r2 = await Turn(ai, service, "Work");
        r2.CaptureType.Should().Be("text");
        r2.Reply.Should().Contain("note");

        var r3 = await Turn(ai, service, "buy milk and eggs");
        r3.CaptureType.Should().BeNull();
        r3.Reply.Should().Contain("saved");

        notes.Verify(n => n.CreateAsync(_userId,
            It.Is<CreateNoteRequest>(req => req.Content == "buy milk and eggs" && req.Tags.Contains("Work")),
            It.IsAny<CancellationToken>()), Times.Once);
        sessions.Get(_userId, _session).Should().BeNull();
    }

    [Fact]
    public async Task AddTask_Bare_AsksDate_CapturesTomorrow_ThenCategory_ThenTitle_CreatesTask()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid uid, CreateTaskRequest req, CancellationToken _) => new TaskDto
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                DueDate = req.DueDate,
                Category = req.Category
            });
        var service = BuildService(ai, out var sessions, tasks: tasks);

        var r1 = await Turn(ai, service, "Add Task");
        r1.CaptureType.Should().Be("date");
        r1.Reply.Should().Contain("date");

        // "tomorrow" parses as TomorrowSchedule intent but must be captured as the date.
        var r2 = await Turn(ai, service, "tomorrow");
        r2.CaptureType.Should().Be("category");
        r2.Reply.Should().Contain("category");

        var r3 = await Turn(ai, service, "Personal");
        r3.CaptureType.Should().Be("text");
        r3.Reply.Should().Contain("task");

        var r4 = await Turn(ai, service, "Prepare monthly report");
        r4.CaptureType.Should().BeNull();
        r4.Reply.Should().Contain("added");

        tasks.Verify(t => t.CreateAsync(_userId,
            It.Is<CreateTaskRequest>(req =>
                req.Title == "Prepare monthly report" &&
                req.Category == "Personal" &&
                req.DueDate == DateOnly.FromDateTime(new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc))),
            It.IsAny<CancellationToken>()), Times.Once);
        sessions.Get(_userId, _session).Should().BeNull();
    }

    [Fact]
    public async Task AddTask_SkipDate_AdvancesToCategory_AndCreatesTaskWithoutDate()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid uid, CreateTaskRequest req, CancellationToken _) => new TaskDto
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                DueDate = req.DueDate
            });
        var service = BuildService(ai, out var sessions, tasks: tasks);

        var r1 = await Turn(ai, service, "Add Task");
        r1.CaptureType.Should().Be("date");

        var r2 = await Turn(ai, service, "skip");
        r2.CaptureType.Should().Be("category");

        var r3 = await Turn(ai, service, "Work");
        r3.CaptureType.Should().Be("text");

        var r4 = await Turn(ai, service, "File taxes");
        r4.CaptureType.Should().BeNull();

        tasks.Verify(t => t.CreateAsync(_userId,
            It.Is<CreateTaskRequest>(req => req.Title == "File taxes" && req.DueDate == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CaptureActive_RealCommand_SupersedesPendingFlow()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        var service = BuildService(ai, out var sessions);

        var r1 = await Turn(ai, service, "Add Note");
        r1.CaptureType.Should().Be("category");

        // A genuine list command while mid-capture must be dispatched, not consumed as a category.
        var r2 = await Turn(ai, service, "Today Tasks Reminders");
        r2.Intent.Should().Be(AssistantIntent.ListReminders.ToString());
        sessions.Get(_userId, _session).Should().BeNull();
    }

    [Fact]
    public async Task ScheduleMeeting_Bare_AsksDate_ThenTitle_ThenConfirmation()
    {
        var ai = new Mock<IAssistantAIService>();
        ai.Setup(a => a.DetectLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("en-IN");
        var appointments = new Mock<IAppointmentService>();
        appointments.Setup(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateAppointmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid uid, CreateAppointmentRequest req, CancellationToken _) => new AppointmentDto
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                StartDateTime = req.StartDateTime,
                EndDateTime = req.EndDateTime ?? req.StartDateTime
            });
        var service = BuildService(ai, out var sessions, appointments: appointments);

        var r1 = await Turn(ai, service, "Schedule meeting");
        r1.CaptureType.Should().Be("date");

        var r2 = await Turn(ai, service, "tomorrow");
        r2.CaptureType.Should().Be("text");
        r2.Reply.Should().Contain("meeting");

        var r3 = await Turn(ai, service, "Team standup");
        r3.NeedsConfirmation.Should().BeTrue();
        r3.PendingAction.Should().Be(AssistantIntent.CreateAppointment.ToString());

        var r4 = await Turn(ai, service, "yes");
        r4.NeedsConfirmation.Should().BeFalse();

        appointments.Verify(a => a.CreateAsync(_userId,
            It.Is<CreateAppointmentRequest>(req => req.Title == "Team standup"),
            It.IsAny<CancellationToken>()), Times.Once);
        sessions.Get(_userId, _session).Should().BeNull();
    }
}
