namespace MCPilot.Application.Models;

public sealed record LlmMessage(ChatRole Role, IReadOnlyList<LlmContent> Content)
{
    public static LlmMessage FromUser(string text) => new(ChatRole.User, [new LlmText(text)]);

    public static LlmMessage FromAssistant(IReadOnlyList<LlmContent> content) => new(ChatRole.Assistant, content);

    public static LlmMessage FromToolResults(IReadOnlyList<LlmToolResult> results) => new(ChatRole.User, [.. results]);
}
