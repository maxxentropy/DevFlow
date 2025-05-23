using DevFlow.Domain.Workflows.Enums;

namespace DevFlow.Application.Workflows.DTOs;

/// <summary>
/// Data transfer object for workflow information.
/// </summary>
public sealed record WorkflowDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required WorkflowStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public required List<WorkflowStepDto> Steps { get; init; } = new();
}

/// <summary>
/// Data transfer object for workflow step information.
/// </summary>
public sealed record WorkflowStepDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string PluginId { get; init; }
    public required int Order { get; init; }
    public required Dictionary<string, object> Configuration { get; init; } = new();
    public required WorkflowStepStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Output { get; init; }
    public long? ExecutionDurationMs { get; init; }
}