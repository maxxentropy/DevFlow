using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Domain.Plugins.ValueObjects;
using System.Text.RegularExpressions;

namespace DevFlow.Domain.Plugins.Extensions;

/// <summary>
/// Extension methods for working with plugin dependencies and version resolution.
/// </summary>
public static class PluginDependencyExtensions
{
  /// <summary>
  /// Checks if a dependency matches the given name (case-insensitive).
  /// </summary>
  public static bool MatchesName(this PluginDependency dependency, string name)
  {
    return string.Equals(dependency.Name, name, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Gets dependencies of a specific type from the plugin.
  /// </summary>
  public static IReadOnlyList<PluginDependency> GetDependenciesByType(
      this Plugin plugin,
      PluginDependencyType dependencyType)
  {
    return plugin.Dependencies
        .Where(d => d.Type == dependencyType)
        .ToList()
        .AsReadOnly();
  }

  /// <summary>
  /// Checks if a given version satisfies the dependency's version specifier.
  /// Supports various version patterns including SemVer ranges.
  /// </summary>
  public static bool IsVersionSatisfied(this PluginDependency dependency, string candidateVersion)
  {
    if (string.IsNullOrWhiteSpace(candidateVersion))
      return false;

    var specifier = dependency.Version.Trim();

    // Exact version match
    if (specifier == candidateVersion)
      return true;

    // Wildcard match
    if (specifier == "*" || specifier == "latest")
      return true;

    // Parse candidate version
    if (!System.Version.TryParse(candidateVersion, out var candidate))
      return false;

    return specifier switch
    {
      var s when s.StartsWith("^") => HandleCaretRange(s, candidate),
      var s when s.StartsWith("~") => HandleTildeRange(s, candidate),
      var s when s.StartsWith(">=") => HandleGreaterThanOrEqual(s, candidate),
      var s when s.StartsWith("<=") => HandleLessThanOrEqual(s, candidate),
      var s when s.StartsWith(">") => HandleGreaterThan(s, candidate),
      var s when s.StartsWith("<") => HandleLessThan(s, candidate),
      var s when s.StartsWith("==") => HandleExactMatch(s, candidate),
      var s when s.Contains("-") => HandleVersionRange(s, candidate),
      var s when System.Version.TryParse(s, out var exact) => candidate.Equals(exact),
      _ => false
    };
  }

  /// <summary>
  /// Gets a human-readable description of the dependency.
  /// </summary>
  public static string GetDescription(this PluginDependency dependency)
  {
    var source = !string.IsNullOrEmpty(dependency.Source) ? $" (from: {dependency.Source})" : "";
    return $"{dependency.Type}: {dependency.Name}@{dependency.Version}{source}";
  }

  /// <summary>
  /// Validates that the dependency has all required properties for its type.
  /// </summary>
  public static bool IsValid(this PluginDependency dependency)
  {
    if (string.IsNullOrWhiteSpace(dependency.Name) || string.IsNullOrWhiteSpace(dependency.Version))
      return false;

    return dependency.Type switch
    {
      PluginDependencyType.FileReference => !string.IsNullOrWhiteSpace(dependency.Source),
      PluginDependencyType.NuGetPackage => IsValidNuGetPackageName(dependency.Name),
      PluginDependencyType.Plugin => true,
      PluginDependencyType.NpmPackage => IsValidNpmPackageName(dependency.Name),
      PluginDependencyType.PipPackage => IsValidPipPackageName(dependency.Name),
      _ => false
    };
  }

  // Private helper methods for version resolution
  private static bool HandleCaretRange(string specifier, System.Version candidate)
  {
    var versionStr = specifier.Substring(1);
    if (!System.Version.TryParse(versionStr, out var baseVersion))
      return false;

    return candidate >= baseVersion &&
           candidate.Major == baseVersion.Major;
  }

  private static bool HandleTildeRange(string specifier, System.Version candidate)
  {
    var versionStr = specifier.Substring(1);
    if (!System.Version.TryParse(versionStr, out var baseVersion))
      return false;

    return candidate >= baseVersion &&
           candidate.Major == baseVersion.Major &&
           candidate.Minor == baseVersion.Minor;
  }

  private static bool HandleGreaterThanOrEqual(string specifier, System.Version candidate)
  {
    var versionStr = specifier.Substring(2);
    if (!System.Version.TryParse(versionStr, out var baseVersion))
      return false;

    return candidate >= baseVersion;
  }

  private static bool HandleLessThanOrEqual(string specifier, System.Version candidate)
  {
    var versionStr = specifier.Substring(2);
    if (!System.Version.TryParse(versionStr, out var baseVersion))
      return false;

    return candidate <= baseVersion;
  }

  private static bool HandleGreaterThan(string specifier, System.Version candidate)
  {
    var versionStr = specifier.Substring(1);
    if (!System.Version.TryParse(versionStr, out var baseVersion))
      return false;

    return candidate > baseVersion;
  }

  private static bool HandleLessThan(string specifier, System.Version candidate)
  {
    var versionStr = specifier.Substring(1);
    if (!System.Version.TryParse(versionStr, out var baseVersion))
      return false;

    return candidate < baseVersion;
  }

  private static bool HandleExactMatch(string specifier, System.Version candidate)
  {
    var versionStr = specifier.Substring(2);
    if (!System.Version.TryParse(versionStr, out var baseVersion))
      return false;

    return candidate.Equals(baseVersion);
  }

  private static bool HandleVersionRange(string specifier, System.Version candidate)
  {
    var parts = specifier.Split('-');
    if (parts.Length != 2)
      return false;

    if (!System.Version.TryParse(parts[0].Trim(), out var minVersion) ||
        !System.Version.TryParse(parts[1].Trim(), out var maxVersion))
      return false;

    return candidate >= minVersion && candidate <= maxVersion;
  }

  private static bool IsValidNuGetPackageName(string name)
  {
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
      return false;

    return Regex.IsMatch(name, @"^[a-zA-Z0-9._-]+$") &&
           !name.StartsWith('.') &&
           !name.EndsWith('.');
  }

  private static bool IsValidNpmPackageName(string name)
  {
    if (string.IsNullOrWhiteSpace(name) || name.Length > 214)
      return false;

    if (name.StartsWith('@'))
    {
      var parts = name.Split('/');
      if (parts.Length != 2)
        return false;

      var scope = parts[0].Substring(1);
      var packageName = parts[1];

      return IsValidNpmIdentifier(scope) && IsValidNpmIdentifier(packageName);
    }

    return IsValidNpmIdentifier(name);
  }

  private static bool IsValidNpmIdentifier(string identifier)
  {
    if (string.IsNullOrWhiteSpace(identifier))
      return false;

    return Regex.IsMatch(identifier, @"^[a-z0-9._-]+$") &&
           !identifier.StartsWith('.') &&
           !identifier.StartsWith('_') &&
           !identifier.StartsWith('-');
  }

  private static bool IsValidPipPackageName(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return false;

    var baseNameMatch = Regex.Match(name, @"^([a-zA-Z0-9._-]+)(\[.+\])?$");
    if (!baseNameMatch.Success)
      return false;

    var baseName = baseNameMatch.Groups[1].Value;

    return Regex.IsMatch(baseName, @"^[a-zA-Z0-9._-]+$") &&
           baseName.Length <= 100;
  }
}