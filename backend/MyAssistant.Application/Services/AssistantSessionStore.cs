using System.Collections.Concurrent;
using MyAssistant.Application.DTOs.Assistant;

namespace MyAssistant.Application.Services;

/// <summary>
/// In-memory, per-session pending-action store used for multi-turn assistant
/// confirmations (e.g. "Should I schedule the meeting?" -> "Yes").
/// </summary>
public class PendingAction
{
    public ParsedCommand Command { get; init; } = new();

    public int Stage { get; set; } = 1;

    public string SessionId { get; init; } = string.Empty;
}

public interface IAssistantSessionStore
{
    PendingAction? Get(Guid userId, string sessionId);

    void Set(Guid userId, string sessionId, PendingAction action);

    void Clear(Guid userId, string sessionId);
}

public class InMemoryAssistantSessionStore : IAssistantSessionStore
{
    private readonly ConcurrentDictionary<string, PendingAction> _store = new();
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(15);

    public PendingAction? Get(Guid userId, string sessionId)
    {
        var key = Key(userId, sessionId);
        return _store.TryGetValue(key, out var action) ? action : null;
    }

    public void Set(Guid userId, string sessionId, PendingAction action)
    {
        _store[Key(userId, sessionId)] = action;
    }

    public void Clear(Guid userId, string sessionId)
    {
        _store.TryRemove(Key(userId, sessionId), out _);
    }

    private static string Key(Guid userId, string sessionId) => $"{userId}:{sessionId}";
}