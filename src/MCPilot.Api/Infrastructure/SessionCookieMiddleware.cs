using Microsoft.AspNetCore.Mvc;

namespace MCPilot.Api.Infrastructure;

public sealed class SessionCookieMiddleware(RequestDelegate next)
{
    public const string CookieName = "mcpilot_sid";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");

            context.Response.Cookies.Append(CookieName, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
            });
        }

        context.Items[CookieName] = sessionId;

        await next(context);
    }
}

public static class SessionExtensions
{
    public static string SessionId(this ControllerBase controller) =>
        controller.HttpContext.Items[SessionCookieMiddleware.CookieName] as string
        ?? throw new InvalidOperationException("Oturum kimligi bulunamadi. SessionCookieMiddleware kayitli mi?");
}
