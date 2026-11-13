using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using TaskManager.Api.Data;
using TaskManager.Api.Security;

namespace TaskManager.Api.Features.Tasks;

public static class TaskEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/tasks", ReadTasksAsync);
        api.MapPost("/tasks", CreateTaskAsync);
    }

    private static async Task<IResult> ReadTasksAsync(
        HttpContext context,
        TaskManagerDbContext database,
        CancellationToken cancellationToken)
    {
        var access = await ApiSecurity.ResolveWorkspaceAsync(context, database, cancellationToken);
        if (access.Workspace is null)
        {
            return ApiSecurity.Unauthorized(access.Failure!);
        }

        var tasks = await database.Tasks.AsNoTracking()
            .Where(task => task.WorkspaceId == access.Workspace.Id)
            .OrderByDescending(task => task.CreatedAt)
            .ThenBy(task => task.Id)
            .Select(task => new TaskResponse(
                task.Id,
                task.Title,
                task.Description,
                task.DueDate,
                task.Priority,
                task.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(tasks);
    }

    private static async Task<IResult> CreateTaskAsync(
        CreateTaskRequest request,
        HttpContext context,
        TaskManagerDbContext database,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!ApiSecurity.HasValidOriginAndCsrf(context, configuration))
        {
            return ApiSecurity.Forbidden();
        }

        var access = await ApiSecurity.ResolveWorkspaceAsync(context, database, cancellationToken);
        if (access.Workspace is null)
        {
            return ApiSecurity.Unauthorized(access.Failure!);
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ValidationError("title", "Enter a title before saving the task.");
        }

        if (!ApiSecurity.TryGetIdempotencyKey(context, out var keyDigest))
        {
            return ValidationError("idempotencyKey", "A valid operation key is required.");
        }

        if (request.Priority is not null && !Enum.IsDefined(request.Priority.Value))
        {
            return ValidationError("priority", "Choose Low, Medium, High, or no priority.");
        }

        var fingerprint = ApiSecurity.Fingerprint(
            request.Title,
            request.Description,
            request.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.Priority?.ToString());

        var existing = await database.TaskCreateOperations.AsNoTracking()
            .FirstOrDefaultAsync(
                operation => operation.WorkspaceId == access.Workspace.Id &&
                    operation.KeyDigest == keyDigest,
                cancellationToken);
        if (existing is not null)
        {
            return await ReplayTaskCreateAsync(
                existing, fingerprint, access.Workspace.Id, database, cancellationToken);
        }

        var listId = await database.TaskLists.AsNoTracking()
            .Where(list => list.WorkspaceId == access.Workspace.Id && list.Name == "Tasks")
            .Select(list => (Guid?)list.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (listId is null)
        {
            throw new InvalidOperationException("The default task list is unavailable.");
        }

        var now = DateTimeOffset.UtcNow;
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            WorkspaceId = access.Workspace.Id,
            ListId = listId.Value,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Priority = request.Priority,
            CreatedAt = now
        };
        database.Tasks.Add(task);
        database.TaskCreateOperations.Add(new TaskCreateOperation
        {
            WorkspaceId = task.WorkspaceId,
            KeyDigest = keyDigest,
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
            var concurrentOperation = await database.TaskCreateOperations.AsNoTracking()
                .FirstOrDefaultAsync(
                    operation => operation.WorkspaceId == access.Workspace.Id &&
                        operation.KeyDigest == keyDigest,
                    cancellationToken);
            if (concurrentOperation is null)
            {
                throw;
            }

            database.ChangeTracker.Clear();
            return await ReplayTaskCreateAsync(
                concurrentOperation, fingerprint, access.Workspace.Id, database, cancellationToken);
        }

        return Results.Created($"/api/tasks/{task.Id}", ToResponse(task));
    }

    private static async Task<IResult> ReplayTaskCreateAsync(
        TaskCreateOperation operation,
        byte[] fingerprint,
        Guid workspaceId,
        TaskManagerDbContext database,
        CancellationToken cancellationToken)
    {
        if (!ApiSecurity.Matches(operation.RequestFingerprint, fingerprint))
        {
            return Results.Conflict(new SafeTaskError("The operation key was already used with different task details."));
        }

        var task = await database.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(
                value => value.WorkspaceId == workspaceId && value.Id == operation.TaskId,
                cancellationToken);
        if (task is null)
        {
            return Results.Conflict(new SafeTaskError("The original task operation is no longer available."));
        }

        return Results.Ok(ToResponse(task));
    }

    private static IResult ValidationError(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static TaskResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.DueDate, task.Priority, task.CreatedAt);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

public sealed record CreateTaskRequest(
    string? Title,
    string? Description,
    DateOnly? DueDate,
    TaskPriority? Priority);

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    DateOnly? DueDate,
    TaskPriority? Priority,
    DateTimeOffset CreatedAt);

public sealed record SafeTaskError(string Title);
