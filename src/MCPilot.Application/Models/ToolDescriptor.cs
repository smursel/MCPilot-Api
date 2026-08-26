using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Models;

public sealed record ToolDescriptor(
    string Name,
    string ServerName,
    string OriginalName,
    string Description,
    JObject InputSchema,
    bool ReadOnly);
