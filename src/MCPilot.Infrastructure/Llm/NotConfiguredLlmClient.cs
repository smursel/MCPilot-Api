using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;

namespace MCPilot.Infrastructure.Llm;

public sealed class NotConfiguredLlmClient : ILlmClient
{
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "LLM istemcisi yapilandirilmadi. AnthropicLlmClient yazilip DI'da NotConfiguredLlmClient yerine kaydedilmeli.");
}
