using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Models;

public sealed record LlmToolDefinition(string Name, string Description, JObject InputSchema);
