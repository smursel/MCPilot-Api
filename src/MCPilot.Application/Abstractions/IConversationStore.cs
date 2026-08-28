using MCPilot.Application.Models;

namespace MCPilot.Application.Abstractions;

public interface IConversationStore
{
    Task<Conversation> GetOrCreateAsync(string? conversationId, string sessionId, CancellationToken ct = default);

    Task<Conversation?> GetAsync(string conversationId, string sessionId, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationSummary>> ListAsync(string sessionId, CancellationToken ct = default);

    Task SaveAsync(Conversation conversation, CancellationToken ct = default);

    Task<bool> DeleteAsync(string conversationId, string sessionId, CancellationToken ct = default);
}
