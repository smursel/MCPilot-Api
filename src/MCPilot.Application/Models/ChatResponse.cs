namespace MCPilot.Application.Models;

public sealed class ChatResponse
{
    public required string ConversationId { get; init; }
    public required string MessageId { get; init; }
    public required string Answer { get; init; }
    public IReadOnlyList<ToolCallTrace> ToolCalls { get; init; } = [];
    public required UsageInfo Usage { get; init; }
    public bool Truncated { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
