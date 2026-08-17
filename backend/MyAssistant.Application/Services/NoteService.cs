using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Notes;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _notes;
    private readonly ISubscriptionService _subscriptionService;

    public NoteService(INoteRepository notes, ISubscriptionService subscriptionService)
    {
        _notes = notes;
        _subscriptionService = subscriptionService;
    }

    public async Task<IReadOnlyList<NoteDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notes = await _notes.GetForUserAsync(userId, cancellationToken);
        return notes
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<NoteDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _notes.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Note not found.");
        return ToDto(note);
    }

    public async Task<NoteDto> CreateAsync(Guid userId, CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _subscriptionService.CanUseFeatureAsync(userId, Domain.Enums.UsageType.Note, cancellationToken))
        {
            throw new AppException("You have reached your note limit. Please upgrade your plan.", 403);
        }

        var note = new Note
        {
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? DeriveTitle(request.Content) : request.Title,
            Content = request.Content,
            OriginalLanguage = request.OriginalLanguage,
            Tags = request.Tags
        };
        await _notes.AddAsync(note, cancellationToken);
        await _subscriptionService.RecordUsageAsync(userId, Domain.Enums.UsageType.Note, cancellationToken: cancellationToken);
        return ToDto(note);
    }

    public async Task<NoteDto> UpdateAsync(Guid userId, Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var note = await _notes.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Note not found.");
        note.Title = request.Title;
        note.Content = request.Content;
        note.Tags = request.Tags;
        note.IsPinned = request.IsPinned;
        note.UpdatedAt = DateTime.UtcNow;
        await _notes.UpdateAsync(note, cancellationToken);
        return ToDto(note);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _notes.GetForUserByIdAsync(userId, id, cancellationToken)
                   ?? throw new NotFoundException("Note not found.");
        await _notes.DeleteAsync(note, cancellationToken);
    }

    internal static string DeriveTitle(string content)
    {
        var firstLine = content.Trim().Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
        return firstLine.Length > 60 ? firstLine[..60] : firstLine;
    }

    internal static NoteDto ToDto(Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        OriginalLanguage = note.OriginalLanguage,
        Tags = note.Tags,
        IsPinned = note.IsPinned,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt
    };
}
