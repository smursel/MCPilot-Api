namespace MCPlilot.Application.Models;

using MCPilot.Application.Models;
using MCPilot.Application.Abstractions;

public sealed class ChatService : IChatService
{
    private readonly IChatService _chatService;

    public ChatService(IChatService chatService)
    {
        _chatService = chatService;
    }

    public Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        return _chatService.AskAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<ChatEvent> StreamAsync(ChatRequest request, CancellationToken ct = default)
    {
        return _chatService.StreamAsync(request, ct);
    }
}