namespace MCPilot.Application.Models;

public sealed class ChatRequest
{
    public required string Message { get; init; }
    public string? ConversationId { get; init; }
    public string? UserId { get; init; }
    public string? Context { get; init; }
}
