using System.Net.Http.Headers;
using System.Text;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Llm;
using MCPilot.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NewtonsoftFormatting = Newtonsoft.Json.Formatting;

namespace MCPilot.Infrastructure.Llm;

/// <summary>
/// DeepSeek (OpenAI uyumlu chat/completions) icin <see cref="ILlmClient"/> implementasyonu.
/// </summary>
public sealed class DeepSeekLlmClient(
    HttpClient httpClient,
    IOptions<DeepSeekOptions> options,
    LlmRuntimeState state,
    ILogger<DeepSeekLlmClient> logger) : ILlmClient
{
    private readonly DeepSeekOptions _options = options.Value;

    private string ActiveModel =>
        string.Equals(state.Current.Provider, LlmCatalog.DeepSeek, StringComparison.OrdinalIgnoreCase)
            ? state.Current.Model
            : _options.Model;

    public LlmModelInfo ModelInfo => new(
        LlmCatalog.DeepSeek,
        ActiveModel,
        SupportsTools: !ActiveModel.Contains("reasoner", StringComparison.OrdinalIgnoreCase),
        SupportsThinking: ActiveModel.Contains("reasoner", StringComparison.OrdinalIgnoreCase));

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var payload = new JObject
        {
            ["model"] = ActiveModel,
            ["max_tokens"] = _options.MaxTokens,
            ["temperature"] = _options.Temperature,
            ["stream"] = false,
            ["messages"] = BuildMessages(request),
        };

        if (request.Tools.Count > 0 && ModelInfo.SupportsTools)
        {
            payload["tools"] = new JArray(request.Tools.Select(ToToolDefinition));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = new StringContent(payload.ToString(NewtonsoftFormatting.None), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await httpClient.SendAsync(httpRequest, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeepSeek cagrisi basarisiz oldu (model={Model}).", ActiveModel);
            throw new LlmProviderException($"DeepSeek API cagrisi basarisiz: {ex.Message}", ex);
        }

        var body = await httpResponse.Content.ReadAsStringAsync(ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("DeepSeek {Status} dondu: {Body}", (int)httpResponse.StatusCode, body);
            throw new LlmProviderException($"DeepSeek API hatasi ({(int)httpResponse.StatusCode}): {body}");
        }

        var json = JObject.Parse(body);
        var message = json["choices"]?[0]?["message"] as JObject
                      ?? throw new LlmProviderException("DeepSeek yanitinda mesaj bulunamadi.");

        var content = new List<LlmContent>();

        if (message["content"]?.Type is JTokenType.String && !string.IsNullOrWhiteSpace(message["content"]!.ToString()))
        {
            content.Add(new LlmText(message["content"]!.ToString()));
        }

        if (message["tool_calls"] is JArray toolCalls)
        {
            foreach (var call in toolCalls.OfType<JObject>())
            {
                var function = call["function"] as JObject;
                var name = function?["name"]?.ToString();

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // DeepSeek argumanlari JSON *string* olarak dondurur.
                var rawArguments = function?["arguments"]?.ToString();
                var arguments = ParseArguments(rawArguments);

                content.Add(new LlmToolUse(call["id"]?.ToString() ?? Guid.NewGuid().ToString("N"), name, arguments));
            }
        }

        var usage = new UsageInfo(
            ActiveModel,
            json["usage"]?["prompt_tokens"]?.Value<int>() ?? 0,
            json["usage"]?["completion_tokens"]?.Value<int>() ?? 0);

        var finishReason = json["choices"]?[0]?["finish_reason"]?.ToString();

        logger.LogInformation(
            "DeepSeek yaniti: finish={Finish} in={In} out={Out}",
            finishReason,
            usage.InputTokens,
            usage.OutputTokens);

        return new LlmResponse
        {
            Content = content,
            Usage = usage,
            StopReason = finishReason,
        };
    }

    private JArray BuildMessages(LlmRequest request)
    {
        var messages = new JArray();

        if (!string.IsNullOrWhiteSpace(request.System))
        {
            messages.Add(new JObject { ["role"] = "system", ["content"] = request.System });
        }

        foreach (var message in request.Messages)
        {
            // Anthropic'te tum arac sonuclari tek user mesajinda gelir;
            // DeepSeek'te her biri ayri bir "tool" mesaji olmali.
            var toolResults = message.Content.OfType<LlmToolResult>().ToList();
            if (toolResults.Count > 0)
            {
                foreach (var result in toolResults)
                {
                    messages.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = result.ToolUseId,
                        ["content"] = result.Content,
                    });
                }

                continue;
            }

            var text = string.Join("\n\n", message.Content.OfType<LlmText>().Select(c => c.Text));
            var toolUses = message.Content.OfType<LlmToolUse>().ToList();

            if (message.Role == ChatRole.User)
            {
                messages.Add(new JObject { ["role"] = "user", ["content"] = text });
                continue;
            }

            var assistant = new JObject { ["role"] = "assistant", ["content"] = text };

            if (toolUses.Count > 0)
            {
                assistant["tool_calls"] = new JArray(toolUses.Select(toolUse => new JObject
                {
                    ["id"] = toolUse.Id,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = toolUse.Name,
                        ["arguments"] = toolUse.Input.ToString(NewtonsoftFormatting.None),
                    },
                }));
            }

            messages.Add(assistant);
        }

        return messages;
    }

    private static JObject ToToolDefinition(LlmToolDefinition definition) => new()
    {
        ["type"] = "function",
        ["function"] = new JObject
        {
            ["name"] = definition.Name,
            ["description"] = definition.Description,
            ["parameters"] = definition.InputSchema,
        },
    };

    private static JToken ParseArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new JObject();
        }

        try
        {
            return JToken.Parse(raw);
        }
        catch (Newtonsoft.Json.JsonReaderException)
        {
            return new JObject();
        }
    }
}
