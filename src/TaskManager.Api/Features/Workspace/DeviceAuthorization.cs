using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Infrastructure;

namespace TaskManager.Api.Features.Workspace;

public sealed class DeviceAuthorization(TaskManagerDbContext database)
{
    public async Task<Guid?> GetWorkspaceIdAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var token = context.Request.Cookies[RequestSecurity.DeviceCookieName];
        if (!CredentialDigests.TryHashToken(token, out var verifier))
        {
            return null;
        }

        var credential = await database.DeviceCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Verifier == verifier, cancellationToken);
        if (credential is null
            || credential.RevokedAtUtc is not null
            || credential.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return credential.WorkspaceId;
    }
}
