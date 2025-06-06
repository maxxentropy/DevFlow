// File: src/DevFlow.Application/Plugins/Runtime/IPluginDependencyResolver.cs
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Application.Plugins.Runtime;

/// <summary>
/// Service interface for resolving and managing plugin dependencies.
/// Handles NuGet packages, plugin-to-plugin dependencies, and file references.
/// </summary>
public interface IPluginDependencyResolver
{
  /// <summary>
  /// Resolves all dependencies for a plugin and returns the resolution context.
  /// </summary>
  /// <param name="plugin">The plugin to resolve dependencies for</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result containing the dependency resolution context</returns>
  Task<Result<PluginDependencyContext>> ResolveDependenciesAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Validates that all dependencies for a plugin can be resolved.
  /// </summary>
  /// <param name="plugin">The plugin to validate dependencies for</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result containing validation status and any issues</returns>
  Task<Result<PluginDependencyValidation>> ValidateDependenciesAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Resolves a specific NuGet package dependency.
  /// </summary>
  /// <param name="dependency">The NuGet package dependency to resolve</param>
  /// <param name="targetFramework">The target framework for package resolution (e.g., "net8.0")</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result containing the resolved package information</returns>
  Task<Result<ResolvedNuGetPackage>> ResolveNuGetPackageAsync(
      PluginDependency dependency,
      string targetFramework = "net8.0",
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Resolves a plugin-to-plugin dependency.
  /// </summary>
  /// <param name="dependency">The plugin dependency to resolve</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result containing the resolved plugin reference</returns>
  Task<Result<ResolvedPluginReference>> ResolvePluginDependencyAsync(
      PluginDependency dependency,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Resolves a file reference dependency.
  /// </summary>
  /// <param name="dependency">The file reference dependency to resolve</param>
  /// <param name="baseDirectory">The base directory for relative path resolution (typically the plugin's own directory)</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result containing the resolved file reference</returns>
  Task<Result<ResolvedFileReference>> ResolveFileReferenceAsync(
      PluginDependency dependency,
      string baseDirectory,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Downloads and caches a specific version of a NuGet package.
  /// </summary>
  /// <param name="packageName">The name of the NuGet package</param>
  /// <param name="specificVersion">The specific version of the package to download</param>
  /// <param name="targetFramework">The target framework</param>
  /// <param name="packageInstallPath">The full path where this specific version should be installed/cached.</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result indicating success or failure of the download and extraction.</returns>
  Task<Result> DownloadAndExtractSpecificNuGetPackageAsync(
      string packageName,
      string specificVersion,
      string targetFramework,
      string packageInstallPath,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Clears the dependency cache. Can be global or plugin-specific if implemented.
  /// </summary>
  /// <param name="pluginId">Optional plugin ID to clear cache for. If null, might clear global cache elements or be a no-op.</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A task representing the cache clearing operation</returns>
  Task ClearDependencyCacheAsync(
      PluginId? pluginId = null,
      CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets the dependency graph for a plugin, including transitive dependencies.
  /// </summary>
  /// <param name="plugin">The plugin to analyze</param>
  /// <param name="includeTransitive">Whether to include transitive dependencies</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  /// <returns>A result containing the dependency graph</returns>
  Task<Result<PluginDependencyGraph>> GetDependencyGraphAsync(
      Plugin plugin,
      bool includeTransitive = true,
      CancellationToken cancellationToken = default);
}