namespace MCPilot.Application.Abstractions;

using MCPilot.Application.Models;

using Newtonsoft.Json.Linq;

public interface IToolCatalog
{

    Task<IReadOnlyList<ToolDescriptor>> GetToolsAsync(CancellationToken ct = default);

    Task<ToolExecutionResult> InvokeAsync(string toolName, JToken arguments, CancellationToken ct = default);

    Task RefreshAsync(CancellationToken ct = default);
}