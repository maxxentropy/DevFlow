namespace DevFlow.Application.Plugins.Runtime.Models;

/// <summary>
/// Represents the execution capabilities and requirements of a plugin.
/// Provides information about what a plugin can do and what it needs to execute successfully.
/// </summary>
public sealed record PluginExecutionCapabilities
{
  /// <summary>
  /// Gets a value indicating whether the plugin can be executed.
  /// </summary>
  public bool CanExecute { get; init; }

  /// <summary>
  /// Gets the supported programming language.
  /// </summary>
  public string Language { get; init; } = string.Empty;

  /// <summary>
  /// Gets the runtime manager that will handle execution.
  /// </summary>
  public string RuntimeManagerId { get; init; } = string.Empty;

  /// <summary>
  /// Gets any limitations or restrictions for execution.
  /// </summary>
  public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

  /// <summary>
  /// Gets the required capabilities for execution.
  /// </summary>
  public IReadOnlyList<string> RequiredCapabilities { get; init; } = Array.Empty<string>();

  /// <summary>
  /// Gets the estimated memory requirements in bytes.
  /// </summary>
  public long EstimatedMemoryRequirementBytes { get; init; }

  /// <summary>
  /// Gets the estimated execution timeout.
  /// </summary>
  public TimeSpan EstimatedExecutionTimeout { get; init; } = TimeSpan.FromMinutes(5);

  /// <summary>
  /// Gets a value indicating whether the plugin supports cancellation.
  /// </summary>
  public bool SupportsCancellation { get; init; } = true;

  /// <summary>
  /// Creates plugin execution capabilities for a plugin that can be executed.
  /// </summary>
  /// <param name="language">The programming language</param>
  /// <param name="runtimeManagerId">The runtime manager identifier</param>
  /// <param name="requiredCapabilities">Required capabilities</param>
  /// <param name="limitations">Any execution limitations</param>
  /// <param name="estimatedMemoryRequirementBytes">Estimated memory requirement</param>
  /// <param name="estimatedExecutionTimeout">Estimated execution timeout</param>
  /// <param name="supportsCancellation">Whether the plugin supports cancellation</param>
  /// <returns>A capabilities object indicating the plugin can execute</returns>
  public static PluginExecutionCapabilities CreateExecutable(
      string language,
      string runtimeManagerId,
      IReadOnlyList<string>? requiredCapabilities = null,
      IReadOnlyList<string>? limitations = null,
      long estimatedMemoryRequirementBytes = 256 * 1024 * 1024,
      TimeSpan? estimatedExecutionTimeout = null,
      bool supportsCancellation = true)
  {
    return new PluginExecutionCapabilities
    {
      CanExecute = true,
      Language = language,
      RuntimeManagerId = runtimeManagerId,
      RequiredCapabilities = requiredCapabilities ?? Array.Empty<string>(),
      Limitations = limitations ?? Array.Empty<string>(),
      EstimatedMemoryRequirementBytes = estimatedMemoryRequirementBytes,
      EstimatedExecutionTimeout = estimatedExecutionTimeout ?? TimeSpan.FromMinutes(5),
      SupportsCancellation = supportsCancellation
    };
  }

  /// <summary>
  /// Creates plugin execution capabilities for a plugin that cannot be executed.
  /// </summary>
  /// <param name="language">The programming language</param>
  /// <param name="limitations">Reasons why the plugin cannot execute</param>
  /// <returns>A capabilities object indicating the plugin cannot execute</returns>
  public static PluginExecutionCapabilities CreateNotExecutable(
      string language,
      IReadOnlyList<string> limitations)
  {
    return new PluginExecutionCapabilities
    {
      CanExecute = false,
      Language = language,
      RuntimeManagerId = string.Empty,
      RequiredCapabilities = Array.Empty<string>(),
      Limitations = limitations,
      EstimatedMemoryRequirementBytes = 0,
      EstimatedExecutionTimeout = TimeSpan.Zero,
      SupportsCancellation = false
    };
  }
}