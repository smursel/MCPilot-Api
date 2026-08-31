using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Llm;
using MCPilot.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewtonsoftFormatting = Newtonsoft.Json.Formatting;
using Newtonsoft.Json.Linq;

namespace MCPilot.Infrastructure.Llm;

public sealed class AnthropicLlmClient(
    AnthropicClient client,
    IOptions<AnthropicOptions> options,
    LlmRuntimeState state,
    ILogger<AnthropicLlmClient> logger) : ILlmClient
{
    private readonly AnthropicOptions _options = options.Value;

    private string ActiveModel =>
        string.Equals(state.Current.Provider, LlmCatalog.Anthropic, StringComparison.OrdinalIgnoreCase)
            ? state.Current.Model
            : _options.Model;

    public LlmModelInfo ModelInfo => new(LlmCatalog.Anthropic, ActiveModel, SupportsTools: true, SupportsThinking: _options.EnableThinking);

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        MessageCreateParamsSystem? system = null;
        if (!string.IsNullOrWhiteSpace(request.System))
        {
            system = new List<TextBlockParam>
            {
                new() { Text = request.System, CacheControl = new CacheControlEphemeral() },
            };
        }

        ThinkingConfigParam? thinking = null;
        if (_options.EnableThinking)
        {
            thinking = new ThinkingConfigAdaptive();
        }

        var parameters = new MessageCreateParams
        {
            Model = ActiveModel,
            MaxTokens = _options.MaxTokens,
            Messages = [.. request.Messages.Select(ToMessageParam)],
            OutputConfig = new OutputConfig { Effort = ParseEffort(_options.Effort) },
            System = system,
            Thinking = thinking,
            Tools = request.Tools.Count > 0 ? [.. request.Tools.Select(ToTool)] : null,
        };

        Message message;
        try
        {
            message = await client.Messages.Create(parameters, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Claude cagrisi basarisiz oldu (model={Model}).", ActiveModel);
            throw new LlmProviderException($"Claude API cagrisi basarisiz: {ex.Message}", ex);
        }

        var content = new List<LlmContent>(message.Content.Count);
        foreach (var block in message.Content)
        {
            if (block.TryPickText(out TextBlock? text))
            {
                content.Add(new LlmText(text.Text));
            }
            else if (block.TryPickThinking(out ThinkingBlock? thinkingBlock))
            {
                content.Add(new LlmThinking(thinkingBlock.Thinking, thinkingBlock.Signature));
            }
            else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
            {
                content.Add(new LlmRedactedThinking(redacted.Data));
            }
            else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
            {
                content.Add(new LlmToolUse(toolUse.ID, toolUse.Name, ToJToken(toolUse.Input)));
            }
        }

        var usage = new UsageInfo(
            ActiveModel,
            (int)message.Usage.InputTokens,
            (int)message.Usage.OutputTokens);

        logger.LogInformation(
            "Claude yaniti: stop={StopReason} in={In} out={Out}",
            message.StopReason,
            usage.InputTokens,
            usage.OutputTokens);

        return new LlmResponse
        {
            Content = content,
            Usage = usage,
            StopReason = message.StopReason?.ToString(),
        };
    }

    private static MessageParam ToMessageParam(LlmMessage message) => new()
    {
        Role = message.Role == ChatRole.User ? Role.User : Role.Assistant,
        Content = message.Content.Select(ToContentBlockParam).ToList(),
    };

    private static ContentBlockParam ToContentBlockParam(LlmContent content) => content switch
    {
        LlmText text => new TextBlockParam { Text = text.Text },
        LlmThinking thinking => new ThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature },
        LlmRedactedThinking redacted => new RedactedThinkingBlockParam { Data = redacted.Data },
        LlmToolUse toolUse => new ToolUseBlockParam
        {
            ID = toolUse.Id,
            Name = toolUse.Name,
            Input = ToJsonElementDictionary(toolUse.Input),
        },
        LlmToolResult result => new ToolResultBlockParam
        {
            ToolUseID = result.ToolUseId,
            Content = result.Content,
            IsError = result.IsError,
        },
        _ => throw new NotSupportedException($"Bilinmeyen icerik tipi: {content.GetType().Name}"),
    };

    private static Tool ToTool(LlmToolDefinition definition) => new()
    {
        Name = definition.Name,
        Description = definition.Description,
        InputSchema = ToInputSchema(definition.InputSchema),
    };

    private static InputSchema ToInputSchema(JObject schema)
    {
        var properties = new Dictionary<string, JsonElement>();
        List<string> required = [];

        if (schema["properties"] is JObject props)
        {
            foreach (var property in props.Properties())
            {
                properties[property.Name] = ToJsonElement(property.Value);
            }
        }

        if (schema["required"] is JArray requiredArray)
        {
            required = [.. requiredArray.Select(t => t.ToString())];
        }

        return new InputSchema { Properties = properties, Required = required };
    }

    private static JsonElement ToJsonElement(JToken token) =>
        JsonSerializer.Deserialize<JsonElement>(token.ToString(NewtonsoftFormatting.None));

    private static JToken ToJToken(IReadOnlyDictionary<string, JsonElement> input)
    {
        var result = new JObject();
        foreach (var (key, value) in input)
        {
            result[key] = JToken.Parse(value.GetRawText());
        }

        return result;
    }

    private static Dictionary<string, JsonElement> ToJsonElementDictionary(JToken token) =>
        token is JObject obj
            ? obj.Properties().ToDictionary(p => p.Name, p => ToJsonElement(p.Value))
            : [];

    private static Effort ParseEffort(string effort) => effort.ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "max" => Effort.Max,
        _ => Effort.High,
    };
}
