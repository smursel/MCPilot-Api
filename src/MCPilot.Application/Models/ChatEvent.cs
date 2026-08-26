using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Models;

public abstract record ChatEvent
{
    public abstract string Type { get; }
}

public sealed record ChatStartedEvent(string ConversationId) : ChatEvent
{
    public override string Type => "started";
}

public sealed record AssistantTextEvent(string Text) : ChatEvent
{
    public override string Type => "assistant_text";
}

public sealed record ToolCallStartedEvent(string Id, string Tool, string Server, JToken Arguments) : ChatEvent
{
    public override string Type => "tool_call";
}

public sealed record ToolCallCompletedEvent(ToolCallTrace Trace) : ChatEvent
{
    public override string Type => "tool_result";
}

public sealed record ChatCompletedEvent(ChatResponse Response) : ChatEvent
{
    public override string Type => "completed";
}

public sealed record ChatFailedEvent(string Message) : ChatEvent
{
    public override string Type => "failed";
}
