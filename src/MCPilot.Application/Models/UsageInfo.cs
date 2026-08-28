namespace MCPilot.Application.Models;

public sealed record UsageInfo(string Model, int InputTokens, int OutputTokens)
{
    public int TotalTokens => InputTokens + OutputTokens;

    public static readonly UsageInfo Empty = new(string.Empty, 0, 0);

    public static UsageInfo operator +(UsageInfo a, UsageInfo b) =>
        new(string.IsNullOrEmpty(b.Model) ? a.Model : b.Model, a.InputTokens + b.InputTokens, a.OutputTokens + b.OutputTokens);
}
