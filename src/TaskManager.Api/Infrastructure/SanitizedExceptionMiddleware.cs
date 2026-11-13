using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Api.Infrastructure;

public sealed class SanitizedExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "about:blank"
            });
        }
    }
}

public sealed class SecurityOptions(IReadOnlyCollection<string> allowedOrigins)
{
    public IReadOnlyCollection<string> AllowedOrigins { get; } = allowedOrigins;
}

public static class RequestSecurity
{
    public const string DeviceCookieName = "__Host-device";
    public const string CsrfCookieName = "__Host-csrf";

    public static bool HasValidOriginAndCsrf(HttpContext context, SecurityOptions options)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin)
            || !options.AllowedOrigins.Contains(origin, StringComparer.Ordinal))
        {
            return false;
        }

        var cookieToken = context.Request.Cookies[CsrfCookieName];
        var headerToken = context.Request.Headers["X-CSRF-Token"].ToString();
        if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(headerToken))
        {
            return false;
        }

        var cookieBytes = System.Text.Encoding.UTF8.GetBytes(cookieToken);
        var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerToken);
        return cookieBytes.Length == headerBytes.Length
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
    }
}
