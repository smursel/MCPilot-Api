namespace MCPilot.Application.Llm;

public static class LlmCatalog
{
    public const string Anthropic = "anthropic";
    public const string DeepSeek = "deepseek";

    public static readonly IReadOnlyDictionary<string, string[]> Models =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Anthropic] = ["claude-opus-5", "claude-sonnet-5", "claude-haiku-4-5-20251001"],
            [DeepSeek] = ["deepseek-chat", "deepseek-reasoner"],
        };

    public static bool IsKnown(string provider, string model) =>
        Models.TryGetValue(provider, out var models) &&
        models.Contains(model, StringComparer.OrdinalIgnoreCase);
}
