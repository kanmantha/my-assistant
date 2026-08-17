using FluentAssertions;
using Moq;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Notes;
using MyAssistant.Application.DTOs.Tasks;
using MyAssistant.Application.Interfaces;
using MyAssistant.Application.Services;
using MyAssistant.Domain.Entities;
using MyAssistant.Domain.Enums;
using TaskStatus = MyAssistant.Domain.Enums.TaskStatus;

namespace MyAssistant.Tests;

public class DateTimeParserServiceTests
{
    private static DateTimeParserService CreateParser(DateTime now)
    {
        var time = new Mock<ITimeZoneService>();
        time.Setup(t => t.NowInTimeZone(It.IsAny<string?>())).Returns(now);
        return new DateTimeParserService(time.Object);
    }

    [Fact]
    public async Task Parse_English_TomorrowAt5Pm_ReturnsExpected()
    {
        var parser = CreateParser(new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        var result = await parser.ParseAsync("tomorrow at 5pm", "en", "Asia/Kolkata");
        result.HasDate.Should().BeTrue();
        result.HasTime.Should().BeTrue();
        result.DateTime.Should().Be(new DateTime(2026, 8, 14, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Parse_Hindi_KalSubah_ReturnsTomorrow()
    {
        var parser = CreateParser(new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        var result = await parser.ParseAsync("कल सुबह 9 बजे", "hi", "Asia/Kolkata");
        result.HasDate.Should().BeTrue();
        result.HasTime.Should().BeTrue();
        result.DateTime.Should().Be(new DateTime(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Parse_Telugu_Repu_ReturnsTomorrow()
    {
        var parser = CreateParser(new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        var result = await parser.ParseAsync("రేపు సాయంత్రం 6 గంటలకు", "te", "Asia/Kolkata");
        result.HasDate.Should().BeTrue();
        result.HasTime.Should().BeTrue();
        result.DateTime.Should().Be(new DateTime(2026, 8, 14, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Parse_GarbageText_ReturnsNoDateOrTime()
    {
        var parser = CreateParser(new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc));
        var result = await parser.ParseAsync("just some random words", "en", "Asia/Kolkata");
        result.HasDate.Should().BeFalse();
        result.HasTime.Should().BeFalse();
    }
}

public class SubscriptionServiceTests
{
    private static (SubscriptionService service, Mock<ISubscriptionRepository> subs, Mock<IUsageRepository> usage) Build(
        SubscriptionTier tier = SubscriptionTier.Free, int usageCount = 0)
    {
        var subs = new Mock<ISubscriptionRepository>();
        var usage = new Mock<IUsageRepository>();
        subs.Setup(s => s.GetActiveByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription { UserId = Guid.NewGuid(), Tier = tier, Status = SubscriptionStatus.Active });
        usage.Setup(u => u.CountSinceAsync(It.IsAny<Guid>(), It.IsAny<UsageType>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageCount);
        var service = new SubscriptionService(subs.Object, usage.Object);
        return (service, subs, usage);
    }

    [Fact]
    public async Task FreeTier_BelowNoteLimit_CanUseFeature()
    {
        var (service, _, _) = Build(tier: SubscriptionTier.Free, usageCount: 10);
        var result = await service.CanUseFeatureAsync(Guid.NewGuid(), UsageType.Note);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FreeTier_AtNoteLimit_BlocksFeature()
    {
        var (service, _, _) = Build(tier: SubscriptionTier.Free, usageCount: 50);
        var result = await service.CanUseFeatureAsync(Guid.NewGuid(), UsageType.Note);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PaidTier_AlwaysCanUseFeature()
    {
        var (service, _, _) = Build(tier: SubscriptionTier.Pro, usageCount: int.MaxValue);
        var result = await service.CanUseFeatureAsync(Guid.NewGuid(), UsageType.Note);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RecordUsage_AddsUsageRecord()
    {
        var (service, _, usage) = Build();
        await service.RecordUsageAsync(Guid.NewGuid(), UsageType.Task, "via-voice");
        usage.Verify(u => u.AddAsync(It.Is<UsageRecord>(r => r.Type == UsageType.Task && r.Metadata == "via-voice"), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class NoteServiceTests
{
    [Fact]
    public async Task Create_BeyondFreeLimit_Throws()
    {
        var userId = Guid.NewGuid();
        var notes = new Mock<INoteRepository>();
        var subscription = new Mock<ISubscriptionService>();
        subscription.Setup(s => s.CanUseFeatureAsync(userId, UsageType.Note, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = new NoteService(notes.Object, subscription.Object);

        await service.Invoking(s => s.CreateAsync(userId, new CreateNoteRequest { Title = "T", Content = "C" }, default))
            .Should().ThrowAsync<AppException>().WithMessage("*note limit*");
    }

    [Fact]
    public async Task Create_WithinLimit_SavesNote()
    {
        var userId = Guid.NewGuid();
        var notes = new Mock<INoteRepository>();
        var subscription = new Mock<ISubscriptionService>();
        subscription.Setup(s => s.CanUseFeatureAsync(userId, UsageType.Note, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new NoteService(notes.Object, subscription.Object);

        var created = await service.CreateAsync(userId, new CreateNoteRequest { Title = "Grocery", Content = "Milk, eggs" }, default);

        created.Title.Should().Be("Grocery");
        notes.Verify(n => n.AddAsync(It.IsAny<Note>(), It.IsAny<CancellationToken>()), Times.Once);
        subscription.Verify(s => s.RecordUsageAsync(userId, UsageType.Note, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class SettingsDefaultTests
{
    [Fact]
    public void NewUserSettings_WakeWordEnabled_IsTrueByDefault()
    {
        var settings = new UserSettings();
        settings.WakeWordEnabled.Should().BeTrue();
    }
}

public class TaskServiceTests
{
    [Fact]
    public async Task UpdateStatus_ToCompleted_SetsCompletedDate()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(t => t.GetForUserByIdAsync(userId, taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskItem { Id = taskId, UserId = userId, Title = "Ship it" });
        var subscription = new Mock<ISubscriptionService>();
        var service = new TaskService(tasks.Object, subscription.Object);

        var result = await service.UpdateStatusAsync(userId, taskId, new UpdateTaskStatusRequest { Status = TaskStatus.Completed }, default);

        result.Status.Should().Be(TaskStatus.Completed);
        result.CompletedDate.Should().NotBeNull();
    }
}
