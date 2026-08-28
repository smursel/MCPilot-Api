using System.Collections.Concurrent;
using MCPilot.Application.Abstractions;
using MCPilot.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace MCPilot.Infrastructure.Mcp;

public sealed class McpToolCatalog(
    IOptions<McpOptions> options,
    ILogger<McpToolCatalog> logger) : IToolCatalog, IAsyncDisposable
{
    private readonly McpOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, McpServerConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<ToolDescriptor>? _cachedTools;

    public async Task<IReadOnlyList<ToolDescriptor>> GetToolsAsync(CancellationToken ct = default)
    {
        if (_cachedTools is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedTools is { } existing)
            {
                return existing;
            }

            var descriptors = new List<ToolDescriptor>();

            foreach (var server in _options.Servers.Where(s => s.Enabled))
            {
                try
                {
                    var connection = await GetConnectionAsync(server, ct);
                    descriptors.AddRange(await connection.ListToolsAsync(_options.ToolNameSeparator, ct));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "MCP sunucusu '{Server}' hazirlanamadi.", server.Name);
                }
            }

            _cachedTools = descriptors;
            logger.LogInformation("MCP katalogu hazir: {Count} arac.", descriptors.Count);

            return descriptors;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ToolExecutionResult> InvokeAsync(string toolName, JToken arguments, CancellationToken ct = default)
    {
        var tools = await GetToolsAsync(ct);
        var descriptor = tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            return ToolExecutionResult.Error($"'{toolName}' adinda kayitli bir MCP araci yok.");
        }

        if (!_connections.TryGetValue(descriptor.ServerName, out var connection))
        {
            return ToolExecutionResult.Error($"'{descriptor.ServerName}' MCP sunucusuna baglanti yok.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ToolCallTimeoutSeconds));

        logger.LogInformation("MCP arac cagrisi: {Server}/{Tool}", descriptor.ServerName, descriptor.OriginalName);

        try
        {
            return await connection.CallToolAsync(descriptor.OriginalName, arguments, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ToolExecutionResult.Error(
                $"Arac {_options.ToolCallTimeoutSeconds} saniyede yanit vermedi. Sorguyu daraltmayi deneyin.");
        }
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _cachedTools = null;

            foreach (var connection in _connections.Values)
            {
                await connection.DisposeAsync();
            }

            _connections.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<McpServerConnection> GetConnectionAsync(McpServerOptions server, CancellationToken ct)
    {
        if (_connections.TryGetValue(server.Name, out var existing))
        {
            return existing;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

        var connection = await McpServerConnection.ConnectAsync(server, logger, timeout.Token);
        _connections[server.Name] = connection;

        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            await connection.DisposeAsync();
        }

        _connections.Clear();
        _gate.Dispose();
    }
}
