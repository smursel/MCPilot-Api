namespace MCPilot.Application.Models;

public sealed record LlmModelInfo(
    string Provider,
    string Model,
    bool SupportsTools,
    bool SupportsThinking);
