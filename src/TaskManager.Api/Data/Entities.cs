namespace TaskManager.Api.Data;

public sealed class Workspace
{
    public Guid Id { get; set; }
    public required string TimeZoneId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TaskList
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DeviceCredential
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Verifier { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RecoveryCredential
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Verifier { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class WorkspaceCreationOperation
{
    public required string IdempotencyKeyDigest { get; set; }
    public required string RequestFingerprint { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid DefaultListId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TaskItem
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ListId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TaskCreationOperation
{
    public Guid WorkspaceId { get; set; }
    public required string IdempotencyKeyDigest { get; set; }
    public required string RequestFingerprint { get; set; }
    public Guid TaskId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
