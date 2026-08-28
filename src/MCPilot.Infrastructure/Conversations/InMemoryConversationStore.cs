using System.Collections.Concurrent;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;

namespace MCPilot.Infrastructure.Conversations;

public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new(StringComparer.Ordinal);

    public Task<Conversation> GetOrCreateAsync(string? conversationId, string sessionId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(conversationId) &&
            _conversations.TryGetValue(conversationId, out var existing) &&
            existing.SessionId == sessionId)
        {
            return Task.FromResult(existing);
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
        };

        _conversations[conversation.Id] = conversation;
        return Task.FromResult(conversation);
    }

    public Task<Conversation?> GetAsync(string conversationId, string sessionId, CancellationToken ct = default)
    {
        var conversation = _conversations.GetValueOrDefault(conversationId);
        return Task.FromResult(conversation?.SessionId == sessionId ? conversation : null);
    }

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(string sessionId, CancellationToken ct = default)
    {
        IReadOnlyList<ConversationSummary> summaries =
        [
            .. _conversations.Values
                .Where(c => c.SessionId == sessionId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new ConversationSummary(c.Id, c.Title, c.UpdatedAt, c.Messages.Count)),
        ];

        return Task.FromResult(summaries);
    }

    public Task SaveAsync(Conversation conversation, CancellationToken ct = default)
    {
        _conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string conversationId, string sessionId, CancellationToken ct = default)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation) && conversation.SessionId == sessionId)
        {
            return Task.FromResult(_conversations.TryRemove(conversationId, out _));
        }

        return Task.FromResult(false);
    }
}
