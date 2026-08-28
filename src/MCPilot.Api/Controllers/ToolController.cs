using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace MCPilot.Api.Controllers;

[ApiController]
[Route("api/tools")]
[Produces("application/json")]
public sealed class ToolController(IToolCatalog toolCatalog) : ControllerBase
{
    public sealed record RefreshResponse(int ToolCount);

    /// <summary>Bagli MCP sunucularinda kesfedilen tum araclari listeler.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ToolDescriptor>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ToolDescriptor>>> GetAll(CancellationToken cancellationToken)
    {
        var tools = await toolCatalog.GetToolsAsync(cancellationToken);
        return Ok(tools);
    }

    /// <summary>MCP baglantilarini yeniler ve arac listesini yeniden kesfeder.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RefreshResponse>> Refresh(CancellationToken cancellationToken)
    {
        await toolCatalog.RefreshAsync(cancellationToken);
        var tools = await toolCatalog.GetToolsAsync(cancellationToken);
        return Ok(new RefreshResponse(tools.Count));
    }

    /// <summary>Bir MCP aracini LLM devrede olmadan dogrudan calistirir.</summary>
    [HttpPost("{toolName}/invoke")]
    [ProducesResponseType(typeof(ToolExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ToolExecutionResult>> Invoke(
        string toolName,
        [FromBody] JObject arguments,
        CancellationToken cancellationToken)
    {
        var tools = await toolCatalog.GetToolsAsync(cancellationToken);
        var tool = tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));

        if (tool is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = $"'{toolName}' araci bulunamadi.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        var result = await toolCatalog.InvokeAsync(tool.Name, arguments, cancellationToken);
        return Ok(result);
    }
}
