using MCPilot.Application.Abstractions;
using MCPilot.Application.Llm;
using Microsoft.AspNetCore.Mvc;

namespace MCPilot.Api.Controllers;

[ApiController]
[Route("api/model")]
[Produces("application/json")]
public sealed class ModelController(
    ILlmClient llmClient,
    LlmRuntimeState state,
    IConfiguration configuration,
    ILogger<ModelController> logger) : ControllerBase
{
    public sealed record ModelResponse(
        string Provider,
        string Model,
        bool SupportsTools,
        bool SupportsThinking,
        bool Configured,
        IReadOnlyDictionary<string, string[]> AvailableModels);

    public sealed record ChangeModelRequest(string Provider, string Model);

    /// <summary>Sohbette kullanilan yapay zeka saglayicisini ve modelini dondurur.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status200OK)]
    public ActionResult<ModelResponse> Get() => Ok(Describe());

    /// <summary>Aktif saglayici/modeli degistirir. Degisiklik surec genelindedir ve yeniden baslatmada sifirlanir.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ModelResponse> Set([FromBody] ChangeModelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.Model))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "provider ve model zorunlu.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (!LlmCatalog.IsKnown(request.Provider, request.Model))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bilinmeyen saglayici veya model.",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"Gecerli secenekler: {string.Join(", ", LlmCatalog.Models.SelectMany(kv => kv.Value.Select(m => $"{kv.Key}/{m}")))}",
            });
        }

        state.Set(request.Provider.ToLowerInvariant(), request.Model);
        logger.LogWarning("Aktif model degistirildi: {Provider}/{Model}", request.Provider, request.Model);

        return Ok(Describe());
    }

    private ModelResponse Describe()
    {
        var info = llmClient.ModelInfo;

        var configured = info.Provider switch
        {
            LlmCatalog.DeepSeek => !string.IsNullOrWhiteSpace(configuration["DeepSeek:ApiKey"]),
            _ => !string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"])
                 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
        };

        return new ModelResponse(
            info.Provider,
            info.Model,
            info.SupportsTools,
            info.SupportsThinking,
            configured,
            LlmCatalog.Models);
    }
}
