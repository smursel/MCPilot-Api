namespace MCPilot.Infrastructure.Llm;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5";

    public int MaxTokens { get; set; } = 16_000;

    public string Effort { get; set; } = "high";

    public bool EnableThinking { get; set; } = true;
}
