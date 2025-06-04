using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.ValueObjects;

namespace DevFlow.Application.Plugins.Runtime.Models;

/// <summary>
/// Represents the context for plugin dependency resolution.
/// Contains all resolved dependencies and their metadata.
/// </summary>
public sealed record PluginDependencyContext
{
    /// <summary>
    /// Gets the plugin ID this context belongs to.
    /// </summary>
    public PluginId PluginId { get; init; } = null!;

    /// <summary>
    /// Gets the resolved NuGet packages.
    /// </summary>
    public IReadOnlyList<ResolvedNuGetPackage> NuGetPackages { get; init; } = Array.Empty<ResolvedNuGetPackage>();

    /// <summary>
    /// Gets the resolved plugin references.
    /// </summary>
    public IReadOnlyList<ResolvedPluginReference> PluginReferences { get; init; } = Array.Empty<ResolvedPluginReference>();

    /// <summary>
    /// Gets the resolved file references.
    /// </summary>
    public IReadOnlyList<ResolvedFileReference> FileReferences { get; init; } = Array.Empty<ResolvedFileReference>();

    /// <summary>
    /// Gets the timestamp when this context was created.
    /// </summary>
    public DateTimeOffset ResolvedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets any warnings encountered during resolution.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets all assembly paths that should be referenced during compilation.
    /// </summary>
    public IEnumerable<string> GetAllAssemblyPaths()
    {
        return NuGetPackages.SelectMany(p => p.AssemblyPaths)
            .Concat(FileReferences.Select(f => f.FilePath))
            .Distinct();
    }

    /// <summary>
    /// Gets all runtime dependencies that should be copied to the output directory.
    /// </summary>
    public IEnumerable<string> GetRuntimeDependencyPaths()
    {
        return NuGetPackages.SelectMany(p => p.RuntimeDependencies)
            .Distinct();
    }
}

/// <summary>
/// Represents the validation result for plugin dependencies.
/// </summary>
public sealed record PluginDependencyValidation
{
    /// <summary>
    /// Gets whether all dependencies are valid and can be resolved.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the list of validation issues found.
    /// </summary>
    public IReadOnlyList<DependencyValidationIssue> Issues { get; init; } = Array.Empty<DependencyValidationIssue>();

    /// <summary>
    /// Gets the total number of dependencies validated.
    /// </summary>
    public int TotalDependencies { get; init; }

    /// <summary>
    /// Gets the number of successfully resolved dependencies.
    /// </summary>
    public int ResolvedDependencies { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static PluginDependencyValidation Success(int totalDependencies) =>
        new()
        {
            IsValid = true,
            TotalDependencies = totalDependencies,
            ResolvedDependencies = totalDependencies
        };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static PluginDependencyValidation Failed(IEnumerable<DependencyValidationIssue> issues, int totalDependencies, int resolvedDependencies) =>
        new()
        {
            IsValid = false,
            Issues = issues.ToList().AsReadOnly(),
            TotalDependencies = totalDependencies,
            ResolvedDependencies = resolvedDependencies
        };
}

/// <summary>
/// Represents a dependency validation issue.
/// </summary>
public sealed record DependencyValidationIssue
{
    /// <summary>
    /// Gets the severity of the issue.
    /// </summary>
    public DependencyIssueSeverity Severity { get; init; }

    /// <summary>
    /// Gets the dependency that caused the issue.
    /// </summary>
    public PluginDependency Dependency { get; init; } = null!;

    /// <summary>
    /// Gets the issue message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the suggested resolution for the issue.
    /// </summary>
    public string? SuggestedResolution { get; init; }
}

/// <summary>
/// Represents the severity of a dependency validation issue.
/// </summary>
public enum DependencyIssueSeverity
{
    /// <summary>
    /// A warning that doesn't prevent plugin execution.
    /// </summary>
    Warning,

    /// <summary>
    /// An error that prevents plugin execution.
    /// </summary>
    Error,

    /// <summary>
    /// A critical error that indicates a configuration problem.
    /// </summary>
    Critical
}

/// <summary>
/// Represents a resolved NuGet package dependency.
/// </summary>
public sealed record ResolvedNuGetPackage
{
    /// <summary>
    /// Gets the package name.
    /// </summary>
    public string PackageName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the paths to the package assemblies.
    /// </summary>
    public IReadOnlyList<string> AssemblyPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the paths to runtime dependencies.
    /// </summary>
    public IReadOnlyList<string> RuntimeDependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the package installation directory.
    /// </summary>
    public string InstallationPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target framework the package was resolved for.
    /// </summary>
    public string TargetFramework { get; init; } = string.Empty;

    /// <summary>
    /// Gets additional metadata about the package.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Represents a resolved plugin-to-plugin dependency.
/// </summary>
public sealed record ResolvedPluginReference
{
    /// <summary>
    /// Gets the referenced plugin ID.
    /// </summary>
    public PluginId PluginId { get; init; } = null!;

    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the path to the plugin directory.
    /// </summary>
    public string PluginPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the plugin entry point.
    /// </summary>
    public string EntryPoint { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the referenced plugin is currently available.
    /// </summary>
    public bool IsAvailable { get; init; }
}

/// <summary>
/// Represents a resolved file reference dependency.
/// </summary>
public sealed record ResolvedFileReference
{
    /// <summary>
    /// Gets the file name.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved file path.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the file exists.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Gets the file's last modified timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; init; }
}

/// <summary>
/// Represents the dependency graph for a plugin.
/// </summary>
public sealed record PluginDependencyGraph
{
    /// <summary>
    /// Gets the root plugin ID.
    /// </summary>
    public PluginId RootPluginId { get; init; } = null!;

    /// <summary>
    /// Gets the direct dependencies.
    /// </summary>
    public IReadOnlyList<DependencyGraphNode> DirectDependencies { get; init; } = Array.Empty<DependencyGraphNode>();

    /// <summary>
    /// Gets all dependencies (including transitive).
    /// </summary>
    public IReadOnlyList<DependencyGraphNode> AllDependencies { get; init; } = Array.Empty<DependencyGraphNode>();

    /// <summary>
    /// Gets any circular dependency warnings.
    /// </summary>
    public IReadOnlyList<string> CircularDependencyWarnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the maximum dependency depth.
    /// </summary>
    public int MaxDepth { get; init; }
}

/// <summary>
/// Represents a node in the dependency graph.
/// </summary>
public sealed record DependencyGraphNode
{
    /// <summary>
    /// Gets the dependency information.
    /// </summary>
    public PluginDependency Dependency { get; init; } = null!;

    /// <summary>
    /// Gets the depth of this dependency in the graph (0 = direct dependency).
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// Gets the child dependencies.
    /// </summary>
    public IReadOnlyList<DependencyGraphNode> Children { get; init; } = Array.Empty<DependencyGraphNode>();

    /// <summary>
    /// Gets whether this dependency was successfully resolved.
    /// </summary>
    public bool IsResolved { get; init; }

    /// <summary>
    /// Gets any error message if the dependency failed to resolve.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

