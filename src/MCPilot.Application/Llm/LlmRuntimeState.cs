namespace MCPilot.Application.Llm;

public sealed record LlmSelection(string Provider, string Model);

/// <summary>
/// Calisma aninda secili saglayici/model. Surec genelinde paylasilir ve
/// uygulama yeniden baslatildiginda yapilandirmadaki degere doner.
/// </summary>
public sealed class LlmRuntimeState(string provider, string model)
{
    private LlmSelection _current = new(provider, model);

    public LlmSelection Current => Volatile.Read(ref _current);

    public void Set(string provider, string model) =>
        Volatile.Write(ref _current, new LlmSelection(provider, model));
}
