using DevFlow.SharedKernel.Results;

namespace DevFlow.Application.Plugins.Runtime.Models;

/// <summary>
/// Represents the execution context provided to a plugin during execution.
/// Contains all necessary information and resources for plugin execution.
/// </summary>
public sealed record PluginExecutionContext
{
  /// <summary>
  /// Gets the input data to be processed by the plugin.
  /// </summary>
  public object? InputData { get; init; }

  /// <summary>
  /// Gets the working directory path for plugin execution.
  /// The plugin should perform all file operations within this directory or its subdirectories.
  /// </summary>
  public string WorkingDirectory { get; init; } = string.Empty;

  /// <summary>
  /// Gets additional configuration parameters specific to this execution.
  /// These override or supplement the plugin's default configuration.
  /// </summary>
  public IReadOnlyDictionary<string, object> ExecutionParameters { get; init; } =
      new Dictionary<string, object>();

  /// <summary>
  /// Gets the environment variables available to the plugin during execution.
  /// </summary>
  public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
      new Dictionary<string, string>();

  /// <summary>
  /// Gets the maximum execution time allowed for the plugin.
  /// </summary>
  public TimeSpan ExecutionTimeout { get; init; } = TimeSpan.FromMinutes(5);

  /// <summary>
  /// Gets the maximum memory usage allowed for the plugin in bytes.
  /// </summary>
  public long MaxMemoryBytes { get; init; } = 256 * 1024 * 1024; // 256 MB default

  /// <summary>
  /// Gets the correlation identifier for tracking this execution across logs and telemetry.
  /// </summary>
  public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

  /// <summary>
  /// Creates a new plugin execution context with the specified parameters.
  /// </summary>
  /// <param name="workingDirectory">The working directory for plugin execution</param>
  /// <param name="inputData">The input data for the plugin</param>
  /// <param name="executionParameters">Additional execution parameters</param>
  /// <param name="environmentVariables">Environment variables for the plugin</param>
  /// <param name="executionTimeout">Maximum execution time</param>
  /// <param name="maxMemoryBytes">Maximum memory usage in bytes</param>
  /// <param name="correlationId">Correlation identifier for tracking</param>
  /// <returns>A result containing the execution context or validation errors</returns>
  public static Result<PluginExecutionContext> Create(
      string workingDirectory,
      object? inputData = null,
      IReadOnlyDictionary<string, object>? executionParameters = null,
      IReadOnlyDictionary<string, string>? environmentVariables = null,
      TimeSpan? executionTimeout = null,
      long? maxMemoryBytes = null,
      string? correlationId = null)
  {
    if (string.IsNullOrWhiteSpace(workingDirectory))
      return Result<PluginExecutionContext>.Failure(Error.Validation(
          "PluginExecutionContext.WorkingDirectoryEmpty",
          "Working directory cannot be empty."));

    if (!Directory.Exists(workingDirectory))
      return Result<PluginExecutionContext>.Failure(Error.Validation(
          "PluginExecutionContext.WorkingDirectoryNotFound",
          $"Working directory '{workingDirectory}' does not exist."));

    var timeout = executionTimeout ?? TimeSpan.FromMinutes(5);
    if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(1))
      return Result<PluginExecutionContext>.Failure(Error.Validation(
          "PluginExecutionContext.InvalidTimeout",
          "Execution timeout must be between 1 second and 1 hour."));

    var memoryLimit = maxMemoryBytes ?? (256 * 1024 * 1024);
    if (memoryLimit <= 0 || memoryLimit > (8L * 1024 * 1024 * 1024))
      return Result<PluginExecutionContext>.Failure(Error.Validation(
          "PluginExecutionContext.InvalidMemoryLimit",
          "Memory limit must be between 1 byte and 8 GB."));

    var context = new PluginExecutionContext
    {
      WorkingDirectory = Path.GetFullPath(workingDirectory),
      InputData = inputData,
      ExecutionParameters = executionParameters ?? new Dictionary<string, object>(),
      EnvironmentVariables = environmentVariables ?? new Dictionary<string, string>(),
      ExecutionTimeout = timeout,
      MaxMemoryBytes = memoryLimit,
      CorrelationId = correlationId ?? Guid.NewGuid().ToString()
    };

    return Result<PluginExecutionContext>.Success(context);
  }
}