using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

namespace TaskManager.Api.Security;

public static class ApiSecurity
{
    public const string DeviceCookieName = "__Host-device";
    public const string CsrfCookieName = "__Host-csrf";
    public const int CredentialLength = 32;

    public static bool TryDecodeToken(string? token, out byte[] value)
    {
        value = [];
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            value = Convert.FromBase64String(base64);
            return value.Length == CredentialLength;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static byte[] Hash(byte[] value) => SHA256.HashData(value);

    public static byte[] HashToken(string token)
    {
        TryDecodeToken(token, out var value);
        return Hash(value);
    }

    public static bool Matches(byte[] left, byte[] right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);

    public static byte[] Fingerprint(params string?[] values)
    {
        var encoded = values.Select(value => value is null ? null : Encoding.UTF8.GetBytes(value)).ToArray();
        var length = encoded.Sum(value => sizeof(int) + (value?.Length ?? 0));
        var payload = new byte[length];
        var offset = 0;
        foreach (var value in encoded)
        {
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, sizeof(int)), value?.Length ?? -1);
            offset += sizeof(int);
            if (value is not null)
            {
                value.CopyTo(payload.AsSpan(offset));
                offset += value.Length;
            }
        }

        return SHA256.HashData(payload);
    }

    public static bool HasValidOriginAndCsrf(HttpContext context, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("TaskManager:AllowedOrigins").Get<string[]>() ?? [];
        var origin = context.Request.Headers.Origin.ToString();
        if (!allowedOrigins.Contains(origin, StringComparer.Ordinal) ||
            !context.Request.Cookies.TryGetValue(CsrfCookieName, out var cookieToken) ||
            !context.Request.Headers.TryGetValue("X-CSRF-Token", out var headerToken))
        {
            return false;
        }

        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
        var headerBytes = Encoding.UTF8.GetBytes(headerToken.ToString());
        return Matches(cookieBytes, headerBytes);
    }

    public static async Task<(Workspace? Workspace, string? Failure)> ResolveWorkspaceAsync(
        HttpContext context,
        TaskManagerDbContext database,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Cookies.TryGetValue(DeviceCookieName, out var token))
        {
            return (null, "missing_device");
        }

        if (!TryDecodeToken(token, out var rawToken))
        {
            return (null, "invalid_device");
        }

        var verifier = Hash(rawToken);
        var credential = await database.DeviceCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.Verifier == verifier, cancellationToken);
        if (credential is null || credential.Revoked || credential.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return (null, "invalid_device");
        }

        var workspace = await database.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == credential.WorkspaceId, cancellationToken);
        return workspace is null ? (null, "invalid_device") : (workspace, null);
    }

    public static IResult Unauthorized(string code) =>
        Results.Problem(
            title: "Workspace access could not be confirmed.",
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static IResult Forbidden() =>
        Results.Problem(
            title: "The request could not be verified.",
            statusCode: StatusCodes.Status403Forbidden);

    public static bool TryGetIdempotencyKey(HttpContext context, out byte[] digest)
    {
        digest = [];
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var key) ||
            !TryDecodeToken(key.ToString(), out var rawKey))
        {
            return false;
        }

        digest = Hash(rawKey);
        return true;
    }

    public static void SetDeviceCookie(HttpContext context, string token, DateTimeOffset expiresAt)
    {
        context.Response.Cookies.Append(
            DeviceCookieName,
            token,
            new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiresAt,
                IsEssential = true
            });
    }

    public static string EncodeToken(byte[] token) => Convert.ToBase64String(token)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
