namespace MCPilot.Infrastructure.Llm;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>deepseek-chat arac cagirmayi destekler; deepseek-reasoner desteklemez.</summary>
    public string Model { get; set; } = "deepseek-chat";

    public int MaxTokens { get; set; } = 8_000;

    public double Temperature { get; set; } = 0.2;

    public int TimeoutSeconds { get; set; } = 180;
}
