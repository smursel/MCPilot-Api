using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace MCPilot.Api.Controllers;

[ApiController]
[Route("api/model")]
[Produces("application/json")]
public sealed class ModelController(ILlmClient llmClient, IConfiguration configuration) : ControllerBase
{
    public sealed record ModelResponse(
        string Provider,
        string Model,
        bool SupportsTools,
        bool SupportsThinking,
        bool Configured,
        IReadOnlyList<string> AvailableProviders);

    /// <summary>Sohbette kullanilan yapay zeka saglayicisini ve modelini dondurur.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status200OK)]
    public ActionResult<ModelResponse> Get()
    {
        var info = llmClient.ModelInfo;

        var configured = info.Provider switch
        {
            "deepseek" => !string.IsNullOrWhiteSpace(configuration["DeepSeek:ApiKey"]),
            _ => !string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"])
                 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
        };

        return Ok(new ModelResponse(
            info.Provider,
            info.Model,
            info.SupportsTools,
            info.SupportsThinking,
            configured,
            ["anthropic", "deepseek"]));
    }
}
