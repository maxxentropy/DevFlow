// File: src/DevFlow.Domain/Workflows/Enums/WorkflowStepStatus.cs
namespace DevFlow.Domain.Workflows.Enums;

/// <summary>
/// Represents the possible statuses of a workflow step execution.
/// </summary>
public enum WorkflowStepStatus
{
    /// <summary>
    /// The step is waiting to be executed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The step is currently executing.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The step has completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The step has failed during execution.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The step was skipped during execution.
    /// </summary>
    Skipped = 4
}
