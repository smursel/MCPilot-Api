using MCPilot.Application.Models;

namespace MCPilot.Application.Abstractions;

public interface ILlmClient
{
    LlmModelInfo ModelInfo { get; }

    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
}
