using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Application.Plugins.Runtime.Models;

/// <summary>
/// Represents a plugin manifest parsed from a plugin.json file.
/// Contains all metadata and configuration information for a plugin.
/// </summary>
public sealed record PluginManifest
{
  /// <summary>
  /// Gets the plugin name.
  /// </summary>
  public string Name { get; init; } = string.Empty;

  /// <summary>
  /// Gets the plugin version.
  /// </summary>
  public string Version { get; init; } = string.Empty;

  /// <summary>
  /// Gets the plugin description.
  /// </summary>
  public string Description { get; init; } = string.Empty;

  /// <summary>
  /// Gets the programming language of the plugin.
  /// </summary>
  public PluginLanguage Language { get; init; }

  /// <summary>
  /// Gets the entry point file or class name for the plugin.
  /// </summary>
  public string EntryPoint { get; init; } = string.Empty;

  /// <summary>
  /// Gets the full path to the plugin directory.
  /// </summary>
  public string PluginDirectoryPath { get; init; } = string.Empty;

  /// <summary>
  /// Gets the full path to the plugin manifest file.
  /// </summary>
  public string ManifestFilePath { get; init; } = string.Empty;

  /// <summary>
  /// Gets the capabilities (permissions) required by the plugin.
  /// </summary>
  public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

  /// <summary>
  /// Gets the plugin dependencies.
  /// </summary>
  public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

  /// <summary>
  /// Gets the default configuration for the plugin.
  /// </summary>
  public IReadOnlyDictionary<string, object> Configuration { get; init; } =
      new Dictionary<string, object>();

  /// <summary>
  /// Gets additional metadata from the manifest file.
  /// </summary>
  public IReadOnlyDictionary<string, object> Metadata { get; init; } =
      new Dictionary<string, object>();

  /// <summary>
  /// Gets the timestamp when the manifest was last modified.
  /// </summary>
  public DateTimeOffset LastModified { get; init; }

  /// <summary>
  /// Creates a plugin manifest from the provided parameters with validation.
  /// </summary>
  /// <param name="name">The plugin name</param>
  /// <param name="version">The plugin version</param>
  /// <param name="description">The plugin description</param>
  /// <param name="language">The programming language</param>
  /// <param name="entryPoint">The entry point file or class</param>
  /// <param name="pluginDirectoryPath">The plugin directory path</param>
  /// <param name="manifestFilePath">The manifest file path</param>
  /// <param name="capabilities">Required capabilities</param>
  /// <param name="dependencies">Plugin dependencies</param>
  /// <param name="configuration">Default configuration</param>
  /// <param name="metadata">Additional metadata</param>
  /// <param name="lastModified">Last modification timestamp</param>
  /// <returns>A result containing the plugin manifest or validation errors</returns>
  public static Result<PluginManifest> Create(
      string name,
      string version,
      string description,
      PluginLanguage language,
      string entryPoint,
      string pluginDirectoryPath,
      string manifestFilePath,
      IReadOnlyList<string>? capabilities = null,
      IReadOnlyList<string>? dependencies = null,
      IReadOnlyDictionary<string, object>? configuration = null,
      IReadOnlyDictionary<string, object>? metadata = null,
      DateTimeOffset? lastModified = null)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.NameEmpty", "Plugin name cannot be empty."));

    if (string.IsNullOrWhiteSpace(version))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.VersionEmpty", "Plugin version cannot be empty."));

    if (!System.Version.TryParse(version, out _))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.InvalidVersion", "Plugin version must be a valid semantic version."));

    if (string.IsNullOrWhiteSpace(entryPoint))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.EntryPointEmpty", "Plugin entry point cannot be empty."));

    if (string.IsNullOrWhiteSpace(pluginDirectoryPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.DirectoryPathEmpty", "Plugin directory path cannot be empty."));

    if (!Directory.Exists(pluginDirectoryPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.DirectoryNotFound", $"Plugin directory '{pluginDirectoryPath}' does not exist."));

    if (string.IsNullOrWhiteSpace(manifestFilePath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.ManifestPathEmpty", "Manifest file path cannot be empty."));

    if (!File.Exists(manifestFilePath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.ManifestNotFound", $"Manifest file '{manifestFilePath}' does not exist."));

    // Validate entry point file exists
    var entryPointPath = Path.Combine(pluginDirectoryPath, entryPoint);
    if (!File.Exists(entryPointPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginManifest.EntryPointNotFound", $"Entry point file '{entryPointPath}' does not exist."));

    var manifest = new PluginManifest
    {
      Name = name.Trim(),
      Version = version.Trim(),
      Description = description?.Trim() ?? string.Empty,
      Language = language,
      EntryPoint = entryPoint.Trim(),
      PluginDirectoryPath = Path.GetFullPath(pluginDirectoryPath),
      ManifestFilePath = Path.GetFullPath(manifestFilePath),
      Capabilities = capabilities ?? Array.Empty<string>(),
      Dependencies = dependencies ?? Array.Empty<string>(),
      Configuration = configuration ?? new Dictionary<string, object>(),
      Metadata = metadata ?? new Dictionary<string, object>(),
      LastModified = lastModified ?? File.GetLastWriteTimeUtc(manifestFilePath)
    };

    return Result<PluginManifest>.Success(manifest);
  }

  /// <summary>
  /// Gets the absolute path to the entry point file.
  /// </summary>
  public string EntryPointPath => Path.Combine(PluginDirectoryPath, EntryPoint);

  /// <summary>
  /// Checks if the plugin manifest is newer than the specified timestamp.
  /// </summary>
  /// <param name="timestamp">The timestamp to compare against</param>
  /// <returns>True if the manifest is newer, false otherwise</returns>
  public bool IsNewerThan(DateTimeOffset timestamp) => LastModified > timestamp;

  /// <summary>
  /// Gets a configuration value with the specified key.
  /// </summary>
  /// <typeparam name="T">The type of the configuration value</typeparam>
  /// <param name="key">The configuration key</param>
  /// <param name="defaultValue">The default value if the key is not found</param>
  /// <returns>The configuration value or the default value</returns>
  public T GetConfigurationValue<T>(string key, T defaultValue = default!)
  {
    if (Configuration.TryGetValue(key, out var value) && value is T typedValue)
      return typedValue;

    return defaultValue;
  }

  /// <summary>
  /// Checks if the plugin requires a specific capability.
  /// </summary>
  /// <param name="capability">The capability to check</param>
  /// <returns>True if the capability is required, false otherwise</returns>
  public bool RequiresCapability(string capability) =>
      Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
}