using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Features.Workspace;
using TaskManager.Api.Infrastructure;

namespace TaskManager.Api.Features.Tasks;

public static class TaskEndpoints
{
    private static readonly string[] Priorities = ["Low", "Medium", "High"];

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

        var tasks = await database.Tasks
            .AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId.Value)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new TaskResponse(
                item.Id,
                item.Title,
                item.Description,
                item.DueDate,
                item.Priority,
                item.TaskList.Name,
                item.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return Results.Ok(tasks);
    }

    public static async Task<IResult> Create(
        TaskCreateRequest request,
        HttpContext context,
        TaskManagerDbContext database,
        DeviceAuthorization authorization,
        SecurityOptions securityOptions,
        CancellationToken cancellationToken)
    {
        if (!RequestSecurity.HasValidOriginAndCsrf(context, securityOptions))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Request verification failed.");
        }

        var workspaceId = await authorization.GetWorkspaceIdAsync(context, cancellationToken);
        if (workspaceId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "A valid device is required.");
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Enter a task title."];
        }

        if (request.Priority is not null && !Priorities.Contains(request.Priority, StringComparer.Ordinal))
        {
            errors["priority"] = ["Select Low, Medium, High, or no priority."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (!CredentialDigests.TryHashToken(context.Request.Headers["Idempotency-Key"].ToString(), out var keyDigest))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A valid operation key is required.");
        }

        var fingerprint = CredentialDigests.Fingerprint(
            request.Title,
            request.Description,
            request.DueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            request.Priority);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = CredentialDigests.AdvisoryLockKey($"task:{workspaceId.Value}", keyDigest);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);

        var priorOperation = await database.TaskCreationOperations
            .SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId.Value
                && item.KeyDigest == keyDigest, cancellationToken);
        if (priorOperation is not null)
        {
            if (!string.Equals(priorOperation.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The operation key was already used for a different request.");
            }

            var priorTask = await database.Tasks
                .AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId.Value && item.Id == priorOperation.TaskId)
                .Select(item => new TaskResponse(
                    item.Id,
                    item.Title,
                    item.Description,
                    item.DueDate,
                    item.Priority,
                    item.TaskList.Name,
                    item.CreatedAtUtc))
                .SingleAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created("/api/tasks", priorTask);
        }

        var defaultList = await database.TaskLists
            .SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId.Value && item.Name == "Tasks", cancellationToken);
        if (defaultList is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The task space is unavailable.");
        }

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId.Value,
            ListId = defaultList.Id,
            Title = request.Title!,
            Description = request.Description,
            DueDate = request.DueDate,
            Priority = request.Priority,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        database.Tasks.Add(task);
        database.TaskCreationOperations.Add(new TaskCreationOperation
        {
            WorkspaceId = workspaceId.Value,
            KeyDigest = keyDigest,
            RequestFingerprint = fingerprint,
            TaskId = task.Id,
            Task = task
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created("/api/tasks", ToResponse(task, defaultList.Name));
    }

    private static TaskResponse ToResponse(TaskItem task, string listName) =>
        new(task.Id, task.Title, task.Description, task.DueDate, task.Priority, listName, task.CreatedAtUtc);
}
