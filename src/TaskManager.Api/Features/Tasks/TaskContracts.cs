namespace TaskManager.Api.Features.Tasks;

public sealed record TaskCreateRequest(
    string? Title,
    string? Description,
    DateOnly? DueDate,
    string? Priority);

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    DateOnly? DueDate,
    string? Priority,
    string ListName,
    DateTimeOffset CreatedAtUtc);
