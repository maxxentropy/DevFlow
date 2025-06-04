using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Application.Plugins.Runtime;

/// <summary>
/// Defines the contract for plugin runtime execution across different programming languages.
/// Responsible for executing plugins in their appropriate runtime environments with proper isolation and security.
/// </summary>
public interface IPluginRuntimeManager
{
  /// <summary>
  /// Gets the programming languages supported by this runtime manager.
  /// </summary>
  IReadOnlyList<PluginLanguage> SupportedLanguages { get; }

  /// <summary>
  /// Gets the unique identifier for this runtime manager.
  /// </summary>
  string RuntimeId { get; }

  /// <summary>
  /// Executes a plugin with the provided context and returns the execution result.
  /// </summary>
  /// <param name="plugin">The plugin to execute</param>
  /// <param name="context">The execution context containing input data and configuration</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>The result of the plugin execution</returns>
  Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Validates that a plugin can be executed by this runtime manager.
  /// Checks for proper structure, dependencies, and compatibility.
  /// </summary>
  /// <param name="plugin">The plugin to validate</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>True if the plugin is valid and executable, false otherwise</returns>
  Task<Result<bool>> ValidatePluginAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Determines if this runtime manager can execute the specified plugin.
  /// </summary>
  /// <param name="plugin">The plugin to check compatibility for</param>
  /// <returns>True if this runtime can execute the plugin, false otherwise</returns>
  bool CanExecutePlugin(Plugin plugin);

  /// <summary>
  /// Initializes the runtime environment for plugin execution.
  /// This may include setting up dependencies, compilers, or other runtime requirements.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result indicating success or failure of initialization</returns>
  Task<Result> InitializeAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Performs cleanup of runtime resources.
  /// Should be called when the runtime manager is no longer needed.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A task representing the cleanup operation</returns>
  Task DisposeAsync(CancellationToken cancellationToken = default);
}