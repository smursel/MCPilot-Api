using System.Diagnostics;
using System.Runtime.CompilerServices;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;
using MCPilot.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace MCPilot.Application.Chat;

public sealed class ChatService(
    ILlmClient llm,
    IToolCatalog toolCatalog,
    IConversationStore conversations,
    IOptions<ChatOptions> options,
    ILogger<ChatService> logger) : IChatService
{
    private readonly ChatOptions _options = options.Value;

    public async Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ChatResponse? response = null;

        await foreach (var chatEvent in StreamAsync(request, cancellationToken))
        {
            if (chatEvent is ChatCompletedEvent completed)
            {
                response = completed.Response;
            }
        }

        return response ?? throw new InvalidOperationException("Sohbet dongusu sonuc uretmeden bitti.");
    }

    public async IAsyncEnumerable<ChatEvent> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.GetOrCreateAsync(request.ConversationId, request.SessionId, cancellationToken);
        conversation.Title ??= BuildTitle(request.Message);

        yield return new ChatStartedEvent(conversation.Id);

        var userMessage = LlmMessage.FromUser(request.Message);
        conversation.Messages.Add(userMessage);

        var promptMessages = TrimHistory(conversation.Messages);

        var tools = await toolCatalog.GetToolsAsync(cancellationToken);
        var toolsByName = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var toolDefinitions = tools
            .Select(t => new LlmToolDefinition(t.Name, t.Description, t.InputSchema))
            .ToList();

        var traces = new List<ToolCallTrace>();
        var usage = UsageInfo.Empty;
        var answer = string.Empty;
        var truncated = true;

        for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            var llmResponse = await llm.CompleteAsync(
                new LlmRequest
                {
                    Messages = promptMessages,
                    System = BuildSystemPrompt(request.Context),
                    Tools = toolDefinitions,
                },
                cancellationToken);

            usage += llmResponse.Usage;

            var assistantMessage = LlmMessage.FromAssistant(llmResponse.Content);
            promptMessages.Add(assistantMessage);
            conversation.Messages.Add(assistantMessage);

            var toolUses = llmResponse.ToolUses;
            if (toolUses.Count == 0)
            {
                answer = llmResponse.Text;
                truncated = false;
                break;
            }

            if (!string.IsNullOrWhiteSpace(llmResponse.Text))
            {
                yield return new AssistantTextEvent(llmResponse.Text);
            }

            var results = new List<LlmToolResult>(toolUses.Count);

            foreach (var toolUse in toolUses)
            {
                var descriptor = toolsByName.GetValueOrDefault(toolUse.Name);
                var server = descriptor?.ServerName ?? "unknown";

                yield return new ToolCallStartedEvent(toolUse.Id, toolUse.Name, server, toolUse.Input);

                var stopwatch = Stopwatch.StartNew();
                ToolExecutionResult execution;

                if (traces.Count >= _options.MaxToolCallsPerRequest)
                {
                    execution = ToolExecutionResult.Error(
                        $"Arac cagri limiti ({_options.MaxToolCallsPerRequest}) doldu. Eldeki verilerle cevap ver.");
                }
                else if (descriptor is null)
                {
                    execution = ToolExecutionResult.Error(
                        $"'{toolUse.Name}' adinda bir arac yok. Kullanilabilir araclar: {string.Join(", ", toolsByName.Keys)}");
                }
                else
                {
                    execution = await InvokeToolAsync(descriptor, toolUse.Input, cancellationToken);
                }

                stopwatch.Stop();

                var content = Truncate(execution.Content, _options.MaxToolResultChars);

                var trace = new ToolCallTrace
                {
                    Id = toolUse.Id,
                    Tool = toolUse.Name,
                    Server = server,
                    Arguments = toolUse.Input,
                    Success = !execution.IsError,
                    Result = content,
                    StructuredResult = execution.StructuredContent,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                };

                traces.Add(trace);
                results.Add(new LlmToolResult(toolUse.Id, content, execution.IsError));

                yield return new ToolCallCompletedEvent(trace);
            }

            var resultMessage = LlmMessage.FromToolResults(results);
            promptMessages.Add(resultMessage);
            conversation.Messages.Add(resultMessage);
        }

        if (truncated && string.IsNullOrWhiteSpace(answer))
        {
            answer = "Islem adim limitine takildi; sonucu tamamlayamadim. Lutfen sorunuzu daraltarak tekrar deneyin.";
            logger.LogWarning("Sohbet {ConversationId} tool dongusu limitine takildi.", conversation.Id);
        }

        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await conversations.SaveAsync(conversation, cancellationToken);

        yield return new ChatCompletedEvent(new ChatResponse
        {
            ConversationId = conversation.Id,
            MessageId = Guid.NewGuid().ToString("N"),
            Answer = answer,
            ToolCalls = traces,
            Usage = usage,
            Truncated = truncated,
        });
    }

    private async Task<ToolExecutionResult> InvokeToolAsync(
        ToolDescriptor descriptor,
        JToken arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await toolCatalog.InvokeAsync(descriptor.Name, arguments, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Arac {Tool} calistirilirken hata olustu.", descriptor.Name);
            return ToolExecutionResult.Error($"Arac calistirilamadi: {ex.Message}");
        }
    }

    private string BuildSystemPrompt(string? context) =>
        string.IsNullOrWhiteSpace(context)
            ? _options.SystemPrompt
            : $"{_options.SystemPrompt}\n\n# Istek baglami\n{context}";

    private List<LlmMessage> TrimHistory(List<LlmMessage> messages)
    {
        if (messages.Count <= _options.MaxHistoryMessages)
        {
            return [.. messages];
        }

        var window = messages.Skip(messages.Count - _options.MaxHistoryMessages).ToList();

        while (window.Count > 0 &&
               (window[0].Role != ChatRole.User || window[0].Content.Any(c => c is LlmToolResult)))
        {
            window.RemoveAt(0);
        }

        return window.Count > 0 ? window : [messages[^1]];
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars
            ? value
            : value[..maxChars] + $"\n\n[... {value.Length - maxChars} karakter kirpildi. Daha dar bir sorgu kullanin.]";

    private static string BuildTitle(string message) =>
        message.Length <= 60 ? message : message[..60] + "...";
}
