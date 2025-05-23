using DevFlow.SharedKernel.Common;

namespace DevFlow.Domain.Common;

/// <summary>
/// Strongly-typed identifier for Workflow entities.
/// </summary>
public sealed class WorkflowId : StronglyTypedId<WorkflowId>
{
    public WorkflowId(string value) : base(value) { }

    /// <summary>
    /// Creates a new unique workflow identifier.
    /// </summary>
    public static WorkflowId New() => new(Guid.NewGuid().ToString("D"));

    /// <summary>
    /// Creates a workflow identifier from a string value.
    /// </summary>
    public static WorkflowId From(string value) => new(value);
}

/// <summary>
/// Strongly-typed identifier for Plugin entities.
/// </summary>
public sealed class PluginId : StronglyTypedId<PluginId>
{
    public PluginId(string value) : base(value) { }

    /// <summary>
    /// Creates a new unique plugin identifier.
    /// </summary>
    public static PluginId New() => new(Guid.NewGuid().ToString("D"));

    /// <summary>
    /// Creates a plugin identifier from a string value.
    /// </summary>
    public static PluginId From(string value) => new(value);
}

/// <summary>
/// Strongly-typed identifier for WorkflowStep entities.
/// </summary>
public sealed class WorkflowStepId : StronglyTypedId<WorkflowStepId>
{
    public WorkflowStepId(string value) : base(value) { }

    /// <summary>
    /// Creates a new unique workflow step identifier.
    /// </summary>
    public static WorkflowStepId New() => new(Guid.NewGuid().ToString("D"));

    /// <summary>
    /// Creates a workflow step identifier from a string value.
    /// </summary>
    public static WorkflowStepId From(string value) => new(value);
}