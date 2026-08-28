using MCPilot.Application.Models;

namespace MCPilot.Application.Abstractions;

public interface IChatService
{
    Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatEvent> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
