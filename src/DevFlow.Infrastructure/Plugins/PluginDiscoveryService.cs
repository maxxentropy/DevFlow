using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevFlow.Infrastructure.Plugins;

/// <summary>
/// Concrete implementation of plugin discovery service that scans the filesystem for plugins.
/// Handles plugin manifest parsing, validation, and plugin entity creation.
/// </summary>
public sealed class PluginDiscoveryService : IPluginDiscoveryService
{
  private readonly ILogger<PluginDiscoveryService> _logger;
  private readonly JsonSerializerOptions _jsonOptions;

  private const string PluginManifestFileName = "plugin.json";

  public PluginDiscoveryService(ILogger<PluginDiscoveryService> logger)
  {
    _logger = logger;
    _jsonOptions = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      ReadCommentHandling = JsonCommentHandling.Skip,
      AllowTrailingCommas = true
    };
  }

  public async Task<Result<IReadOnlyList<PluginManifest>>> DiscoverPluginsAsync(
      string pluginDirectoryPath,
      CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(pluginDirectoryPath))
      return Result<IReadOnlyList<PluginManifest>>.Failure(Error.Validation(
          "PluginDiscovery.DirectoryPathEmpty", "Plugin directory path cannot be empty."));

    if (!Directory.Exists(pluginDirectoryPath))
      return Result<IReadOnlyList<PluginManifest>>.Failure(Error.Validation(
          "PluginDiscovery.DirectoryNotFound", $"Plugin directory '{pluginDirectoryPath}' does not exist."));

    _logger.LogInformation("Discovering plugins in directory: {PluginDirectory}", pluginDirectoryPath);

    try
    {
      var manifests = new List<PluginManifest>();
      var pluginDirectories = Directory.GetDirectories(pluginDirectoryPath, "*", SearchOption.AllDirectories)
          .Where(dir => File.Exists(Path.Combine(dir, PluginManifestFileName)))
          .ToList();

      _logger.LogDebug("Found {Count} potential plugin directories", pluginDirectories.Count);

      foreach (var directory in pluginDirectories)
      {
        cancellationToken.ThrowIfCancellationRequested();

        var manifestResult = await ScanPluginDirectoryAsync(directory, cancellationToken);
        if (manifestResult.IsSuccess)
        {
          manifests.Add(manifestResult.Value);
          _logger.LogDebug("Successfully loaded plugin manifest: {PluginName} v{Version}",
              manifestResult.Value.Name, manifestResult.Value.Version);
        }
        else
        {
          _logger.LogWarning("Failed to load plugin from directory {Directory}: {Error}",
              directory, manifestResult.Error.Message);
        }
      }

      _logger.LogInformation("Discovered {Count} valid plugins from {TotalDirectories} directories",
          manifests.Count, pluginDirectories.Count);

      return Result<IReadOnlyList<PluginManifest>>.Success(manifests.AsReadOnly());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to discover plugins in directory: {PluginDirectory}", pluginDirectoryPath);
      return Result<IReadOnlyList<PluginManifest>>.Failure(Error.Failure(
          "PluginDiscovery.DiscoveryFailed", $"Plugin discovery failed: {ex.Message}"));
    }
  }

  public async Task<Result<IReadOnlyList<PluginManifest>>> DiscoverPluginsAsync(
      IEnumerable<string> pluginDirectoryPaths,
      CancellationToken cancellationToken = default)
  {
    var directoryPaths = pluginDirectoryPaths?.ToList();
    if (directoryPaths is null || !directoryPaths.Any())
      return Result<IReadOnlyList<PluginManifest>>.Success(Array.Empty<PluginManifest>());

    _logger.LogInformation("Discovering plugins in {Count} directories", directoryPaths.Count);

    try
    {
      var discoveryTasks = directoryPaths.Select(path => DiscoverPluginsAsync(path, cancellationToken));
      var results = await Task.WhenAll(discoveryTasks);

      var allManifests = new List<PluginManifest>();
      var errors = new List<Error>();

      foreach (var result in results)
      {
        if (result.IsSuccess)
          allManifests.AddRange(result.Value);
        else
          errors.Add(result.Error);
      }

      if (errors.Any() && !allManifests.Any())
      {
        var combinedError = Error.Failure(
            "PluginDiscovery.AllDirectoriesFailed",
            $"Failed to discover plugins in all directories. Errors: {string.Join("; ", errors.Select(e => e.Message))}");
        return Result<IReadOnlyList<PluginManifest>>.Failure(combinedError);
      }

      if (errors.Any())
        _logger.LogWarning("Some directories failed during discovery: {ErrorCount} errors", errors.Count);

      _logger.LogInformation("Discovered {Count} total plugins from {DirectoryCount} directories",
          allManifests.Count, directoryPaths.Count);

      return Result<IReadOnlyList<PluginManifest>>.Success(allManifests.AsReadOnly());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to discover plugins in multiple directories");
      return Result<IReadOnlyList<PluginManifest>>.Failure(Error.Failure(
          "PluginDiscovery.BatchDiscoveryFailed", $"Batch plugin discovery failed: {ex.Message}"));
    }
  }

  public Task<Result<Plugin>> LoadPluginAsync(
      PluginManifest manifest,
      CancellationToken cancellationToken = default)
  {
    if (manifest is null)
      return Task.FromResult(Result<Plugin>.Failure(Error.Validation(
          "PluginDiscovery.ManifestNull", "Plugin manifest cannot be null.")));

    _logger.LogDebug("Loading plugin: {PluginName} v{Version}", manifest.Name, manifest.Version);

    try
    {
      var pluginResult = Plugin.Create(
          manifest.Name,
          manifest.Version,
          manifest.Description,
          manifest.Language,
          manifest.EntryPoint,
          manifest.PluginDirectoryPath,
          manifest.Capabilities.ToList(),
          manifest.Configuration.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

      if (pluginResult.IsFailure)
      {
        _logger.LogWarning("Failed to create plugin entity: {Error}", pluginResult.Error.Message);
        return Task.FromResult(pluginResult);
      }

      _logger.LogDebug("Successfully loaded plugin: {PluginName} v{Version}", manifest.Name, manifest.Version);
      return Task.FromResult(pluginResult);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to load plugin from manifest: {PluginName}", manifest.Name);
      return Task.FromResult(Result<Plugin>.Failure(Error.Failure(
          "PluginDiscovery.LoadFailed", $"Failed to load plugin '{manifest.Name}': {ex.Message}")));
    }
  }

  public async Task<Result<bool>> ValidateManifestAsync(
      PluginManifest manifest,
      CancellationToken cancellationToken = default)
  {
    if (manifest is null)
      return Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.ManifestNull", "Plugin manifest cannot be null."));

    try
    {
      // Check if entry point file exists
      if (!File.Exists(manifest.EntryPointPath))
        return Result<bool>.Failure(Error.Validation(
            "PluginDiscovery.EntryPointNotFound", $"Entry point file '{manifest.EntryPointPath}' does not exist."));

      // Validate language-specific requirements
      var languageValidation = await ValidateLanguageRequirementsAsync(manifest, cancellationToken);
      if (languageValidation.IsFailure)
        return languageValidation;

      // Validate dependencies
      foreach (var dependency in manifest.Dependencies)
      {
        if (string.IsNullOrWhiteSpace(dependency))
          return Result<bool>.Failure(Error.Validation(
              "PluginDiscovery.InvalidDependency", "Plugin dependencies cannot be null or empty."));
      }

      // Validate capabilities
      foreach (var capability in manifest.Capabilities)
      {
        if (string.IsNullOrWhiteSpace(capability))
          return Result<bool>.Failure(Error.Validation(
              "PluginDiscovery.InvalidCapability", "Plugin capabilities cannot be null or empty."));
      }

      return Result<bool>.Success(true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate plugin manifest: {PluginName}", manifest.Name);
      return Result<bool>.Failure(Error.Failure(
          "PluginDiscovery.ValidationFailed", $"Manifest validation failed: {ex.Message}"));
    }
  }

  public Task<Result<bool>> IsPluginModifiedAsync(
      string pluginDirectoryPath,
      DateTimeOffset lastScanTime,
      CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(pluginDirectoryPath))
      return Task.FromResult(Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.DirectoryPathEmpty", "Plugin directory path cannot be empty.")));

    if (!Directory.Exists(pluginDirectoryPath))
      return Task.FromResult(Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.DirectoryNotFound", $"Plugin directory '{pluginDirectoryPath}' does not exist.")));

    try
    {
      var manifestPath = Path.Combine(pluginDirectoryPath, PluginManifestFileName);
      if (!File.Exists(manifestPath))
        return Task.FromResult(Result<bool>.Success(false));

      var lastWriteTime = File.GetLastWriteTimeUtc(manifestPath);
      var isModified = lastWriteTime > lastScanTime.UtcDateTime;

      return Task.FromResult(Result<bool>.Success(isModified));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to check plugin modification time: {PluginDirectory}", pluginDirectoryPath);
      return Task.FromResult(Result<bool>.Failure(Error.Failure(
          "PluginDiscovery.ModificationCheckFailed", $"Failed to check modification time: {ex.Message}")));
    }
  }

  public async Task<Result<PluginManifest>> ScanPluginDirectoryAsync(
      string pluginDirectoryPath,
      CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(pluginDirectoryPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.DirectoryPathEmpty", "Plugin directory path cannot be empty."));

    if (!Directory.Exists(pluginDirectoryPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.DirectoryNotFound", $"Plugin directory '{pluginDirectoryPath}' does not exist."));

    var manifestPath = Path.Combine(pluginDirectoryPath, PluginManifestFileName);
    if (!File.Exists(manifestPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.ManifestNotFound", $"Plugin manifest file not found at '{manifestPath}'."));

    try
    {
      var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
      var manifestData = JsonSerializer.Deserialize<Dictionary<string, object>>(manifestJson, _jsonOptions);

      if (manifestData is null)
        return Result<PluginManifest>.Failure(Error.Validation(
            "PluginDiscovery.InvalidManifestFormat", "Plugin manifest file contains invalid JSON."));

      var parseResult = ParseManifestData(manifestData, pluginDirectoryPath, manifestPath);
      return parseResult;
    }
    catch (JsonException ex)
    {
      _logger.LogWarning("Invalid JSON in plugin manifest: {ManifestPath}. Error: {Error}", manifestPath, ex.Message);
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.InvalidJson", $"Invalid JSON in manifest file: {ex.Message}"));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to scan plugin directory: {PluginDirectory}", pluginDirectoryPath);
      return Result<PluginManifest>.Failure(Error.Failure(
          "PluginDiscovery.ScanFailed", $"Failed to scan plugin directory: {ex.Message}"));
    }
  }

  private Result<PluginManifest> ParseManifestData(
      Dictionary<string, object> manifestData,
      string pluginDirectoryPath,
      string manifestPath)
  {
    try
    {
      var name = GetStringValue(manifestData, "name");
      var version = GetStringValue(manifestData, "version");
      var description = GetStringValue(manifestData, "description", string.Empty);
      var languageStr = GetStringValue(manifestData, "language");
      var entryPoint = GetStringValue(manifestData, "entryPoint");

      if (!Enum.TryParse<PluginLanguage>(languageStr, true, out var language))
        return Result<PluginManifest>.Failure(Error.Validation(
            "PluginDiscovery.InvalidLanguage", $"Invalid or unsupported language: '{languageStr}'."));

      var capabilities = GetStringArray(manifestData, "capabilities");
      var dependencies = GetStringArray(manifestData, "dependencies");
      var configuration = GetObjectDictionary(manifestData, "configuration");
      var metadata = manifestData.Where(kvp => !IsReservedKey(kvp.Key))
          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

      return PluginManifest.Create(
          name, version, description, language, entryPoint,
          pluginDirectoryPath, manifestPath, capabilities, dependencies,
          configuration, metadata);
    }
    catch (Exception ex)
    {
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.ManifestParseError", $"Failed to parse manifest data: {ex.Message}"));
    }
  }

  private Task<Result<bool>> ValidateLanguageRequirementsAsync(
      PluginManifest manifest,
      CancellationToken cancellationToken)
  {
    var result = manifest.Language switch
    {
      PluginLanguage.CSharp => ValidateCSharpPlugin(manifest),
      PluginLanguage.TypeScript => ValidateTypeScriptPlugin(manifest),
      PluginLanguage.Python => ValidatePythonPlugin(manifest),
      _ => Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.UnsupportedLanguage", $"Language '{manifest.Language}' is not supported."))
    };

    return Task.FromResult(result);
  }

  private Result<bool> ValidateCSharpPlugin(PluginManifest manifest)
  {
    var entryPointPath = manifest.EntryPointPath;
    if (!entryPointPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
      return Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.InvalidCSharpEntryPoint", "C# plugin entry point must be a .cs file."));

    return Result<bool>.Success(true);
  }

  private Result<bool> ValidateTypeScriptPlugin(PluginManifest manifest)
  {
    var entryPointPath = manifest.EntryPointPath;
    if (!entryPointPath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) &&
        !entryPointPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
      return Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.InvalidTypeScriptEntryPoint", "TypeScript plugin entry point must be a .ts or .js file."));

    var packageJsonPath = Path.Combine(manifest.PluginDirectoryPath, "package.json");
    if (!File.Exists(packageJsonPath))
      _logger.LogWarning("TypeScript plugin '{PluginName}' does not have a package.json file", manifest.Name);

    return Result<bool>.Success(true);
  }

  private Result<bool> ValidatePythonPlugin(PluginManifest manifest)
  {
    var entryPointPath = manifest.EntryPointPath;
    if (!entryPointPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
      return Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.InvalidPythonEntryPoint", "Python plugin entry point must be a .py file."));

    return Result<bool>.Success(true);
  }

  private static string GetStringValue(Dictionary<string, object> data, string key, string defaultValue = "")
  {
    return data.TryGetValue(key, out var value) ? value?.ToString() ?? defaultValue : defaultValue;
  }

  private static List<string> GetStringArray(Dictionary<string, object> data, string key)
  {
    if (!data.TryGetValue(key, out var value) || value is not JsonElement element)
      return new List<string>();

    if (element.ValueKind != JsonValueKind.Array)
      return new List<string>();

    return element.EnumerateArray()
        .Where(item => item.ValueKind == JsonValueKind.String)
        .Select(item => item.GetString()!)
        .Where(str => !string.IsNullOrWhiteSpace(str))
        .ToList();
  }

  private static Dictionary<string, object> GetObjectDictionary(Dictionary<string, object> data, string key)
  {
    if (!data.TryGetValue(key, out var value) || value is not JsonElement element)
      return new Dictionary<string, object>();

    if (element.ValueKind != JsonValueKind.Object)
      return new Dictionary<string, object>();

    var result = new Dictionary<string, object>();
    foreach (var property in element.EnumerateObject())
    {
      result[property.Name] = ConvertJsonElement(property.Value);
    }

    return result;
  }

  private static object ConvertJsonElement(JsonElement element)
  {
    return element.ValueKind switch
    {
      JsonValueKind.String => element.GetString()!,
      JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
      JsonValueKind.True => true,
      JsonValueKind.False => false,
      JsonValueKind.Null => null!,
      JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
      JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
      _ => element.ToString()
    };
  }

  private static bool IsReservedKey(string key)
  {
    var reservedKeys = new[] { "name", "version", "description", "language", "entryPoint", "capabilities", "dependencies", "configuration" };
    return reservedKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
  }
}