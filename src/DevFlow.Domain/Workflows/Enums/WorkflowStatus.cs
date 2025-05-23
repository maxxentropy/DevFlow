namespace DevFlow.Domain.Workflows.Enums;

/// <summary>
/// Represents the possible statuses of a workflow execution.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// The workflow has been created but not yet started.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// The workflow is currently running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// The workflow has completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The workflow has failed during execution.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The workflow has been cancelled by the user.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// The workflow is paused and waiting for user intervention.
    /// </summary>
    Paused = 5,

    /// <summary>
    /// The workflow is waiting for dependencies or external resources.
    /// </summary>
    Waiting = 6
}