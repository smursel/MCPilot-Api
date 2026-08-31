using MCPilot.Application.Abstractions;
using MCPilot.Application.Llm;
using MCPilot.Application.Models;

namespace MCPilot.Infrastructure.Llm;

/// <summary>Secili saglayiciya yonlendiren <see cref="ILlmClient"/>.</summary>
public sealed class RoutingLlmClient(
    LlmRuntimeState state,
    AnthropicLlmClient anthropic,
    DeepSeekLlmClient deepSeek) : ILlmClient
{
    private ILlmClient Active =>
        string.Equals(state.Current.Provider, LlmCatalog.DeepSeek, StringComparison.OrdinalIgnoreCase)
            ? deepSeek
            : anthropic;

    public LlmModelInfo ModelInfo => Active.ModelInfo;

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
        Active.CompleteAsync(request, ct);
}
