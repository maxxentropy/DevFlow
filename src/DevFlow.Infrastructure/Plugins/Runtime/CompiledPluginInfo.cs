using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;

namespace DevFlow.Infrastructure.Plugins.Runtime;

/// <summary>
/// Information about a compiled plugin stored in cache.
/// Contains metadata about the compilation and paths to the compiled assembly.
/// </summary>
public sealed record CompiledPluginInfo
{
  /// <summary>
  /// The unique identifier of the plugin that was compiled.
  /// </summary>
  public required PluginId PluginId { get; init; }

  /// <summary>
  /// The file system path to the compiled assembly (.dll file).
  /// </summary>
  public required string AssemblyPath { get; init; }

  /// <summary>
  /// The cache key used to identify this compiled plugin.
  /// Based on plugin ID, version, and content hash.
  /// </summary>
  public required string CacheKey { get; init; }

  /// <summary>
  /// The timestamp when this plugin was compiled.
  /// </summary>
  public required DateTimeOffset CompiledAt { get; init; }

  /// <summary>
  /// The resolved dependency context that was used during compilation.
  /// Contains information about NuGet packages, file references, etc.
  /// </summary>
  public required PluginDependencyContext Dependencies { get; init; }
}