using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Application.Plugins.Runtime;

/// <summary>
/// Defines the contract for discovering and loading plugins from the filesystem.
/// Responsible for scanning plugin directories, validating plugin manifests, and creating plugin entities.
/// </summary>
public interface IPluginDiscoveryService
{
  /// <summary>
  /// Discovers all plugins in the specified directory and its subdirectories.
  /// </summary>
  /// <param name="pluginDirectoryPath">The root directory to scan for plugins</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A collection of discovered plugin manifests</returns>
  Task<Result<IReadOnlyList<PluginManifest>>> DiscoverPluginsAsync(
      string pluginDirectoryPath,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Discovers plugins in multiple directories concurrently.
  /// </summary>
  /// <param name="pluginDirectoryPaths">The directories to scan for plugins</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A collection of discovered plugin manifests from all directories</returns>
  Task<Result<IReadOnlyList<PluginManifest>>> DiscoverPluginsAsync(
      IEnumerable<string> pluginDirectoryPaths,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Loads a specific plugin from its manifest and creates a plugin entity.
  /// </summary>
  /// <param name="manifest">The plugin manifest containing metadata and file paths</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A fully configured plugin entity ready for registration</returns>
  Task<Result<Plugin>> LoadPluginAsync(
      PluginManifest manifest,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Validates that a plugin manifest is well-formed and complete.
  /// </summary>
  /// <param name="manifest">The plugin manifest to validate</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>True if the manifest is valid, false otherwise with validation errors</returns>
  Task<Result<bool>> ValidateManifestAsync(
      PluginManifest manifest,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Checks if a plugin at the specified path has been modified since the last scan.
  /// Useful for implementing hot-reload functionality.
  /// </summary>
  /// <param name="pluginDirectoryPath">The plugin directory path</param>
  /// <param name="lastScanTime">The timestamp of the last scan</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>True if the plugin has been modified, false otherwise</returns>
  Task<Result<bool>> IsPluginModifiedAsync(
      string pluginDirectoryPath,
      DateTimeOffset lastScanTime,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Scans a single plugin directory and attempts to parse its manifest.
  /// </summary>
  /// <param name="pluginDirectoryPath">The path to the plugin directory</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A plugin manifest if found and valid, or an error result</returns>
  Task<Result<PluginManifest>> ScanPluginDirectoryAsync(
      string pluginDirectoryPath,
      CancellationToken cancellationToken = default);
}