namespace MCPilot.Infrastructure.Mcp;

public enum McpTransportKind
{
    Stdio,
    Http,
}

public sealed class McpServerOptions
{
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public McpTransportKind Transport { get; set; } = McpTransportKind.Stdio;

    public string? Command { get; set; }

    public List<string> Arguments { get; set; } = [];

    public string? WorkingDirectory { get; set; }

    public Dictionary<string, string?> Environment { get; set; } = [];

    public string? Endpoint { get; set; }

    public Dictionary<string, string> Headers { get; set; } = [];
}

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    public List<McpServerOptions> Servers { get; set; } = [];

    public string ToolNameSeparator { get; set; } = "__";

    public int ConnectTimeoutSeconds { get; set; } = 30;

    public int ToolCallTimeoutSeconds { get; set; } = 120;
}
