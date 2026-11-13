namespace TaskManager.Api.Features.Workspace;

public sealed record WorkspaceCreateRequest(
    string? TimeZoneId,
    string? DeviceCredential,
    string? RecoverySecret,
    string? IdempotencyKey);

public sealed record WorkspaceCreatedResponse(
    Guid WorkspaceId,
    string TimeZoneId,
    string DefaultListName);

public sealed record CsrfResponse(string Token);
