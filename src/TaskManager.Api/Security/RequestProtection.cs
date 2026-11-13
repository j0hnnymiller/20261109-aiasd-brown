using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Api.Security;

public static class RequestProtection
{
    public const string DeviceCookieName = "__Host-device";
    public const string CsrfCookieName = "__Host-csrf";
    public const string CsrfHeaderName = "X-CSRF-Token";

    public static bool TryGetTokenBytes(string? value, out byte[] tokenBytes)
    {
        tokenBytes = [];
        if (string.IsNullOrWhiteSpace(value) || value.Length != 43)
        {
            return false;
        }

        try
        {
            var decoded = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "=");
            if (decoded.Length != 32 || ToBase64Url(decoded) != value)
            {
                return false;
            }

            tokenBytes = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Digest(byte[] value) => Convert.ToHexString(SHA256.HashData(value));

    public static string Fingerprint<T>(T value) => Digest(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value));

    public static string ToBase64Url(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public static async Task MiddlewareAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (IsMutation(context.Request.Method) && !HasValidMutationOriginAndCsrf(context))
        {
            await Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "The request could not be verified. Reload the page and try again.")
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static bool HasValidMutationOriginAndCsrf(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (!context.Request.IsHttps && !IsLocalhost(host))
        {
            return false;
        }

        var origin = context.Request.Headers.Origin.ToString();
        var expectedOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            || !Uri.TryCreate(expectedOrigin, UriKind.Absolute, out var expectedUri)
            || Uri.Compare(originUri, expectedUri, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        var cookie = context.Request.Cookies[CsrfCookieName];
        var header = context.Request.Headers[CsrfHeaderName].ToString();
        if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(cookie),
            Encoding.UTF8.GetBytes(header));
    }

    private static bool IsMutation(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method)
        || HttpMethods.IsDelete(method);

    private static bool IsLocalhost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address);
}
