using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Tasks;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _tasks;
    private readonly ISubscriptionService _subscriptionService;

    public TaskService(ITaskRepository tasks, ISubscriptionService subscriptionService)
    {
        _tasks = tasks;
        _subscriptionService = subscriptionService;
    }

    public async Task<IReadOnlyList<TaskDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tasks = await _tasks.GetForUserAsync(userId, cancellationToken);
        return tasks
            .OrderBy(t => t.Status == Domain.Enums.TaskStatus.Completed)
            .ThenBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.DueTime ?? TimeOnly.MaxValue)
            .Select(ToDto)
            .ToList();
    }

    public async Task<TaskDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Task not found.");
        return ToDto(task);
    }

    public async Task<TaskDto> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _subscriptionService.CanUseFeatureAsync(userId, Domain.Enums.UsageType.Task, cancellationToken))
        {
            throw new AppException("You have reached your task limit. Please upgrade your plan.", 403);
        }

        var task = new TaskItem
        {
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Status = request.Status,
            DueDate = request.DueDate,
            DueTime = request.DueTime,
            Category = request.Category
        };
        await _tasks.AddAsync(task, cancellationToken);
        await _subscriptionService.RecordUsageAsync(userId, Domain.Enums.UsageType.Task, cancellationToken: cancellationToken);
        return ToDto(task);
    }

    public async Task<TaskDto> UpdateAsync(Guid userId, Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Task not found.");
        task.Title = request.Title;
        task.Description = request.Description;
        task.Priority = request.Priority;
        task.Status = request.Status;
        task.DueDate = request.DueDate;
        task.DueTime = request.DueTime;
        task.Category = request.Category;
        task.UpdatedAt = DateTime.UtcNow;
        if (request.Status == Domain.Enums.TaskStatus.Completed && task.CompletedDate == null)
        {
            task.CompletedDate = DateTime.UtcNow;
        }
        await _tasks.UpdateAsync(task, cancellationToken);
        return ToDto(task);
    }

    public async Task<TaskDto> UpdateStatusAsync(Guid userId, Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Task not found.");
        task.Status = request.Status;
        task.CompletedDate = request.Status == Domain.Enums.TaskStatus.Completed ? DateTime.UtcNow : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _tasks.UpdateAsync(task, cancellationToken);
        return ToDto(task);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Task not found.");
        await _tasks.DeleteAsync(task, cancellationToken);
    }

    internal static TaskDto ToDto(TaskItem task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Priority = task.Priority,
        Status = task.Status,
        DueDate = task.DueDate,
        DueTime = task.DueTime,
        CompletedDate = task.CompletedDate,
        Category = task.Category,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt
    };
}
