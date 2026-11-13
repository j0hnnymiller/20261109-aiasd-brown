using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Infrastructure;

namespace TaskManager.Api.Features.Workspace;

public static class WorkspaceEndpoints
{
    private static readonly TimeSpan DeviceLifetime = TimeSpan.FromDays(90);

    public static async Task<IResult> Read(
        HttpContext context,
        TaskManagerDbContext database,
        DeviceAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var workspaceId = await authorization.GetWorkspaceIdAsync(context, cancellationToken);
        if (workspaceId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "A valid device is required.");
        }

        var workspace = await database.Workspaces
            .AsNoTracking()
            .Where(item => item.Id == workspaceId.Value)
            .Select(item => new WorkspaceInfoResponse(item.Id, item.TimeZoneId))
            .SingleOrDefaultAsync(cancellationToken);
        return workspace is null
            ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "A valid device is required.")
            : Results.Ok(workspace);
    }

    public static async Task<IResult> Create(
        WorkspaceCreateRequest request,
        HttpContext context,
        TaskManagerDbContext database,
        SecurityOptions securityOptions,
        CancellationToken cancellationToken)
    {
        if (!RequestSecurity.HasValidOriginAndCsrf(context, securityOptions))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Request verification failed.");
        }

        var errors = new Dictionary<string, string[]>();
        if (!TimeZoneEndpoints.IsSupported(request.TimeZoneId))
        {
            errors["timeZoneId"] = ["Select a supported time zone."];
        }

        if (!CredentialDigests.TryHashToken(request.DeviceCredential, out var deviceVerifier))
        {
            errors["deviceCredential"] = ["A valid device credential is required."];
        }

        if (!CredentialDigests.TryHashToken(request.RecoverySecret, out var recoveryVerifier))
        {
            errors["recoverySecret"] = ["A valid recovery secret is required."];
        }

        if (!CredentialDigests.TryHashToken(request.IdempotencyKey, out var operationKeyDigest))
        {
            errors["idempotencyKey"] = ["A valid operation key is required."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var fingerprint = CredentialDigests.Fingerprint(
            request.TimeZoneId,
            deviceVerifier,
            recoveryVerifier);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = CredentialDigests.AdvisoryLockKey("workspace", operationKeyDigest);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);

        var priorOperation = await database.WorkspaceCreationOperations
            .SingleOrDefaultAsync(item => item.KeyDigest == operationKeyDigest, cancellationToken);
        if (priorOperation is not null)
        {
            if (!string.Equals(priorOperation.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The operation key was already used for a different request.");
            }

            var originalWorkspace = await database.Workspaces
                .SingleAsync(item => item.Id == priorOperation.WorkspaceId, cancellationToken);
            var originalDevice = await database.DeviceCredentials
                .SingleAsync(item => item.WorkspaceId == originalWorkspace.Id
                    && item.Verifier == deviceVerifier, cancellationToken);
            SetDeviceCookie(context, request.DeviceCredential!, originalDevice.ExpiresAtUtc);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created(
                "/api/workspaces",
                new WorkspaceCreatedResponse(originalWorkspace.Id, originalWorkspace.TimeZoneId, "Tasks"));
        }

        var now = DateTimeOffset.UtcNow;
        var workspace = new TaskManager.Api.Data.Workspace
        {
            Id = Guid.NewGuid(),
            TimeZoneId = request.TimeZoneId!,
            CreatedAtUtc = now
        };
        var defaultList = new TaskList
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "Tasks",
            CreatedAtUtc = now
        };
        var device = new DeviceCredential
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Verifier = deviceVerifier,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.Add(DeviceLifetime)
        };
        var recovery = new RecoveryCredential
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Verifier = recoveryVerifier,
            IssuedAtUtc = now
        };
        var operation = new WorkspaceCreationOperation
        {
            KeyDigest = operationKeyDigest,
            RequestFingerprint = fingerprint,
            WorkspaceId = workspace.Id
        };

        database.AddRange(workspace, defaultList, device, recovery, operation);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        SetDeviceCookie(context, request.DeviceCredential!, device.ExpiresAtUtc);

        return Results.Created(
            "/api/workspaces",
            new WorkspaceCreatedResponse(workspace.Id, workspace.TimeZoneId, defaultList.Name));
    }

    private static void SetDeviceCookie(HttpContext context, string credential, DateTimeOffset expiresAtUtc)
    {
        context.Response.Cookies.Append(
            RequestSecurity.DeviceCookieName,
            credential,
            new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = expiresAtUtc,
                IsEssential = true
            });
    }
}
