using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.SharedKernel.Results;
using System.Text.RegularExpressions;

/// <summary>
/// Factory methods for creating common dependency types with validation.
/// </summary>
public static class PluginDependencyFactory
{
  /// <summary>
  /// Creates a NuGet package dependency with SemVer validation.
  /// </summary>
  public static Result<PluginDependency> CreateNuGetDependency(string packageName, string versionSpecifier)
  {
    if (string.IsNullOrWhiteSpace(packageName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidPackageName", "Package name cannot be empty"));

    if (string.IsNullOrWhiteSpace(versionSpecifier))
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidVersion", "Version specifier cannot be empty"));

    if (!IsValidNuGetPackageName(packageName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidNuGetName", $"Invalid NuGet package name: {packageName}"));

    return PluginDependency.CreateNuGetPackage(packageName, versionSpecifier);
  }

  /// <summary>
  /// Creates a plugin reference dependency with validation.
  /// </summary>
  public static Result<PluginDependency> CreatePluginReference(string pluginName, string versionSpecifier)
  {
    if (string.IsNullOrWhiteSpace(pluginName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidPluginName", "Plugin name cannot be empty"));

    if (string.IsNullOrWhiteSpace(versionSpecifier))
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidVersion", "Version specifier cannot be empty"));

    return PluginDependency.CreatePluginDependency(pluginName, versionSpecifier);
  }

  /// <summary>
  /// Creates a file reference dependency with path validation.
  /// </summary>
  public static Result<PluginDependency> CreateFileReference(string fileName, string filePath, string? version = null)
  {
    if (string.IsNullOrWhiteSpace(fileName))
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidFileName", "File name cannot be empty"));

    if (string.IsNullOrWhiteSpace(filePath))
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidFilePath", "File path cannot be empty"));

    try
    {
      // Validate path format (doesn't check if file exists)
      Path.GetFullPath(filePath);
    }
    catch (Exception ex)
    {
      return Result<PluginDependency>.Failure(Error.Validation(
          "Dependency.InvalidPath", $"Invalid file path format: {ex.Message}"));
    }

    return PluginDependency.CreateFileReference(fileName, version ?? "1.0.0", filePath);
  }

  // Private validation methods (same as in extensions)
  private static bool IsValidNuGetPackageName(string name)
  {
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
      return false;

    return Regex.IsMatch(name, @"^[a-zA-Z0-9._-]+$") &&
           !name.StartsWith('.') &&
           !name.EndsWith('.');
  }
}
