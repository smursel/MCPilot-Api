namespace MCPilot.Application.Abstractions;

using MCPilot.Application.Models;

public interface ILlmClient
{
    Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken);
    IAsyncEnumerable<ChatEvent> StreamAsync(ChatRequest request, CancellationToken ct = default);

}