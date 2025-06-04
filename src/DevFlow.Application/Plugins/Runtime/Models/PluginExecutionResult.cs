using DevFlow.SharedKernel.Results;

namespace DevFlow.Application.Plugins.Runtime.Models;

/// <summary>
/// Represents the result of plugin execution, including output data, execution metrics, and any errors.
/// </summary>
public sealed record PluginExecutionResult
{
  /// <summary>
  /// Gets a value indicating whether the plugin execution was successful.
  /// </summary>
  public bool IsSuccess { get; init; }

  /// <summary>
  /// Gets the output data produced by the plugin execution.
  /// </summary>
  public object? OutputData { get; init; }

  /// <summary>
  /// Gets the error message if the execution failed.
  /// </summary>
  public string? ErrorMessage { get; init; }

  /// <summary>
  /// Gets the detailed error information if the execution failed.
  /// </summary>
  public Error? Error { get; init; }

  /// <summary>
  /// Gets the execution logs generated during plugin execution.
  /// </summary>
  public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();

  /// <summary>
  /// Gets the time when execution started.
  /// </summary>
  public DateTimeOffset StartedAt { get; init; }

  /// <summary>
  /// Gets the time when execution completed.
  /// </summary>
  public DateTimeOffset CompletedAt { get; init; }

  /// <summary>
  /// Gets the total execution duration.
  /// </summary>
  public TimeSpan ExecutionDuration => CompletedAt - StartedAt;

  /// <summary>
  /// Gets the peak memory usage during execution in bytes.
  /// </summary>
  public long PeakMemoryUsageBytes { get; init; }

  /// <summary>
  /// Gets the exit code from the plugin execution (relevant for subprocess execution).
  /// </summary>
  public int? ExitCode { get; init; }

  /// <summary>
  /// Gets additional metadata about the execution.
  /// </summary>
  public IReadOnlyDictionary<string, object> Metadata { get; init; } =
      new Dictionary<string, object>();

  /// <summary>
  /// Creates a successful plugin execution result.
  /// </summary>
  /// <param name="outputData">The output data produced by the plugin</param>
  /// <param name="startedAt">When execution started</param>
  /// <param name="completedAt">When execution completed</param>
  /// <param name="logs">Execution logs</param>
  /// <param name="peakMemoryUsageBytes">Peak memory usage</param>
  /// <param name="metadata">Additional metadata</param>
  /// <returns>A successful execution result</returns>
  public static PluginExecutionResult Success(
      object? outputData = null,
      DateTimeOffset? startedAt = null,
      DateTimeOffset? completedAt = null,
      IReadOnlyList<string>? logs = null,
      long peakMemoryUsageBytes = 0,
      IReadOnlyDictionary<string, object>? metadata = null)
  {
    var now = DateTimeOffset.UtcNow;
    return new PluginExecutionResult
    {
      IsSuccess = true,
      OutputData = outputData,
      StartedAt = startedAt ?? now,
      CompletedAt = completedAt ?? now,
      Logs = logs ?? Array.Empty<string>(),
      PeakMemoryUsageBytes = peakMemoryUsageBytes,
      Metadata = metadata ?? new Dictionary<string, object>()
    };
  }

  /// <summary>
  /// Creates a failed plugin execution result.
  /// </summary>
  /// <param name="error">The error that caused the failure</param>
  /// <param name="startedAt">When execution started</param>
  /// <param name="completedAt">When execution completed</param>
  /// <param name="logs">Execution logs</param>
  /// <param name="exitCode">Process exit code</param>
  /// <param name="peakMemoryUsageBytes">Peak memory usage</param>
  /// <param name="metadata">Additional metadata</param>
  /// <returns>A failed execution result</returns>
  public static PluginExecutionResult Failure(
      Error error,
      DateTimeOffset? startedAt = null,
      DateTimeOffset? completedAt = null,
      IReadOnlyList<string>? logs = null,
      int? exitCode = null,
      long peakMemoryUsageBytes = 0,
      IReadOnlyDictionary<string, object>? metadata = null)
  {
    var now = DateTimeOffset.UtcNow;
    return new PluginExecutionResult
    {
      IsSuccess = false,
      Error = error,
      ErrorMessage = error.Message,
      StartedAt = startedAt ?? now,
      CompletedAt = completedAt ?? now,
      Logs = logs ?? Array.Empty<string>(),
      ExitCode = exitCode,
      PeakMemoryUsageBytes = peakMemoryUsageBytes,
      Metadata = metadata ?? new Dictionary<string, object>()
    };
  }

  /// <summary>
  /// Creates a failed plugin execution result from an exception.
  /// </summary>
  /// <param name="exception">The exception that caused the failure</param>
  /// <param name="startedAt">When execution started</param>
  /// <param name="completedAt">When execution completed</param>
  /// <param name="logs">Execution logs</param>
  /// <param name="exitCode">Process exit code</param>
  /// <returns>A failed execution result</returns>
  public static PluginExecutionResult Failure(
      Exception exception,
      DateTimeOffset? startedAt = null,
      DateTimeOffset? completedAt = null,
      IReadOnlyList<string>? logs = null,
      int? exitCode = null)
  {
    var error = Error.Failure(
        "PluginExecution.UnhandledException",
        $"Plugin execution failed with unhandled exception: {exception.Message}");

    return Failure(error, startedAt, completedAt, logs, exitCode);
  }
}