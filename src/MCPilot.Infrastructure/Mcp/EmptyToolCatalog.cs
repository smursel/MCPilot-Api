using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;
using Newtonsoft.Json.Linq;

namespace MCPilot.Infrastructure.Mcp;

public sealed class EmptyToolCatalog : IToolCatalog
{
    public Task<IReadOnlyList<ToolDescriptor>> GetToolsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ToolDescriptor>>([]);

    public Task<ToolExecutionResult> InvokeAsync(string toolName, JToken arguments, CancellationToken ct = default) =>
        Task.FromResult(ToolExecutionResult.Error("MCP arac katalogu henuz baglanmadi."));

    public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;
}
