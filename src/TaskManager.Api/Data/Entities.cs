namespace TaskManager.Api.Data;

public sealed class Workspace
{
    public Guid Id { get; set; }
    public required string TimeZoneId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<TaskList> TaskLists { get; set; } = [];
    public List<DeviceCredential> DeviceCredentials { get; set; } = [];
    public List<RecoveryCredential> RecoveryCredentials { get; set; } = [];
}

public sealed class TaskList
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public List<TaskItem> Tasks { get; set; } = [];
}

public sealed class DeviceCredential
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Verifier { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Workspace Workspace { get; set; } = null!;
}

public sealed class RecoveryCredential
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Verifier { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Workspace Workspace { get; set; } = null!;
}

public sealed class WorkspaceCreationOperation
{
    public required string KeyDigest { get; set; }
    public required string RequestFingerprint { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
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
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public TaskList TaskList { get; set; } = null!;
}

public sealed class TaskCreationOperation
{
    public Guid WorkspaceId { get; set; }
    public required string KeyDigest { get; set; }
    public required string RequestFingerprint { get; set; }
    public Guid TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;
}
