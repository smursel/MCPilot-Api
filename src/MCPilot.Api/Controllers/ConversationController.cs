using MCPilot.Api.Infrastructure;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace MCPilot.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Produces("application/json")]
public sealed class ConversationController(IConversationStore conversationStore) : ControllerBase
{
    public sealed record MessageView(string Role, string Text, IReadOnlyList<string> ToolCalls);

    public sealed record ConversationView(
        string Id,
        string? Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<MessageView> Messages);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSummary>>> GetAll(CancellationToken cancellationToken)
    {
        var conversations = await conversationStore.ListAsync(this.SessionId(), cancellationToken);
        return Ok(conversations);
    }

    [HttpGet("{conversationId}")]
    [ProducesResponseType(typeof(ConversationView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationView>> Get(string conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversationStore.GetAsync(conversationId, this.SessionId(), cancellationToken);

        if (conversation is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Konusma bulunamadi.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        var messages = conversation.Messages
            .Select(message => new MessageView(
                message.Role == ChatRole.User ? "user" : "assistant",
                string.Join("\n\n", message.Content.OfType<LlmText>().Select(c => c.Text)),
                [.. message.Content.OfType<LlmToolUse>().Select(c => c.Name)]))
            .Where(view => view.Text.Length > 0 || view.ToolCalls.Count > 0)
            .ToList();

        return Ok(new ConversationView(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            messages));
    }

    [HttpDelete("{conversationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string conversationId, CancellationToken cancellationToken)
    {
        var deleted = await conversationStore.DeleteAsync(conversationId, this.SessionId(), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
