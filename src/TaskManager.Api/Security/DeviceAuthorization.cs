using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

namespace TaskManager.Api.Security;

public static class DeviceAuthorization
{
    public static async Task<Guid?> FindWorkspaceIdAsync(
        HttpContext context,
        IDbContextFactory<TaskManagerDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Cookies.TryGetValue(RequestProtection.DeviceCookieName, out var credential)
            || !RequestProtection.TryGetTokenBytes(credential, out var tokenBytes))
        {
            return null;
        }

        var verifier = RequestProtection.Digest(tokenBytes);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.DeviceCredentials
            .AsNoTracking()
            .Where(item => item.Verifier == verifier && item.ExpiresAt > DateTimeOffset.UtcNow)
            .Select(item => (Guid?)item.WorkspaceId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
