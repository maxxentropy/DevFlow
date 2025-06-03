// File: src/DevFlow.Domain/Workflows/Entities/WorkflowStep.cs
using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.Enums;
using DevFlow.SharedKernel.Entities;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Domain.Workflows.Entities;

/// <summary>
/// Represents a single step within a workflow execution.
/// </summary>
public sealed class WorkflowStep : Entity<WorkflowStepId>
{
  // Private constructor for persistence
  private WorkflowStep()
  {
    Id = default!; // Will be set by EF Core
  }

  // Constructor for creating new workflow steps
  private WorkflowStep(
      WorkflowStepId id,
      string name,
      PluginId pluginId,
      int order,
      Dictionary<string, object>? configuration)
  {
    Id = id;
    Name = name;
    PluginId = pluginId;
    Order = order;
    Configuration = configuration ?? new Dictionary<string, object>();
    Status = WorkflowStepStatus.Pending;
    CreatedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Gets the step name.
  /// </summary>
  public string Name { get; private set; } = null!;

  /// <summary>
  /// Gets the plugin ID to execute for this step.
  /// </summary>
  public PluginId PluginId { get; private set; } = null!;

  /// <summary>
  /// Gets the execution order of this step within the workflow.
  /// </summary>
  public int Order { get; private set; }

  /// <summary>
  /// Gets the configuration parameters for this step.
  /// </summary>
  public Dictionary<string, object> Configuration { get; private set; } = null!;

  /// <summary>
  /// Gets the current status of this step.
  /// </summary>
  public WorkflowStepStatus Status { get; private set; }

  /// <summary>
  /// Gets the step creation timestamp.
  /// </summary>
  public DateTime CreatedAt { get; private set; }

  /// <summary>
  /// Gets the step start timestamp.
  /// </summary>
  public DateTime? StartedAt { get; private set; }

  /// <summary>
  /// Gets the step completion timestamp.
  /// </summary>
  public DateTime? CompletedAt { get; private set; }

  /// <summary>
  /// Gets the step execution error message if failed.
  /// </summary>
  public string? ErrorMessage { get; private set; }

  /// <summary>
  /// Gets the step execution output/result.
  /// </summary>
  public string? Output { get; private set; }

  /// <summary>
  /// Gets the execution duration in milliseconds.
  /// </summary>
  public long? ExecutionDurationMs =>
      StartedAt.HasValue && CompletedAt.HasValue
          ? (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds
          : null;

  /// <summary>
  /// Creates a new workflow step with the specified details.
  /// </summary>
  public static Result<WorkflowStep> Create(
      WorkflowStepId id,
      string name,
      PluginId pluginId,
      int order,
      Dictionary<string, object>? configuration = null)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Result<WorkflowStep>.Failure(Error.Validation(
          "WorkflowStep.NameEmpty", "Step name cannot be empty."));

    if (name.Length > 200)
      return Result<WorkflowStep>.Failure(Error.Validation(
          "WorkflowStep.NameTooLong", "Step name cannot exceed 200 characters."));

    if (order < 0)
      return Result<WorkflowStep>.Failure(Error.Validation(
          "WorkflowStep.InvalidOrder", "Step order must be non-negative."));

    var step = new WorkflowStep(id, name.Trim(), pluginId, order, configuration);
    return Result<WorkflowStep>.Success(step);
  }

  /// <summary>
  /// Marks the step as started.
  /// </summary>
  public Result Start()
  {
    if (Status != WorkflowStepStatus.Pending)
      return Result.Failure(Error.Validation(
          "WorkflowStep.AlreadyStarted", "Step has already been started."));

    Status = WorkflowStepStatus.Running;
    StartedAt = DateTime.UtcNow;

    return Result.Success();
  }

  /// <summary>
  /// Marks the step as completed successfully with optional output.
  /// </summary>
  public Result Complete(string? output = null)
  {
    if (Status != WorkflowStepStatus.Running)
      return Result.Failure(Error.Validation(
          "WorkflowStep.NotRunning", "Cannot complete step that is not running."));

    Status = WorkflowStepStatus.Completed;
    CompletedAt = DateTime.UtcNow;
    Output = output;

    return Result.Success();
  }

  /// <summary>
  /// Marks the step as failed with an error message.
  /// </summary>
  public Result Fail(string errorMessage)
  {
    if (Status != WorkflowStepStatus.Running)
      return Result.Failure(Error.Validation(
          "WorkflowStep.NotRunning", "Cannot fail step that is not running."));

    if (string.IsNullOrWhiteSpace(errorMessage))
      return Result.Failure(Error.Validation(
          "WorkflowStep.ErrorMessageRequired", "Error message is required."));

    Status = WorkflowStepStatus.Failed;
    CompletedAt = DateTime.UtcNow;
    ErrorMessage = errorMessage.Trim();

    return Result.Success();
  }

  /// <summary>
  /// Skips the step execution.
  /// </summary>
  public Result Skip(string? reason = null)
  {
    if (Status != WorkflowStepStatus.Pending)
      return Result.Failure(Error.Validation(
          "WorkflowStep.CannotSkipStartedStep", "Cannot skip step that has already started."));

    Status = WorkflowStepStatus.Skipped;
    CompletedAt = DateTime.UtcNow;
    Output = reason ?? "Step was skipped";

    return Result.Success();
  }

  /// <summary>
  /// Updates the step configuration.
  /// </summary>
  public Result UpdateConfiguration(Dictionary<string, object> configuration)
  {
    if (Status != WorkflowStepStatus.Pending)
      return Result.Failure(Error.Validation(
          "WorkflowStep.CannotModifyStartedStep", "Cannot modify step that has already started."));

    Configuration = configuration ?? new Dictionary<string, object>();
    return Result.Success();
  }
}