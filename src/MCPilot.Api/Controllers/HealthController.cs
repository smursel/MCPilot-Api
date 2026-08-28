using MCPilot.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace MCPilot.Api.Controllers;

[ApiController]
[Route("api/health")]
[Produces("application/json")]
public sealed class HealthController(IToolCatalog toolCatalog) : ControllerBase
{
    public sealed record HealthResponse(
        string Status,
        int ToolCount,
        IReadOnlyList<string> Servers,
        DateTimeOffset Timestamp);

    /// <summary>Servisin ve bagli MCP sunucularinin durumunu dondurur.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var tools = await toolCatalog.GetToolsAsync(cancellationToken);

        return Ok(new HealthResponse(
            tools.Count > 0 ? "healthy" : "degraded",
            tools.Count,
            [.. tools.Select(t => t.ServerName).Distinct()],
            DateTimeOffset.UtcNow));
    }
}
