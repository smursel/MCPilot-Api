using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Models;

public sealed record ToolExecutionResult(bool IsError, string Content, JToken? StructuredContent = null)
{
    public static ToolExecutionResult Error(string message) => new(true, message);
}
