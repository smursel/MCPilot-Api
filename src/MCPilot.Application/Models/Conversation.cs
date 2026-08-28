namespace MCPilot.Application.Models;

public sealed class Conversation
{
    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<LlmMessage> Messages { get; init; } = [];
}

public sealed record ConversationSummary(
    string Id,
    string? Title,
    DateTimeOffset UpdatedAt,
    int MessageCount);
