using DevFlow.SharedKernel.ValueObjects;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Domain.Plugins.ValueObjects;

/// <summary>
/// Represents a plugin dependency with version constraints and metadata.
/// Supports NuGet packages, file references, and plugin-to-plugin dependencies.
/// </summary>
public sealed class PluginDependency : ValueObject
{
    private PluginDependency(string name, string version, PluginDependencyType type, string? source = null)
    {
        Name = name;
        Version = version;
        Type = type;
        Source = source;
    }

    /// <summary>
    /// Gets the dependency name (e.g., package name, plugin name, file name).
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the version constraint (e.g., "1.0.0", ">= 2.0.0", "[1.0.0, 2.0.0)").
    /// </summary>
    public string Version { get; private set; }

    /// <summary>
    /// Gets the type of dependency.
    /// </summary>
    public PluginDependencyType Type { get; private set; }

    /// <summary>
    /// Gets the optional source location (e.g., NuGet feed URL, file path).
    /// </summary>
    public string? Source { get; private set; }

    /// <summary>
    /// Creates a NuGet package dependency.
    /// </summary>
    /// <param name="packageName">The NuGet package name</param>
    /// <param name="version">The version constraint</param>
    /// <param name="source">Optional NuGet feed source</param>
    /// <returns>A result containing the dependency or validation errors</returns>
    public static Result<PluginDependency> CreateNuGetPackage(string packageName, string version, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.PackageNameEmpty", "NuGet package name cannot be empty."));

        if (string.IsNullOrWhiteSpace(version))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.VersionEmpty", "Package version cannot be empty."));

        // Validate package name format (basic validation)
        if (!IsValidPackageName(packageName))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.InvalidPackageName", 
                $"Package name '{packageName}' is not a valid NuGet package name."));

        return Result<PluginDependency>.Success(new PluginDependency(
            packageName.Trim(), 
            version.Trim(), 
            PluginDependencyType.NuGetPackage, 
            source?.Trim()));
    }

    /// <summary>
    /// Creates a plugin-to-plugin dependency.
    /// </summary>
    /// <param name="pluginName">The target plugin name</param>
    /// <param name="version">The version constraint</param>
    /// <returns>A result containing the dependency or validation errors</returns>
    public static Result<PluginDependency> CreatePluginDependency(string pluginName, string version)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.PluginNameEmpty", "Plugin name cannot be empty."));

        if (string.IsNullOrWhiteSpace(version))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.VersionEmpty", "Plugin version cannot be empty."));

        return Result<PluginDependency>.Success(new PluginDependency(
            pluginName.Trim(), 
            version.Trim(), 
            PluginDependencyType.Plugin));
    }

    /// <summary>
    /// Creates a file reference dependency.
    /// </summary>
    /// <param name="fileName">The file name or assembly name</param>
    /// <param name="version">The version constraint</param>
    /// <param name="filePath">The path to the file</param>
    /// <returns>A result containing the dependency or validation errors</returns>
    public static Result<PluginDependency> CreateFileReference(string fileName, string version, string filePath)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.FileNameEmpty", "File name cannot be empty."));

        if (string.IsNullOrWhiteSpace(version))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.VersionEmpty", "File version cannot be empty."));

        if (string.IsNullOrWhiteSpace(filePath))
            return Result<PluginDependency>.Failure(Error.Validation(
                "PluginDependency.FilePathEmpty", "File path cannot be empty."));

        return Result<PluginDependency>.Success(new PluginDependency(
            fileName.Trim(), 
            version.Trim(), 
            PluginDependencyType.FileReference, 
            filePath.Trim()));
    }

    /// <summary>
    /// Checks if this dependency matches the specified name.
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <returns>True if the names match (case-insensitive), false otherwise</returns>
    public bool MatchesName(string name) => 
        string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the specified version satisfies this dependency's version constraint.
    /// </summary>
    /// <param name="availableVersion">The available version to check</param>
    /// <returns>True if the version satisfies the constraint, false otherwise</returns>
    public bool IsVersionSatisfied(string availableVersion)
    {
        try
        {
            // For now, implement simple exact match. 
            // This can be enhanced to support NuGet version ranges later.
            if (Version == "*" || Version == "latest")
                return true;

            if (Version.StartsWith(">="))
            {
                var minVersion = Version.Substring(2).Trim();
                return System.Version.TryParse(availableVersion, out var available) &&
                       System.Version.TryParse(minVersion, out var minimum) &&
                       available >= minimum;
            }

            if (Version.StartsWith(">"))
            {
                var minVersion = Version.Substring(1).Trim();
                return System.Version.TryParse(availableVersion, out var available) &&
                       System.Version.TryParse(minVersion, out var minimum) &&
                       available > minimum;
            }

            // Exact version match
            return string.Equals(Version, availableVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a human-readable description of this dependency.
    /// </summary>
    /// <returns>A formatted description string</returns>
    public string GetDescription()
    {
        var typeDescription = Type switch
        {
            PluginDependencyType.NuGetPackage => "NuGet Package",
            PluginDependencyType.Plugin => "Plugin",
            PluginDependencyType.FileReference => "File Reference",
            _ => "Unknown"
        };

        var sourceInfo = !string.IsNullOrWhiteSpace(Source) ? $" from {Source}" : "";
        return $"{typeDescription}: {Name} v{Version}{sourceInfo}";
    }

    /// <summary>
    /// Validates a NuGet package name format.
    /// </summary>
    /// <param name="packageName">The package name to validate</param>
    /// <returns>True if the package name is valid, false otherwise</returns>
    private static bool IsValidPackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return false;

        // Basic NuGet package name validation
        // - Must not start or end with dots
        // - Must contain only letters, numbers, hyphens, dots, and underscores
        // - Must be at least 1 character long
        if (packageName.StartsWith('.') || packageName.EndsWith('.'))
            return false;

        return packageName.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_');
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Version;
        yield return Type;
        yield return Source;
    }
}

/// <summary>
/// Represents the type of plugin dependency.
/// </summary>
public enum PluginDependencyType
{
    /// <summary>
    /// A NuGet package dependency.
    /// </summary>
    NuGetPackage,

    /// <summary>
    /// A dependency on another plugin.
    /// </summary>
    Plugin,

    /// <summary>
    /// A file reference dependency (assembly, DLL, etc.).
    /// </summary>
    FileReference
}

