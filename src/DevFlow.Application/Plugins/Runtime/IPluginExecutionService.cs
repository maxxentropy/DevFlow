using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;
using DevFlow.SharedKernel.Results;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevFlow.Application.Plugins.Runtime;

/// <summary>
/// Service interface for high-level plugin execution operations.
/// Provides a simplified API for executing plugins with automatic context management.
/// </summary>
public interface IPluginExecutionService
{
  /// <summary>
  /// Executes a plugin with the specified input data and configuration.
  /// Handles plugin discovery, context creation, execution, and cleanup automatically.
  /// </summary>
  /// <param name="pluginId">The plugin identifier</param>
  /// <param name="inputData">The input data for execution</param>
  /// <param name="executionParameters">Additional execution parameters</param>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>The execution result</returns>
  Task<Result<PluginExecutionResult>> ExecutePluginAsync(
      PluginId pluginId,
      object? inputData = null,
      IReadOnlyDictionary<string, object>? executionParameters = null,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Validates that a plugin can be executed before attempting execution.
  /// </summary>
  /// <param name="pluginId">The plugin identifier</param>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>True if the plugin can be executed, false otherwise</returns>
  Task<Result<bool>> ValidatePluginExecutionAsync(
      PluginId pluginId,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets execution capabilities for a specific plugin.
  /// </summary>
  /// <param name="pluginId">The plugin identifier</param>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>Information about the plugin's execution capabilities</returns>
  Task<Result<PluginExecutionCapabilities>> GetPluginCapabilitiesAsync(
      PluginId pluginId,
      CancellationToken cancellationToken = default);
}