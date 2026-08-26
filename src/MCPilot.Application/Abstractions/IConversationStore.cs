namespace MCPilot.Application.Abstractions;


using MCPilot.Application.Models;

public interface IConversationStore
{
    Task<Conversation?> GetAsync(string conversationId, CancellationToken cancellationToken);
    Task SaveAsync(Conversation conversation, CancellationToken cancellationToken);

    Task<Conversation> GetOrCreateAsync(string? conversationId, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string conversationId, CancellationToken cancellationToken);
}