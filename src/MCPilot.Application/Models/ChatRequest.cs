namespace MCPilot.Application.Models;

public sealed record ChatRequest
{
    public required string Message { get; init; }
    public string? ConversationId { get; init; }
    public string? Context { get; init; }
    public string SessionId { get; init; } = string.Empty;
}
