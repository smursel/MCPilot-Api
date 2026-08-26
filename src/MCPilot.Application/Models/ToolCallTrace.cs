using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Models;

public sealed class ToolCallTrace
{
    public required string Id { get; init; }
    public required string Tool { get; init; }
    public required string Server { get; init; }
    public required JToken Arguments { get; init; }
    public required bool Success { get; init; }
    public required string Result { get; init; }
    public JToken? StructuredResult { get; init; }
    public required double DurationMs { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
}
