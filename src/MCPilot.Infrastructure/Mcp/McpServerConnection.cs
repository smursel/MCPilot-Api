using System.Text;
using System.Text.Json;
using MCPilot.Application.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NewtonsoftFormatting = Newtonsoft.Json.Formatting;
using Newtonsoft.Json.Linq;

namespace MCPilot.Infrastructure.Mcp;

internal sealed class McpServerConnection(McpServerOptions options, McpClient client, ILogger logger) : IAsyncDisposable
{
    public static async Task<McpServerConnection> ConnectAsync(
        McpServerOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        IClientTransport transport = options.Transport switch
        {
            McpTransportKind.Stdio => new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = options.Name,
                Command = options.Command ?? throw new InvalidOperationException(
                    $"'{options.Name}' stdio MCP sunucusu icin 'Command' zorunlu."),
                Arguments = options.Arguments,
                WorkingDirectory = options.WorkingDirectory,
                EnvironmentVariables = options.Environment,
            }),
            McpTransportKind.Http => new HttpClientTransport(new HttpClientTransportOptions
            {
                Name = options.Name,
                Endpoint = new Uri(options.Endpoint ?? throw new InvalidOperationException(
                    $"'{options.Name}' http MCP sunucusu icin 'Endpoint' zorunlu.")),
                AdditionalHeaders = options.Headers,
            }),
            _ => throw new NotSupportedException($"Bilinmeyen MCP transport: {options.Transport}"),
        };

        logger.LogInformation("MCP sunucusuna baglaniliyor: {Server} ({Transport})", options.Name, options.Transport);
        var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        return new McpServerConnection(options, client, logger);
    }

    public async Task<IReadOnlyList<ToolDescriptor>> ListToolsAsync(string separator, CancellationToken cancellationToken)
    {
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

        return
        [
            .. tools.Select(tool => new ToolDescriptor(
                Name: $"{options.Name}{separator}{tool.Name}",
                ServerName: options.Name,
                OriginalName: tool.Name,
                Description: tool.Description ?? tool.Name,
                InputSchema: JObject.Parse(tool.JsonSchema.GetRawText()),
                ReadOnly: tool.ProtocolTool.Annotations?.ReadOnlyHint ?? false)),
        ];
    }

    public async Task<ToolExecutionResult> CallToolAsync(
        string toolName,
        JToken arguments,
        CancellationToken cancellationToken)
    {
        var argumentMap = new Dictionary<string, object?>();
        if (arguments is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                argumentMap[property.Name] =
                    JsonSerializer.Deserialize<JsonElement>(property.Value.ToString(NewtonsoftFormatting.None));
            }
        }

        var result = await client.CallToolAsync(toolName, argumentMap, cancellationToken: cancellationToken);

        var text = new StringBuilder();
        foreach (var block in result.Content)
        {
            switch (block)
            {
                case TextContentBlock textBlock:
                    text.AppendLine(textBlock.Text);
                    break;
                case ImageContentBlock image:
                    text.AppendLine($"[gorsel icerik: {image.MimeType}]");
                    break;
                default:
                    text.AppendLine($"[{block.Type} icerigi metne cevrilemedi]");
                    break;
            }
        }

        JToken? structured = result.StructuredContent is { } element
            ? JToken.Parse(element.GetRawText())
            : null;

        var isError = result.IsError ?? false;
        if (isError)
        {
            logger.LogWarning("MCP araci hata dondurdu: {Server}/{Tool}", options.Name, toolName);
        }

        var content = text.ToString().TrimEnd();
        if (content.Length == 0)
        {
            content = structured?.ToString(NewtonsoftFormatting.None) ?? "(bos sonuc)";
        }

        return new ToolExecutionResult(isError, content, structured);
    }

    public async ValueTask DisposeAsync() => await client.DisposeAsync();
}
