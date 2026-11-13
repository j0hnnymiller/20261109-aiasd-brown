using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManager.Api.Data;
using TaskManager.Api.Security;
using WorkspaceEntity = TaskManager.Api.Data.Workspace;

namespace TaskManager.Api.Features.Workspace;

public sealed record CreateWorkspaceRequest(string? TimeZoneId, string? DeviceCredential, string? RecoverySecret);
public sealed record WorkspaceResponse(Guid WorkspaceId, string TimeZoneId, string DefaultListName);
public sealed record CsrfTokenResponse(string Token);

public static class WorkspaceEndpoints
{
    private static readonly TimeSpan DeviceLifetime = TimeSpan.FromDays(90);

    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/security/csrf", GetCsrfTokenAsync);
        endpoints.MapGet("/api/time-zones", () => Results.Ok(
            GetSupportedTimeZoneIds()));
        endpoints.MapPost("/api/workspaces", CreateAsync);
        endpoints.MapGet("/api/workspaces/current", GetCurrentAsync);
        return endpoints;
    }

    private static IResult GetCsrfTokenAsync(HttpContext context)
    {
        var token = RequestProtection.ToBase64Url(RandomNumberGenerator.GetBytes(32));
        context.Response.Cookies.Append(
            RequestProtection.CsrfCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });
        return Results.Ok(new CsrfTokenResponse(token));
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        IDbContextFactory<TaskManagerDbContext> contextFactory,
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TimeZoneId) || !IsSupportedTimeZone(request.TimeZoneId))
        {
            return ValidationError("timeZoneId", "Choose a supported time zone before creating a task space.");
        }

        if (!RequestProtection.TryGetTokenBytes(request.DeviceCredential, out var deviceToken)
            || !RequestProtection.TryGetTokenBytes(request.RecoverySecret, out var recoveryToken))
        {
            return ValidationError("credentials", "Secure task-space credentials could not be created. Try again.");
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
        if (!RequestProtection.TryGetTokenBytes(idempotencyKey, out var idempotencyToken))
        {
            return ValidationError("idempotencyKey", "The operation identifier is invalid. Try again.");
        }

        var idempotencyDigest = RequestProtection.Digest(idempotencyToken);
        var requestFingerprint = RequestProtection.Fingerprint(request);
        var deviceVerifier = RequestProtection.Digest(deviceToken);
        var recoveryVerifier = RequestProtection.Digest(recoveryToken);
        var now = DateTimeOffset.UtcNow;

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await database.WorkspaceCreationOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(operation => operation.IdempotencyKeyDigest == idempotencyDigest, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(context, database, existing, requestFingerprint, deviceVerifier, request.DeviceCredential!, cancellationToken);
        }

        var workspace = new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            TimeZoneId = request.TimeZoneId,
            CreatedAt = now
        };
        var defaultList = new TaskList
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "Tasks",
            CreatedAt = now
        };
        var expiresAt = now.Add(DeviceLifetime);
        database.Workspaces.Add(workspace);
        database.TaskLists.Add(defaultList);
        database.DeviceCredentials.Add(new DeviceCredential
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Verifier = deviceVerifier,
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
        database.RecoveryCredentials.Add(new RecoveryCredential
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Verifier = recoveryVerifier,
            CreatedAt = now
        });
        database.WorkspaceCreationOperations.Add(new WorkspaceCreationOperation
        {
            IdempotencyKeyDigest = idempotencyDigest,
            RequestFingerprint = requestFingerprint,
            WorkspaceId = workspace.Id,
            DefaultListId = defaultList.Id,
            CreatedAt = now
        });

        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await using var lookup = await contextFactory.CreateDbContextAsync(cancellationToken);
            var concurrentOperation = await lookup.WorkspaceCreationOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(operation => operation.IdempotencyKeyDigest == idempotencyDigest, cancellationToken);
            if (concurrentOperation is not null)
            {
                return await ReplayAsync(context, lookup, concurrentOperation, requestFingerprint, deviceVerifier, request.DeviceCredential!, cancellationToken);
            }

            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The task space could not be created. Start a new attempt.");
        }

        AppendDeviceCookie(context, request.DeviceCredential!, expiresAt);
        return Results.Created(
            "/api/workspaces/current",
            new WorkspaceResponse(workspace.Id, workspace.TimeZoneId, defaultList.Name));
    }

    private static async Task<IResult> ReplayAsync(
        HttpContext context,
        TaskManagerDbContext database,
        WorkspaceCreationOperation operation,
        string requestFingerprint,
        string deviceVerifier,
        string deviceCredential,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(operation.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "This operation identifier was already used for a different request.");
        }

        var credential = await database.DeviceCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.WorkspaceId == operation.WorkspaceId && item.Verifier == deviceVerifier,
                cancellationToken);
        var workspace = await database.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == operation.WorkspaceId, cancellationToken);
        var defaultList = await database.TaskLists
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == operation.DefaultListId && item.WorkspaceId == operation.WorkspaceId, cancellationToken);

        if (credential is null || workspace is null || defaultList is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The task-space creation outcome is temporarily unavailable. Retry the same operation.");
        }

        AppendDeviceCookie(context, deviceCredential, credential.ExpiresAt);
        return Results.Ok(new WorkspaceResponse(workspace.Id, workspace.TimeZoneId, defaultList.Name));
    }

    private static async Task<IResult> GetCurrentAsync(
        HttpContext context,
        IDbContextFactory<TaskManagerDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        var authorizedWorkspace = await DeviceAuthorization.FindWorkspaceIdAsync(context, contextFactory, cancellationToken);
        if (authorizedWorkspace is null)
        {
            return Results.Unauthorized();
        }

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await database.Workspaces
            .AsNoTracking()
            .Where(item => item.Id == authorizedWorkspace.Value)
            .Select(item => new { item.Id, item.TimeZoneId })
            .FirstOrDefaultAsync(cancellationToken);
        return workspace is null
            ? Results.Unauthorized()
            : Results.Ok(new WorkspaceResponse(workspace.Id, workspace.TimeZoneId, "Tasks"));
    }

    private static void AppendDeviceCookie(HttpContext context, string credential, DateTimeOffset expiresAt)
    {
        var remainingLifetime = expiresAt - DateTimeOffset.UtcNow;
        context.Response.Cookies.Append(
            RequestProtection.DeviceCookieName,
            credential,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiresAt,
                MaxAge = remainingLifetime > TimeSpan.Zero ? remainingLifetime : TimeSpan.Zero,
                IsEssential = true
            });
    }

    private static bool IsSupportedTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out _);
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string[] GetSupportedTimeZoneIds() => TimeZoneInfo.GetSystemTimeZones()
        .Select(zone => TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaId) ? ianaId : zone.Id)
        .Append("UTC")
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static IResult ValidationError(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
