namespace TaskManager.Api.Data;

public sealed class Workspace
{
    public Guid Id { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<TaskList> Lists { get; set; } = [];
}

public sealed class TaskList
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Workspace Workspace { get; set; } = null!;
}

public sealed class DeviceCredential
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public byte[] Verifier { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}

public sealed class RecoveryCredential
{
    public Guid WorkspaceId { get; set; }
    public byte[] Verifier { get; set; } = [];
}

public sealed class WorkspaceCreateOperation
{
    public byte[] KeyDigest { get; set; } = [];
    public byte[] RequestFingerprint { get; set; } = [];
    public Guid WorkspaceId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public sealed class TaskItem
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ListId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
    public TaskPriority? Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TaskCreateOperation
{
    public Guid WorkspaceId { get; set; }
    public byte[] KeyDigest { get; set; } = [];
    public byte[] RequestFingerprint { get; set; } = [];
    public Guid TaskId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
