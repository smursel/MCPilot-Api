using Microsoft.AspNetCore.Mvc;

namespace MCPilot.Api.Infrastructure;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string? Value { get; set; }

    public string HeaderName { get; set; } = "X-Api-Key";

    public List<string> AnonymousPaths { get; set; } = ["/api/health", "/swagger"];
}

public sealed class ApiKeyMiddleware(RequestDelegate next, ApiKeyOptions options, ILogger<ApiKeyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(options.Value))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (options.AnonymousPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(options.HeaderName, out var provided) ||
            !CryptographicEquals(provided.ToString(), options.Value))
        {
            logger.LogWarning("Yetkisiz istek: {Path} ({Ip})", path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Gecersiz veya eksik API anahtari.",
                Status = StatusCodes.Status401Unauthorized,
                Detail = $"'{options.HeaderName}' basligi ile gecerli bir anahtar gonderin.",
            });

            return;
        }

        await next(context);
    }

    private static bool CryptographicEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a.PadRight(64)[..64]),
            System.Text.Encoding.UTF8.GetBytes(b.PadRight(64)[..64]));
}
