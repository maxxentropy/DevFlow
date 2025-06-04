using DevFlow.SharedKernel.ValueObjects;
using DevFlow.SharedKernel.Results;
using System.Text.RegularExpressions;
using System.Linq; // Required for Enumerable.All
using System.Collections.Generic; // Required for IEnumerable
using System; // Required for System.Version and StringComparison


namespace DevFlow.Domain.Plugins.ValueObjects;

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
  FileReference,

  /// <summary>
  /// An NPM package dependency for TypeScript/JavaScript plugins.
  /// </summary>
  NpmPackage,

  /// <summary>
  /// A PIP package dependency for Python plugins.
  /// </summary>
  PipPackage
}

/// <summary>
/// Represents a plugin dependency with version constraints and metadata.
/// Supports NuGet packages, file references, and plugin-to-plugin dependencies.
/// </summary>
public sealed class PluginDependency : ValueObject
{
  private PluginDependency(string name, string version, PluginDependencyType type, string? source = null)
  {
    Name = name;
    Version = version; // Stores the full version specifier, e.g., "^1.0.0", ">=2.1.0", "1.2.3"
    Type = type;
    Source = source; // For FileReference, this is the path. For NuGet, optional feed.
  }

  public string Name { get; }
  public string Version { get; } // Full version specifier
  public PluginDependencyType Type { get; }
  public string? Source { get; }

  public static Result<PluginDependency> CreateNuGetPackage(string packageName, string version, string? source = null)
  {
    if (string.IsNullOrWhiteSpace(packageName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.PackageNameEmpty", "NuGet package name cannot be empty."));
    if (string.IsNullOrWhiteSpace(version))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.VersionEmpty", "Package version specifier cannot be empty."));
    if (!IsValidNuGetPackageName(packageName)) // Renamed for clarity
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.InvalidPackageName", $"Package name '{packageName}' is not a valid NuGet package name."));
    return Result<PluginDependency>.Success(new PluginDependency(packageName.Trim(), version.Trim(), PluginDependencyType.NuGetPackage, source?.Trim()));
  }

  public static Result<PluginDependency> CreatePluginDependency(string pluginName, string version)
  {
    if (string.IsNullOrWhiteSpace(pluginName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.PluginNameEmpty", "Plugin name cannot be empty."));
    if (string.IsNullOrWhiteSpace(version))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.VersionEmpty", "Plugin version specifier cannot be empty."));
    return Result<PluginDependency>.Success(new PluginDependency(pluginName.Trim(), version.Trim(), PluginDependencyType.Plugin));
  }

  public static Result<PluginDependency> CreateFileReference(string logicalName, string version, string filePath)
  {
    if (string.IsNullOrWhiteSpace(logicalName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.FileNameEmpty", "File reference logical name cannot be empty."));
    if (string.IsNullOrWhiteSpace(version))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.VersionEmpty", "File version specifier cannot be empty."));
    if (string.IsNullOrWhiteSpace(filePath))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.FilePathEmpty", "File path (source) cannot be empty for file reference."));
    // 'logicalName' is the key, 'filePath' is stored in Source.
    return Result<PluginDependency>.Success(new PluginDependency(logicalName.Trim(), version.Trim(), PluginDependencyType.FileReference, filePath.Trim()));
  }

  public static Result<PluginDependency> CreateNpmPackage(string packageName, string version)
  {
    if (string.IsNullOrWhiteSpace(packageName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.NpmPackageNameEmpty", "NPM package name cannot be empty."));
    // NPM version can be complex (tags, git URLs), allow broad specifier, validation happens during 'npm install'
    if (string.IsNullOrWhiteSpace(version))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.NpmVersionEmpty", "NPM package version specifier cannot be empty."));
    return Result<PluginDependency>.Success(new PluginDependency(packageName.Trim(), version.Trim(), PluginDependencyType.NpmPackage));
  }

  public static Result<PluginDependency> CreatePipPackage(string packageName, string versionSpecifier)
  {
    if (string.IsNullOrWhiteSpace(packageName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDependency.PipPackageNameEmpty", "Pip package name cannot be empty."));
    // Pip version specifier can be empty (latest), or like >=1.0, ==1.0 etc.
    if (string.IsNullOrWhiteSpace(versionSpecifier)) // Allow empty, meaning "any" or "latest"
      versionSpecifier = "*"; // Normalize empty to "*" for IsVersionSatisfied
    return Result<PluginDependency>.Success(new PluginDependency(packageName.Trim(), versionSpecifier.Trim(), PluginDependencyType.PipPackage));
  }


  public bool MatchesName(string name) =>
      string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);

  public bool IsVersionSatisfied(string availableVersionString)
  {
    if (string.IsNullOrWhiteSpace(availableVersionString)) return false;
    // Handle wildcards universally
    if (Version == "*" || Version == "latest" || string.IsNullOrWhiteSpace(Version))
      return true;

    if (!System.Version.TryParse(availableVersionString, out var availableVersion))
    {
      // If availableVersionString is not a simple x.y.z, it might be a pre-release.
      // System.Version parsing is limited for full SemVer.
      // For this example, if it's not parsable by System.Version, we consider it non-matching
      // unless the specifier is also non-standard in a way we handle (e.g. direct string match for some npm tags)
      // A full SemVer library would be better here.
      return Version.Equals(availableVersionString, StringComparison.OrdinalIgnoreCase); // Fallback to exact match for complex strings
    }


    try
    {
      string versionSpecifier = Version;

      if (versionSpecifier.StartsWith("^"))
      {
        var versionPart = versionSpecifier.Substring(1);
        if (!System.Version.TryParse(versionPart, out var specifiedCaretVersion)) return false;
        if (availableVersion < specifiedCaretVersion) return false;
        if (specifiedCaretVersion.Major != 0)
          return availableVersion.Major == specifiedCaretVersion.Major;
        if (specifiedCaretVersion.Minor != 0)
          return availableVersion.Major == 0 && availableVersion.Minor == specifiedCaretVersion.Minor;
        return availableVersion.Major == 0 && availableVersion.Minor == 0 && availableVersion.Build == specifiedCaretVersion.Build;
      }

      if (versionSpecifier.StartsWith("~")) // Includes `~=` (PEP 440 for pip) if we treat them similarly
      {
        var versionPart = versionSpecifier.StartsWith("~=") ? versionSpecifier.Substring(2) : versionSpecifier.Substring(1);
        if (!System.Version.TryParse(versionPart, out var specifiedTildeVersion)) return false;
        if (availableVersion < specifiedTildeVersion) return false;
        // ~1.2.3 allows patch (1.2.x) -> available < 1.3.0
        // ~1.2 allows patch (1.2.x) -> available < 1.3.0
        // ~1 allows minor (1.x) -> available < 2.0.0
        if (specifiedTildeVersion.Build != -1) // ~1.2.3
          return availableVersion.Major == specifiedTildeVersion.Major && availableVersion.Minor == specifiedTildeVersion.Minor;
        if (specifiedTildeVersion.Minor != -1) // ~1.2
          return availableVersion.Major == specifiedTildeVersion.Major && availableVersion.Minor == specifiedTildeVersion.Minor;
        // ~1
        return availableVersion.Major == specifiedTildeVersion.Major;
      }

      if (versionSpecifier.StartsWith(">="))
      {
        var versionPart = versionSpecifier.Substring(2);
        if (!System.Version.TryParse(versionPart, out var minVersion)) return false;
        return availableVersion >= minVersion;
      }

      if (versionSpecifier.StartsWith(">"))
      {
        var versionPart = versionSpecifier.Substring(1);
        if (!System.Version.TryParse(versionPart, out var exclusiveMinVersion)) return false;
        return availableVersion > exclusiveMinVersion;
      }

      if (versionSpecifier.StartsWith("<="))
      {
        var versionPart = versionSpecifier.Substring(2);
        if (!System.Version.TryParse(versionPart, out var maxVersion)) return false;
        return availableVersion <= maxVersion;
      }

      if (versionSpecifier.StartsWith("<"))
      {
        var versionPart = versionSpecifier.Substring(1);
        if (!System.Version.TryParse(versionPart, out var exclusiveMaxVersion)) return false;
        return availableVersion < exclusiveMaxVersion;
      }
      if (versionSpecifier.StartsWith("==")) // Common in pip
      {
        var versionPart = versionSpecifier.Substring(2);
        if (!System.Version.TryParse(versionPart, out var exactPipVersion)) return false;
        return availableVersion == exactPipVersion;
      }
      if (versionSpecifier.StartsWith("!=")) // Common in pip
      {
        var versionPart = versionSpecifier.Substring(2);
        if (!System.Version.TryParse(versionPart, out var notEqualVersion)) return false;
        return availableVersion != notEqualVersion;
      }


      // Exact version match (default if no operator or if System.Version.TryParse succeeds)
      if (System.Version.TryParse(versionSpecifier, out var exactVersion))
      {
        return availableVersion == exactVersion;
      }

      // Fallback for non-standard versions or tags (e.g., npm "latest", "next")
      // This is a simplistic check; real npm/pip tag resolution is more complex.
      return versionSpecifier.Equals(availableVersionString, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }

  public string GetDescription()
  {
    var typeDescription = Type switch
    {
      PluginDependencyType.NuGetPackage => "NuGet Package",
      PluginDependencyType.Plugin => "Plugin",
      PluginDependencyType.FileReference => "File Reference",
      PluginDependencyType.NpmPackage => "NPM Package",
      PluginDependencyType.PipPackage => "Pip Package",
      _ => "Unknown"
    };
    var sourceInfo = Type == PluginDependencyType.FileReference && !string.IsNullOrWhiteSpace(Source) ? $" (Path: {Source})" : "";
    return $"{typeDescription}: {Name} Version: {Version}{sourceInfo}";
  }

  private static bool IsValidNuGetPackageName(string packageName) // Renamed
  {
    if (string.IsNullOrWhiteSpace(packageName)) return false;
    if (packageName.StartsWith('.') || packageName.EndsWith('.')) return false;
    // NuGet package IDs are case-insensitive on the server but often case-preserved.
    // Basic validation: letters, numbers, '.', '_', '-'
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