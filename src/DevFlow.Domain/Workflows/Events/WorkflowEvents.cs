using DevFlow.Domain.Common;
using MediatR;

namespace DevFlow.Domain.Workflows.Events;

/// <summary>
/// Domain event raised when a new workflow is created.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="WorkflowName">The workflow name</param>
/// <param name="CreatedAt">The creation timestamp</param>
public sealed record WorkflowCreatedEvent(
    WorkflowId WorkflowId,
    string WorkflowName,
    DateTime CreatedAt) : INotification;

/// <summary>
/// Domain event raised when a workflow is started.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="StartedAt">The start timestamp</param>
public sealed record WorkflowStartedEvent(
    WorkflowId WorkflowId,
    DateTime StartedAt) : INotification;

/// <summary>
/// Domain event raised when a workflow is completed successfully.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="CompletedAt">The completion timestamp</param>
public sealed record WorkflowCompletedEvent(
    WorkflowId WorkflowId,
    DateTime CompletedAt) : INotification;

/// <summary>
/// Domain event raised when a workflow fails.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="ErrorMessage">The error message</param>
/// <param name="FailedAt">The failure timestamp</param>
public sealed record WorkflowFailedEvent(
    WorkflowId WorkflowId,
    string ErrorMessage,
    DateTime FailedAt) : INotification;

/// <summary>
/// Domain event raised when a workflow is paused.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="PausedAt">The pause timestamp</param>
public sealed record WorkflowPausedEvent(
    WorkflowId WorkflowId,
    DateTime PausedAt) : INotification;

/// <summary>
/// Domain event raised when a workflow is resumed.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="ResumedAt">The resume timestamp</param>
public sealed record WorkflowResumedEvent(
    WorkflowId WorkflowId,
    DateTime ResumedAt) : INotification;

/// <summary>
/// Domain event raised when a workflow is cancelled.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="CancelledAt">The cancellation timestamp</param>
public sealed record WorkflowCancelledEvent(
    WorkflowId WorkflowId,
    DateTime CancelledAt) : INotification;

/// <summary>
/// Domain event raised when a workflow is updated.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="WorkflowName">The updated workflow name</param>
/// <param name="Description">The updated description</param>
/// <param name="UpdatedAt">The update timestamp</param>
public sealed record WorkflowUpdatedEvent(
    WorkflowId WorkflowId,
    string WorkflowName,
    string Description,
    DateTime UpdatedAt) : INotification;

/// <summary>
/// Domain event raised when a step is added to a workflow.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="StepId">The step identifier</param>
/// <param name="StepName">The step name</param>
public sealed record WorkflowStepAddedEvent(
    WorkflowId WorkflowId,
    WorkflowStepId StepId,
    string StepName) : INotification;