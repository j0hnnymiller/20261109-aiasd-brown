using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManager.Api.Data;
using TaskManager.Api.Security;

namespace TaskManager.Api.Features.Tasks;

public sealed record CreateTaskRequest(string? Title, string? Description, DateOnly? DueDate, string? Priority);
public sealed record TaskResponse(Guid Id, string Title, string? Description, DateOnly? DueDate, string? Priority, string ListName);

public static class TaskEndpoints
{
    private static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];

    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/tasks", CreateAsync);
        endpoints.MapGet("/api/tasks", ReadAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        IDbContextFactory<TaskManagerDbContext> contextFactory,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceId = await DeviceAuthorization.FindWorkspaceIdAsync(context, contextFactory, cancellationToken);
        if (workspaceId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ValidationError("title", "Enter a task title.");
        }

        if (request.Priority is not null && !AllowedPriorities.Contains(request.Priority, StringComparer.Ordinal))
        {
            return ValidationError("priority", "Choose Low, Medium, High, or no priority.");
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
        if (!RequestProtection.TryGetTokenBytes(idempotencyKey, out var keyBytes))
        {
            return ValidationError("idempotencyKey", "The operation identifier is invalid. Try again.");
        }

        var keyDigest = RequestProtection.Digest(keyBytes);
        var fingerprint = RequestProtection.Fingerprint(request);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await database.TaskCreationOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                operation => operation.WorkspaceId == workspaceId && operation.IdempotencyKeyDigest == keyDigest,
                cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(database, existing, fingerprint, cancellationToken);
        }

        var defaultList = await database.TaskLists
            .AsNoTracking()
            .FirstOrDefaultAsync(
                list => list.WorkspaceId == workspaceId && list.Name == "Tasks",
                cancellationToken);
        if (defaultList is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The task list is temporarily unavailable. Try again.");
        }

        var now = DateTimeOffset.UtcNow;
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId.Value,
            ListId = defaultList.Id,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Priority = request.Priority,
            CreatedAt = now
        };
        database.Tasks.Add(task);
        database.TaskCreationOperations.Add(new TaskCreationOperation
        {
            WorkspaceId = workspaceId.Value,
            IdempotencyKeyDigest = keyDigest,
            RequestFingerprint = fingerprint,
            TaskId = task.Id,
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
            var concurrentOperation = await lookup.TaskCreationOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    operation => operation.WorkspaceId == workspaceId && operation.IdempotencyKeyDigest == keyDigest,
                    cancellationToken);
            if (concurrentOperation is not null)
            {
                return await ReplayAsync(lookup, concurrentOperation, fingerprint, cancellationToken);
            }

            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The task could not be saved. Retry the same operation.");
        }

        return Results.Created("/api/tasks", new TaskResponse(
            task.Id,
            task.Title,
            task.Description,
            task.DueDate,
            task.Priority,
            defaultList.Name));
    }

    private static async Task<IResult> ReadAsync(
        HttpContext context,
        IDbContextFactory<TaskManagerDbContext> contextFactory,
        CancellationToken cancellationToken)
    {
        var workspaceId = await DeviceAuthorization.FindWorkspaceIdAsync(context, contextFactory, cancellationToken);
        if (workspaceId is null)
        {
            return Results.Unauthorized();
        }

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tasks = await database.Tasks
            .AsNoTracking()
            .Where(task => task.WorkspaceId == workspaceId.Value)
            .OrderByDescending(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .Join(
                database.TaskLists.AsNoTracking(),
                task => new { task.WorkspaceId, task.ListId },
                list => new { list.WorkspaceId, ListId = list.Id },
                (task, list) => new TaskResponse(
                    task.Id,
                    task.Title,
                    task.Description,
                    task.DueDate,
                    task.Priority,
                    list.Name))
            .ToArrayAsync(cancellationToken);

        return Results.Ok(tasks);
    }

    private static async Task<IResult> ReplayAsync(
        TaskManagerDbContext database,
        TaskCreationOperation operation,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(operation.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "This operation identifier was already used for a different request.");
        }

        var result = await database.Tasks
            .AsNoTracking()
            .Where(task => task.WorkspaceId == operation.WorkspaceId && task.Id == operation.TaskId)
            .Join(
                database.TaskLists.AsNoTracking(),
                task => new { task.WorkspaceId, task.ListId },
                list => new { list.WorkspaceId, ListId = list.Id },
                (task, list) => new TaskResponse(
                    task.Id,
                    task.Title,
                    task.Description,
                    task.DueDate,
                    task.Priority,
                    list.Name))
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The saved task is temporarily unavailable. Retry the same operation.")
            : Results.Ok(result);
    }

    private static IResult ValidationError(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
