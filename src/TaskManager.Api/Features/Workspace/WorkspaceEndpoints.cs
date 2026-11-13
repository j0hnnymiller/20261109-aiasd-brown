using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using TaskManager.Api.Data;
using TaskManager.Api.Security;
using WorkspaceEntity = TaskManager.Api.Data.Workspace;

namespace TaskManager.Api.Features.Workspace;

public static class WorkspaceEndpoints
{
    private static readonly string[] SupportedTimeZones = TimeZoneInfo.GetSystemTimeZones()
        .Select(ToIanaTimeZoneId)
        .Where(value => value is not null)
        .Select(value => value!)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/time-zones", () => Results.Ok(SupportedTimeZones));
        api.MapGet("/csrf", GetCsrfToken);
        api.MapPost("/workspaces", CreateWorkspaceAsync);
        api.MapGet("/workspace", GetWorkspaceAsync);
    }

    private static IResult GetCsrfToken(HttpContext context)
    {
        var token = ApiSecurity.EncodeToken(RandomNumberGenerator.GetBytes(ApiSecurity.CredentialLength));
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Cookies.Append(
            ApiSecurity.CsrfCookieName,
            token,
            new CookieOptions
            {
                Secure = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true
            });
        return Results.Ok(new CsrfTokenResponse(token));
    }

    private static async Task<IResult> GetWorkspaceAsync(
        HttpContext context,
        TaskManagerDbContext database,
        CancellationToken cancellationToken)
    {
        var access = await ApiSecurity.ResolveWorkspaceAsync(context, database, cancellationToken);
        return access.Workspace is null
            ? ApiSecurity.Unauthorized(access.Failure!)
            : Results.Ok(new WorkspaceResponse(access.Workspace.Id, access.Workspace.TimeZone));
    }

    private static async Task<IResult> CreateWorkspaceAsync(
        CreateWorkspaceRequest request,
        HttpContext context,
        TaskManagerDbContext database,
        CancellationToken cancellationToken)
    {
        if (!ApiSecurity.HasValidOriginAndCsrf(context, context.RequestServices.GetRequiredService<IConfiguration>()))
        {
            return ApiSecurity.Forbidden();
        }

        if (!ApiSecurity.TryGetIdempotencyKey(context, out var keyDigest))
        {
            return ValidationError("idempotencyKey", "A valid operation key is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone) ||
            !SupportedTimeZones.Contains(request.TimeZone, StringComparer.Ordinal))
        {
            return ValidationError("timeZone", "Select a supported time zone before creating a workspace.");
        }

        if (!ApiSecurity.TryDecodeToken(request.DeviceToken, out var rawDeviceToken) ||
            !ApiSecurity.TryDecodeToken(request.RecoverySecret, out var rawRecoverySecret))
        {
            return ValidationError("credentials", "Secure workspace credentials could not be created. Try again.");
        }

        var deviceVerifier = ApiSecurity.Hash(rawDeviceToken);
        var recoveryVerifier = ApiSecurity.Hash(rawRecoverySecret);
        var fingerprint = ApiSecurity.Fingerprint(
            request.TimeZone,
            Convert.ToBase64String(deviceVerifier),
            Convert.ToBase64String(recoveryVerifier));

        var existing = await database.WorkspaceCreateOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.KeyDigest == keyDigest, cancellationToken);
        if (existing is not null)
        {
            return await ReplayWorkspaceCreateAsync(
                existing, fingerprint, request.DeviceToken!, database, context, cancellationToken);
        }

        var workspaceId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(90);
        var now = DateTimeOffset.UtcNow;
        var workspace = new WorkspaceEntity
        {
            Id = workspaceId,
            TimeZone = request.TimeZone,
            CreatedAt = now
        };
        workspace.Lists.Add(new TaskList
        {
            Id = listId,
            WorkspaceId = workspaceId,
            Name = "Tasks"
        });
        database.Workspaces.Add(workspace);
        database.DeviceCredentials.Add(new DeviceCredential
        {
            Id = deviceId,
            WorkspaceId = workspaceId,
            Verifier = deviceVerifier,
            ExpiresAt = expiresAt
        });
        database.RecoveryCredentials.Add(new RecoveryCredential
        {
            WorkspaceId = workspaceId,
            Verifier = recoveryVerifier
        });
        database.WorkspaceCreateOperations.Add(new WorkspaceCreateOperation
        {
            KeyDigest = keyDigest,
            RequestFingerprint = fingerprint,
            WorkspaceId = workspaceId,
            DeviceId = deviceId,
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
            var concurrentOperation = await database.WorkspaceCreateOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.KeyDigest == keyDigest, cancellationToken);
            if (concurrentOperation is null)
            {
                throw;
            }

            database.ChangeTracker.Clear();
            return await ReplayWorkspaceCreateAsync(
                concurrentOperation, fingerprint, request.DeviceToken!, database, context, cancellationToken);
        }

        ApiSecurity.SetDeviceCookie(context, request.DeviceToken!, expiresAt);
        return Results.Created(
            "/api/workspace",
            new WorkspaceCreatedResponse(workspaceId, listId, request.TimeZone));
    }

    private static async Task<IResult> ReplayWorkspaceCreateAsync(
        WorkspaceCreateOperation operation,
        byte[] fingerprint,
        string deviceToken,
        TaskManagerDbContext database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!ApiSecurity.Matches(operation.RequestFingerprint, fingerprint))
        {
            return Results.Conflict(new SafeError("The operation key was already used with different details."));
        }

        var device = await database.DeviceCredentials.AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == operation.DeviceId, cancellationToken);
        var workspace = await database.Workspaces.AsNoTracking()
            .FirstOrDefaultAsync(value => value.Id == operation.WorkspaceId, cancellationToken);
        var list = await database.TaskLists.AsNoTracking()
            .FirstOrDefaultAsync(value => value.WorkspaceId == operation.WorkspaceId && value.Name == "Tasks", cancellationToken);
        if (device is null || workspace is null || list is null)
        {
            return Results.Conflict(new SafeError("The original workspace operation is no longer available."));
        }

        ApiSecurity.SetDeviceCookie(context, deviceToken, device.ExpiresAt);
        return Results.Created(
            "/api/workspace",
            new WorkspaceCreatedResponse(workspace.Id, list.Id, workspace.TimeZone));
    }

    private static IResult ValidationError(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string? ToIanaTimeZoneId(TimeZoneInfo timeZone)
    {
        if (timeZone.Id == "UTC" || timeZone.Id.Contains('/'))
        {
            return timeZone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId)
            ? ianaId
            : null;
    }
}

public sealed record CreateWorkspaceRequest(string? TimeZone, string? DeviceToken, string? RecoverySecret);
public sealed record CsrfTokenResponse(string Token);
public sealed record WorkspaceResponse(Guid Id, string TimeZone);
public sealed record WorkspaceCreatedResponse(Guid WorkspaceId, Guid DefaultListId, string TimeZone);
public sealed record SafeError(string Title);
