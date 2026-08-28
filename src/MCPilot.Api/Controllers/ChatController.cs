using MCPilot.Api.Infrastructure;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MCPilot.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public sealed class ChatController(IChatService chatService, ILogger<ChatController> logger) : ControllerBase
{
    private static readonly JsonSerializerSettings SseSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponse>> Ask(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Mesaj bos olamaz.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var response = await chatService.AskAsync(request with { SessionId = this.SessionId() }, cancellationToken);
        return Ok(response);
    }

    [HttpPost("stream")]
    [Produces("text/event-stream")]
    public async Task Stream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chatEvent in chatService.StreamAsync(request with { SessionId = this.SessionId() }, cancellationToken))
            {
                await WriteEventAsync(chatEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Istemci akisi kapatti: {ConversationId}", request.ConversationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Akis sirasinda hata olustu.");
            await WriteEventAsync(new ChatFailedEvent(ex.Message), CancellationToken.None);
        }
    }

    private async Task WriteEventAsync(ChatEvent chatEvent, CancellationToken cancellationToken)
    {
        var payload = JsonConvert.SerializeObject(chatEvent, SseSettings);
        await Response.WriteAsync($"event: {chatEvent.Type}\n", cancellationToken);
        await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
