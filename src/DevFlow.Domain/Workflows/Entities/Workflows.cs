using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.ValueObjects;
using DevFlow.Domain.Workflows.Enums;
using DevFlow.Domain.Workflows.Events;
using DevFlow.SharedKernel.Entities;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Domain.Workflows.Entities;

/// <summary>
/// Represents the workflow aggregate root.
/// Manages the execution lifecycle and coordination of workflow steps.
/// </summary>
public sealed class Workflow : AggregateRoot<WorkflowId>
{
  private readonly List<WorkflowStep> _steps = new();

  // Private constructor for persistence
  private Workflow()
  {
    Id = default!; // Will be set by EF Core
  }

  // Constructor for creating new workflows
  private Workflow(
      WorkflowId id,
      WorkflowName name,
      WorkflowDescription description,
      DateTime createdAt)
  {
    Id = id;
    Name = name;
    Description = description;
    Status = WorkflowStatus.Draft;
    CreatedAt = createdAt;
    UpdatedAt = createdAt;

    AddDomainEvent(new WorkflowCreatedEvent(Id, Name.Value, CreatedAt));
  }

  /// <summary>
  /// Gets the workflow name.
  /// </summary>
  public WorkflowName Name { get; private set; } = null!;

  /// <summary>
  /// Gets the workflow description.
  /// </summary>
  public WorkflowDescription Description { get; private set; } = null!;

  /// <summary>
  /// Gets the current workflow status.
  /// </summary>
  public WorkflowStatus Status { get; private set; }

  /// <summary>
  /// Gets the workflow creation timestamp.
  /// </summary>
  public DateTime CreatedAt { get; private set; }

  /// <summary>
  /// Gets the workflow last update timestamp.
  /// </summary>
  public DateTime UpdatedAt { get; private set; }

  /// <summary>
  /// Gets the workflow start timestamp (when execution began).
  /// </summary>
  public DateTime? StartedAt { get; private set; }

  /// <summary>
  /// Gets the workflow completion timestamp.
  /// </summary>
  public DateTime? CompletedAt { get; private set; }

  /// <summary>
  /// Gets the workflow execution error if failed.
  /// </summary>
  public string? ErrorMessage { get; private set; }

  /// <summary>
  /// Gets the read-only collection of workflow steps.
  /// </summary>
  public IReadOnlyList<WorkflowStep> Steps => _steps.AsReadOnly();

  /// <summary>
  /// Creates a new workflow with the specified details.
  /// </summary>
  public static Result<Workflow> Create(
      string name,
      string description,
      DateTime? createdAt = null)
  {
    var nameResult = WorkflowName.Create(name);
    if (nameResult.IsFailure)
      return Result<Workflow>.Failure(nameResult.Error);

    var descriptionResult = WorkflowDescription.Create(description);
    if (descriptionResult.IsFailure)
      return Result<Workflow>.Failure(descriptionResult.Error);

    var id = WorkflowId.New();
    var timestamp = createdAt ?? DateTime.UtcNow;

    var workflow = new Workflow(id, nameResult.Value, descriptionResult.Value, timestamp);

    return Result<Workflow>.Success(workflow);
  }

  /// <summary>
  /// Adds a new step to the workflow.
  /// </summary>
  public Result AddStep(string stepName, PluginId pluginId, Dictionary<string, object>? configuration = null, int? order = null)
  {
    if (Status != WorkflowStatus.Draft)
      return Result.Failure(Error.Validation(
          "Workflow.CannotModifyRunningWorkflow",
          "Cannot modify workflow that is not in draft status."));

    var stepOrder = order ?? _steps.Count;
    var stepId = WorkflowStepId.New();

    var step = WorkflowStep.Create(stepId, stepName, pluginId, stepOrder, configuration);
    if (step.IsFailure)
      return Result.Failure(step.Error);

    _steps.Add(step.Value);
    UpdatedAt = DateTime.UtcNow;

    AddDomainEvent(new WorkflowStepAddedEvent(Id, step.Value.Id, stepName));

    return Result.Success();
  }

  /// <summary>
  /// Starts the workflow execution.
  /// </summary>
  public Result Start()
  {
    if (Status != WorkflowStatus.Draft)
      return Result.Failure(Error.Validation(
          "Workflow.AlreadyStarted",
          "Workflow has already been started."));

    if (_steps.Count == 0)
      return Result.Failure(Error.Validation(
          "Workflow.NoSteps",
          "Cannot start workflow with no steps."));

    Status = WorkflowStatus.Running;
    StartedAt = DateTime.UtcNow;
    UpdatedAt = StartedAt.Value;

    AddDomainEvent(new WorkflowStartedEvent(Id, StartedAt.Value));

    return Result.Success();
  }

  /// <summary>
  /// Marks the workflow as completed successfully.
  /// </summary>
  public Result Complete()
  {
    if (Status != WorkflowStatus.Running)
      return Result.Failure(Error.Validation(
          "Workflow.NotRunning",
          "Cannot complete workflow that is not running."));

    Status = WorkflowStatus.Completed;
    CompletedAt = DateTime.UtcNow;
    UpdatedAt = CompletedAt.Value;

    AddDomainEvent(new WorkflowCompletedEvent(Id, CompletedAt.Value));

    return Result.Success();
  }

  /// <summary>
  /// Marks the workflow as failed with an error message.
  /// </summary>
  public Result Fail(string errorMessage)
  {
    if (Status != WorkflowStatus.Running)
      return Result.Failure(Error.Validation(
          "Workflow.NotRunning",
          "Cannot fail workflow that is not running."));

    if (string.IsNullOrWhiteSpace(errorMessage))
      return Result.Failure(Error.Validation(
          "Workflow.ErrorMessageRequired",
          "Error message is required when marking workflow as failed."));

    Status = WorkflowStatus.Failed;
    ErrorMessage = errorMessage.Trim();
    CompletedAt = DateTime.UtcNow;
    UpdatedAt = CompletedAt.Value;

    AddDomainEvent(new WorkflowFailedEvent(Id, ErrorMessage, CompletedAt.Value));

    return Result.Success();
  }

  /// <summary>
  /// Pauses the workflow execution.
  /// </summary>
  public Result Pause()
  {
    if (Status != WorkflowStatus.Running)
      return Result.Failure(Error.Validation(
          "Workflow.NotRunning",
          "Cannot pause workflow that is not running."));

    Status = WorkflowStatus.Paused;
    UpdatedAt = DateTime.UtcNow;

    AddDomainEvent(new WorkflowPausedEvent(Id, UpdatedAt));

    return Result.Success();
  }

  /// <summary>
  /// Resumes the paused workflow execution.
  /// </summary>
  public Result Resume()
  {
    if (Status != WorkflowStatus.Paused)
      return Result.Failure(Error.Validation(
          "Workflow.NotPaused",
          "Cannot resume workflow that is not paused."));

    Status = WorkflowStatus.Running;
    UpdatedAt = DateTime.UtcNow;

    AddDomainEvent(new WorkflowResumedEvent(Id, UpdatedAt));

    return Result.Success();
  }

  /// <summary>
  /// Cancels the workflow execution.
  /// </summary>
  public Result Cancel()
  {
    if (Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
      return Result.Failure(Error.Validation(
          "Workflow.AlreadyFinished",
          "Cannot cancel workflow that has already finished."));

    Status = WorkflowStatus.Cancelled;
    CompletedAt = DateTime.UtcNow;
    UpdatedAt = CompletedAt.Value;

    AddDomainEvent(new WorkflowCancelledEvent(Id, CompletedAt.Value));

    return Result.Success();
  }

  /// <summary>
  /// Updates the workflow name and description.
  /// </summary>
  public Result UpdateDetails(string name, string description)
  {
    if (Status != WorkflowStatus.Draft)
      return Result.Failure(Error.Validation(
          "Workflow.CannotModifyRunningWorkflow",
          "Cannot modify workflow that is not in draft status."));

    var nameResult = WorkflowName.Create(name);
    if (nameResult.IsFailure)
      return Result.Failure(nameResult.Error);

    var descriptionResult = WorkflowDescription.Create(description);
    if (descriptionResult.IsFailure)
      return Result.Failure(descriptionResult.Error);

    var oldName = Name.Value;
    Name = nameResult.Value;
    Description = descriptionResult.Value;
    UpdatedAt = DateTime.UtcNow;

    if (oldName != Name.Value)
    {
      AddDomainEvent(new WorkflowUpdatedEvent(Id, Name.Value, Description.Value, UpdatedAt));
    }

    return Result.Success();
  }
}