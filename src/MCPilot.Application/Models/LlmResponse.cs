namespace MCPilot.Application.Models;

public sealed class LlmResponse
{
    public required IReadOnlyList<LlmContent> Content { get; init; }
    public required UsageInfo Usage { get; init; }
    public string? StopReason { get; init; }

    public string Text =>
        string.Join("\n\n", Content.OfType<LlmText>().Select(c => c.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

    public IReadOnlyList<LlmToolUse> ToolUses => [.. Content.OfType<LlmToolUse>()];
}
