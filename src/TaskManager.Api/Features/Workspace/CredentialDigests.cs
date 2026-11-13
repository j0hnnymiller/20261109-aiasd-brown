using System.Security.Cryptography;
using System.Text;

namespace TaskManager.Api.Features.Workspace;

public static class CredentialDigests
{
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static bool TryHashToken(string? token, out string digest)
    {
        digest = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(
                token.Replace('-', '+').Replace('_', '/')
                .PadRight(token.Length + (4 - token.Length % 4) % 4, '='));
            if (bytes.Length != 32)
            {
                return false;
            }

            digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Fingerprint(params string?[] values)
    {
        var canonical = string.Join(
            '\n',
            values.Select(value => value is null ? "-1:" : $"{value.Length}:{value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static long AdvisoryLockKey(string operation, string keyDigest)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{operation}:{keyDigest}"));
        return System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
