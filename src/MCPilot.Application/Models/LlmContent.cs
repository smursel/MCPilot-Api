using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Models;

public abstract record LlmContent
{
    public abstract string Type { get; }
}

public sealed record LlmText(string Text) : LlmContent
{
    public override string Type => "text";
}

public sealed record LlmThinking(string Thinking, string Signature) : LlmContent
{
    public override string Type => "thinking";
}

public sealed record LlmRedactedThinking(string Data) : LlmContent
{
    public override string Type => "redacted_thinking";
}

public sealed record LlmToolUse(string Id, string Name, JToken Input) : LlmContent
{
    public override string Type => "tool_use";
}

public sealed record LlmToolResult(string ToolUseId, string Content, bool IsError = false) : LlmContent
{
    public override string Type => "tool_result";
}
