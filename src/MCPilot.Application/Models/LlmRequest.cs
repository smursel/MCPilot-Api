namespace MCPilot.Application.Models;

public sealed class LlmRequest
{
    public required IReadOnlyList<LlmMessage> Messages { get; init; }
    public string? System { get; init; }
    public IReadOnlyList<LlmToolDefinition> Tools { get; init; } = [];
}
