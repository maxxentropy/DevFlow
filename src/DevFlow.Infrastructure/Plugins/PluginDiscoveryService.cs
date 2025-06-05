using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums; // Make sure this using is present for PluginLanguage
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // << ??????? LINQ is imported for .Contains() extension method on IEnumerable<T>
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DevFlow.Infrastructure.Plugins;

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
      var manifestFiles = Directory.GetFiles(pluginDirectoryPath, PluginManifestFileName, SearchOption.AllDirectories);

      _logger.LogDebug("Found {Count} potential plugin manifest files in {PluginDirectoryPath}.", manifestFiles.Length, pluginDirectoryPath);

      foreach (var manifestFile in manifestFiles)
      {
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.GetDirectoryName(manifestFile) ?? pluginDirectoryPath;

        var manifestResult = await ScanPluginDirectoryAsync(directory, cancellationToken);
        if (manifestResult.IsSuccess)
        {
          manifests.Add(manifestResult.Value);
          _logger.LogDebug("Successfully loaded plugin manifest: {PluginName} v{Version} from {ManifestPath}",
              manifestResult.Value.Name, manifestResult.Value.Version, manifestFile);
        }
        else
        {
          _logger.LogWarning("Failed to load plugin from directory {Directory} (manifest: {ManifestPath}): {Error}",
              directory, manifestFile, manifestResult.Error.Message);
        }
      }

      _logger.LogInformation("Discovered {Count} valid plugins from {TotalManifestsFound} manifest files scanned in {PluginDirectoryPath}.",
          manifests.Count, manifestFiles.Length, pluginDirectoryPath);

      return Result<IReadOnlyList<PluginManifest>>.Success(manifests.AsReadOnly());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to discover plugins in directory: {PluginDirectory}", pluginDirectoryPath);
      return Result<IReadOnlyList<PluginManifest>>.Failure(Error.Failure(
          "PluginDiscovery.DiscoveryFailed", $"Plugin discovery failed in '{pluginDirectoryPath}': {ex.Message}"));
    }
  }

  public async Task<Result<IReadOnlyList<PluginManifest>>> DiscoverPluginsAsync(
      IEnumerable<string> pluginDirectoryPaths,
      CancellationToken cancellationToken = default)
  {
    var directoryPaths = pluginDirectoryPaths?.ToList();
    if (directoryPaths is null || !directoryPaths.Any())
      return Result<IReadOnlyList<PluginManifest>>.Success(Array.Empty<PluginManifest>());

    _logger.LogInformation("Discovering plugins in {Count} specified root directories.", directoryPaths.Count);

    var allManifests = new List<PluginManifest>();
    var errors = new List<Error>();

    foreach (var path in directoryPaths)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var result = await DiscoverPluginsAsync(path, cancellationToken);
      if (result.IsSuccess)
        allManifests.AddRange(result.Value);
      else
        errors.Add(result.Error);
    }

    if (errors.Any() && !allManifests.Any())
    {
      var combinedError = Error.Failure(
          "PluginDiscovery.AllDirectoriesFailed",
          $"Failed to discover plugins in all specified directories. Errors: {string.Join("; ", errors.Select(e => $"{e.Code}: {e.Message}"))}");
      return Result<IReadOnlyList<PluginManifest>>.Failure(combinedError);
    }

    if (errors.Any())
      _logger.LogWarning("Some directories failed during discovery: {ErrorCount} errors encountered. Details: {Errors}",
          errors.Count, string.Join("; ", errors.Select(e => $"{e.Code}: {e.Message}")));

    _logger.LogInformation("Discovered {Count} total plugins from {DirectoryCount} root directories specified.",
        allManifests.Count, directoryPaths.Count);

    return Result<IReadOnlyList<PluginManifest>>.Success(allManifests.AsReadOnly());
  }

  public Task<Result<Plugin>> LoadPluginAsync(
    PluginManifest manifest,
    CancellationToken cancellationToken = default)
  {
    if (manifest is null)
      return Task.FromResult(Result<Plugin>.Failure(Error.Validation(
          "PluginDiscovery.ManifestNull", "Plugin manifest cannot be null.")));

    _logger.LogDebug("Loading plugin from manifest: {PluginName} v{Version}", manifest.Name, manifest.Version);

    try
    {
      var pluginCreateResult = Plugin.Create(
          manifest.Name,
          manifest.Version,
          manifest.Description,
          manifest.Language,
          manifest.EntryPoint,
          manifest.PluginDirectoryPath,
          manifest.Capabilities.ToList(),
          manifest.Configuration.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
      );

      if (pluginCreateResult.IsFailure)
      {
        _logger.LogWarning("Failed to create plugin entity for {PluginName}: {Error}", manifest.Name, pluginCreateResult.Error.Message);
        return Task.FromResult(pluginCreateResult);
      }

      var plugin = pluginCreateResult.Value;

      if (manifest.Dependencies.Any())
      {
        _logger.LogDebug("Processing {Count} dependencies for plugin: {PluginName}",
            manifest.Dependencies.Count, manifest.Name);

        foreach (var dependencyString in manifest.Dependencies)
        {
          var dependencyResult = ParseDependencyString(dependencyString);
          if (dependencyResult.IsSuccess)
          {
            plugin.AddDependency(dependencyResult.Value);
            _logger.LogTrace("Added dependency to {PluginName}: {DependencyType} {DependencyName} v{DependencyVersion}",
                manifest.Name, dependencyResult.Value.Type, dependencyResult.Value.Name, dependencyResult.Value.Version);
          }
          else
          {
            _logger.LogWarning("Failed to parse dependency '{DependencyString}' for plugin '{PluginName}': {Error}",
                dependencyString, manifest.Name, dependencyResult.Error.Message);
          }
        }
      }

      _logger.LogDebug("Successfully loaded plugin entity: {PluginName} v{Version} with {DependencyCount} dependencies specified in manifest.",
          manifest.Name, manifest.Version, plugin.Dependencies.Count);

      return Task.FromResult(Result<Plugin>.Success(plugin));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to load plugin from manifest: {PluginName}", manifest.Name);
      return Task.FromResult(Result<Plugin>.Failure(Error.Failure(
          "PluginDiscovery.LoadFailed", $"Failed to load plugin '{manifest.Name}': {ex.Message}")));
    }
  }

  // Method within: src/DevFlow.Infrastructure/Plugins/PluginDiscoveryService.cs
  private static Result<PluginDependency> ParseDependencyString(string dependencyString)
  {
    if (string.IsNullOrWhiteSpace(dependencyString))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDiscovery.EmptyDependency", "Dependency string cannot be empty."));

    var parts = dependencyString.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2)
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDiscovery.InvalidTypeFormat",
          $"Invalid dependency format: '{dependencyString}'. Expected 'type:nameAndVersion'. Example: 'nuget:Newtonsoft.Json@^13.0' or 'pip:requests>=2.0'."));

    var typeString = parts[0].Trim().ToLowerInvariant();
    var nameAndVersionString = parts[1].Trim();

    string name;
    string versionSpecifier;

    PluginDependencyType? dependencyType = typeString switch
    {
      "nuget" => PluginDependencyType.NuGetPackage,
      "plugin" => PluginDependencyType.Plugin,
      "file" => PluginDependencyType.FileReference,
      "npm" => PluginDependencyType.NpmPackage,
      "pip" => PluginDependencyType.PipPackage,
      _ => null
    };

    if (!dependencyType.HasValue)
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDiscovery.UnsupportedDependencyType",
          $"Unsupported dependency type: '{typeString}'. Supported types: nuget, plugin, file, npm, pip."));

    switch (dependencyType.Value)
    {
      case PluginDependencyType.PipPackage:
        // Regex:
        // Group 1: Package name, including optional extras like [security]
        //          Allows: word characters, '.', '-' for the name,
        //                  word characters, spaces, ',', '-' inside extras [...]
        // Group 2: (Optional) The rest of the string, which is the version specifier.
        //          This can start with typical pip operators (>=, ==, ~=) or SemVer ones (^, ~)
        //          or just be a version number.
        var pipMatch = Regex.Match(nameAndVersionString, @"^([\w.-]+(?:\[[\w\s,-]+\])?)\s*(.*)$");

        if (pipMatch.Success && !string.IsNullOrWhiteSpace(pipMatch.Groups[1].Value))
        {
          name = pipMatch.Groups[1].Value.Trim();
          versionSpecifier = pipMatch.Groups[2].Value.Trim(); // This captures everything after the name and optional space

          if (string.IsNullOrEmpty(versionSpecifier))
          {
            versionSpecifier = "*"; // Default if only package name is provided
          }
        }
        else
        {
          // This path should ideally not be hit if nameAndVersionString is a valid pip dependency string.
          // This indicates a failure to even extract a basic package name.
          return Result<PluginDependency>.Failure(Error.Validation(
             "PluginDiscovery.InvalidPipFormat",
             $"Invalid pip dependency format for '{nameAndVersionString}'. Could not extract a valid package name. Expected formats like 'requests>=2.0', 'package[extra]^1.0.0', or 'package'."));
        }
        break;

      case PluginDependencyType.NpmPackage:
        int lastAtIndex = nameAndVersionString.LastIndexOf('@');
        if (lastAtIndex > 0 && lastAtIndex < nameAndVersionString.Length - 1)
        {
          name = nameAndVersionString.Substring(0, lastAtIndex).Trim();
          versionSpecifier = nameAndVersionString.Substring(lastAtIndex + 1).Trim();
        }
        else if (lastAtIndex == -1 || (lastAtIndex == 0 && nameAndVersionString.Length > 1 && nameAndVersionString[0] == '@'))
        {
          name = nameAndVersionString.Trim();
          versionSpecifier = "*";
        }
        else
        {
          return Result<PluginDependency>.Failure(Error.Validation(
              "PluginDiscovery.InvalidNpmVersionFormat",
              $"Invalid npm dependency format '{nameAndVersionString}'. Could not reliably extract package name and version specifier. Expected 'package@version' or '@scope/package@version'."));
        }
        break;

      case PluginDependencyType.NuGetPackage:
      case PluginDependencyType.Plugin:
      case PluginDependencyType.FileReference:
      default:
        var nameVersionParts = nameAndVersionString.Split(new[] { '@' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (nameVersionParts.Length == 2)
        {
          name = nameVersionParts[0].Trim();
          versionSpecifier = nameVersionParts[1].Trim();
        }
        else if (nameVersionParts.Length == 1 && dependencyType == PluginDependencyType.FileReference)
        {
          name = nameVersionParts[0].Trim();
          versionSpecifier = "*";
        }
        else
        {
          return Result<PluginDependency>.Failure(Error.Validation(
              "PluginDiscovery.InvalidNameVersionFormat",
              $"Invalid name@version format for '{nameAndVersionString}' (type: {typeString})."));
        }
        break;
    }

    if (string.IsNullOrWhiteSpace(name))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDiscovery.NameEmpty", $"Dependency name cannot be empty for '{dependencyString}'."));
    if (string.IsNullOrWhiteSpace(versionSpecifier))
      return Result<PluginDependency>.Failure(Error.Validation(
          "PluginDiscovery.VersionEmpty", $"Dependency version specifier cannot be empty for '{dependencyString}'."));

    Result<PluginDependency> dependencyResult;
    switch (dependencyType.Value)
    {
      case PluginDependencyType.NuGetPackage:
        dependencyResult = PluginDependency.CreateNuGetPackage(name, versionSpecifier);
        break;
      case PluginDependencyType.Plugin:
        dependencyResult = PluginDependency.CreatePluginDependency(name, versionSpecifier);
        break;
      case PluginDependencyType.FileReference:
        dependencyResult = PluginDependency.CreateFileReference(name, versionSpecifier, name);
        break;
      case PluginDependencyType.NpmPackage:
        dependencyResult = PluginDependency.CreateNpmPackage(name, versionSpecifier);
        break;
      case PluginDependencyType.PipPackage:
        dependencyResult = PluginDependency.CreatePipPackage(name, versionSpecifier);
        break;
      default:
        return Result<PluginDependency>.Failure(Error.Unexpected("PluginDiscovery.UnknownTypeReached", "Reached unknown dependency type after validation."));
    }

    if (dependencyResult.IsFailure)
    {
      return Result<PluginDependency>.Failure(Error.Validation(
          dependencyResult.Error.Code,
          $"Failed to create dependency object for '{dependencyString}': {dependencyResult.Error.Message}"
      ));
    }
    return dependencyResult;
  }

  private static object? ConvertJsonElement(JsonElement element)
  {
    return element.ValueKind switch
    {
      JsonValueKind.Object => element.EnumerateObject()
                        .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
      JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
      JsonValueKind.String => element.GetString(),
      JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
      JsonValueKind.True => true,
      JsonValueKind.False => false,
      JsonValueKind.Null => null,
      JsonValueKind.Undefined => null,
      _ => throw new ArgumentOutOfRangeException(nameof(element.ValueKind), $"Unsupported JsonValueKind: {element.ValueKind}")
    };
  }

  private Result<PluginManifest> ParseManifestData(
      Dictionary<string, object?> manifestDataObject,
      string pluginDirectoryPath,
      string manifestPath)
  {
    try
    {
      string GetDictString(string key, string defaultVal = "") =>
          manifestDataObject.TryGetValue(key, out var val) && val is string strVal ? strVal : defaultVal;

      List<string> GetDictStringList(string key) =>
          manifestDataObject.TryGetValue(key, out var val) && val is List<object?> objList
          ? objList.Select(o => o?.ToString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
          : new List<string>();

      Dictionary<string, object> GetDictObjectDictionary(string key) =>
          manifestDataObject.TryGetValue(key, out var val) && val is Dictionary<string, object?> dictVal
          ? dictVal.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value!)
          : new Dictionary<string, object>();

      var name = GetDictString("name");
      var version = GetDictString("version");
      var description = GetDictString("description", string.Empty);
      var languageStr = GetDictString("language");
      var entryPoint = GetDictString("entryPoint");

      if (string.IsNullOrWhiteSpace(name)) return Result<PluginManifest>.Failure(Error.Validation("Manifest.NameMissing", "'name' is required in plugin.json."));
      if (string.IsNullOrWhiteSpace(version)) return Result<PluginManifest>.Failure(Error.Validation("Manifest.VersionMissing", "'version' is required in plugin.json."));
      if (string.IsNullOrWhiteSpace(languageStr)) return Result<PluginManifest>.Failure(Error.Validation("Manifest.LanguageMissing", "'language' is required in plugin.json."));
      if (string.IsNullOrWhiteSpace(entryPoint)) return Result<PluginManifest>.Failure(Error.Validation("Manifest.EntryPointMissing", "'entryPoint' is required in plugin.json."));

      if (!Enum.TryParse<PluginLanguage>(languageStr, true, out var language))
        return Result<PluginManifest>.Failure(Error.Validation(
            "PluginDiscovery.InvalidLanguage", $"Invalid or unsupported language: '{languageStr}'."));

      var capabilities = GetDictStringList("capabilities");
      var dependencies = GetDictStringList("dependencies");
      var configuration = GetDictObjectDictionary("configuration");

      var metadata = manifestDataObject
          .Where(kvp => !IsReservedKey(kvp.Key) && kvp.Value != null)
          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

      return PluginManifest.Create(
          name, version, description, language, entryPoint,
          pluginDirectoryPath, manifestPath, capabilities, dependencies,
          configuration, metadata, File.GetLastWriteTimeUtc(manifestPath));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error parsing manifest data from {ManifestPath}", manifestPath);
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.ManifestDataParseError", $"Failed to parse manifest data from '{manifestPath}': {ex.Message}"));
    }
  }

  public async Task<Result<PluginManifest>> ScanPluginDirectoryAsync(
      string pluginDirectoryPath,
      CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(pluginDirectoryPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.ScanDir.PathEmpty", "Plugin directory path cannot be empty."));

    if (!Directory.Exists(pluginDirectoryPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.ScanDir.NotFound", $"Plugin directory '{pluginDirectoryPath}' does not exist."));

    var manifestPath = Path.Combine(pluginDirectoryPath, PluginManifestFileName);
    if (!File.Exists(manifestPath))
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.ScanDir.ManifestMissing", $"Plugin manifest file ('{PluginManifestFileName}') not found at '{pluginDirectoryPath}'."));

    try
    {
      var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
      var manifestJsonElements = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(manifestJson, _jsonOptions);

      if (manifestJsonElements is null)
        return Result<PluginManifest>.Failure(Error.Validation(
            "PluginDiscovery.ScanDir.InvalidFormat", $"Plugin manifest file '{manifestPath}' contains invalid JSON or is empty."));

      var manifestDataObject = manifestJsonElements.ToDictionary(
          kvp => kvp.Key,
          kvp => ConvertJsonElement(kvp.Value),
          StringComparer.OrdinalIgnoreCase
      );

      return ParseManifestData(manifestDataObject, pluginDirectoryPath, manifestPath);
    }
    catch (JsonException ex)
    {
      _logger.LogWarning(ex, "Invalid JSON in plugin manifest: {ManifestPath}", manifestPath);
      return Result<PluginManifest>.Failure(Error.Validation(
          "PluginDiscovery.ScanDir.InvalidJson", $"Invalid JSON in manifest file '{manifestPath}': {ex.Message}"));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to scan plugin directory: {PluginDirectory}", pluginDirectoryPath);
      return Result<PluginManifest>.Failure(Error.Failure(
          "PluginDiscovery.ScanDir.ScanFailed", $"Failed to scan plugin directory '{pluginDirectoryPath}': {ex.Message}"));
    }
  }

  // CORRECTED METHOD
  private static bool IsReservedKey(string key)
  {
    var reservedKeys = new[] { "name", "version", "description", "language", "entryPoint", "capabilities", "dependencies", "configuration" };
    // Use LINQ Contains with a StringComparer for case-insensitive check
    return reservedKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
  }

  public async Task<Result<bool>> ValidateManifestAsync(
      PluginManifest manifest,
      CancellationToken cancellationToken = default)
  {
    if (manifest is null)
      return await Task.FromResult(Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.ManifestNull", "Plugin manifest cannot be null.")));

    return await Task.Run(() =>
    {
      try
      {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.Name)) errors.Add("Manifest 'name' is missing or empty.");
        if (string.IsNullOrWhiteSpace(manifest.Version)) errors.Add("Manifest 'version' is missing or empty.");
        if (string.IsNullOrWhiteSpace(manifest.EntryPoint)) errors.Add("Manifest 'entryPoint' is missing or empty.");
        else if (!File.Exists(manifest.EntryPointPath)) errors.Add($"Entry point file '{manifest.EntryPointPath}' does not exist.");

        var langValidationResult = ValidateLanguageRequirements(manifest);
        if (langValidationResult.IsFailure) errors.Add(langValidationResult.Error.Message);

        if (manifest.Dependencies != null)
        {
          foreach (var depString in manifest.Dependencies)
          {
            if (string.IsNullOrWhiteSpace(depString))
            {
              errors.Add("An empty or whitespace dependency string was found.");
              continue;
            }
            var parsedDep = ParseDependencyString(depString);
            if (parsedDep.IsFailure) errors.Add($"Invalid dependency '{depString}': {parsedDep.Error.Message}");
          }
        }

        if (errors.Any())
        {
          string combinedErrors = string.Join("; ", errors);
          _logger.LogWarning("Plugin manifest validation failed for {PluginName}: {Errors}", manifest.Name, combinedErrors);
          return Result<bool>.Failure(Error.Validation("PluginDiscovery.InvalidManifest", combinedErrors));
        }

        return Result<bool>.Success(true);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception during plugin manifest validation: {PluginName}", manifest.Name);
        return Result<bool>.Failure(Error.Failure(
            "PluginDiscovery.ManifestValidationException", $"Manifest validation failed with exception: {ex.Message}"));
      }
    }, cancellationToken);
  }
  private Result<bool> ValidateLanguageRequirements(PluginManifest manifest)
  {
    return manifest.Language switch
    {
      PluginLanguage.CSharp => ValidateCSharpPlugin(manifest),
      PluginLanguage.TypeScript => ValidateTypeScriptPlugin(manifest),
      PluginLanguage.Python => ValidatePythonPlugin(manifest),
      _ => Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.UnsupportedLanguage", $"Language '{manifest.Language}' is not supported by manifest validation."))
    };
  }
  private Result<bool> ValidateCSharpPlugin(PluginManifest manifest)
  {
    if (!manifest.EntryPoint.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
      return Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.InvalidCSharpEntryPoint", "C# plugin entry point must be a .cs file."));
    return Result<bool>.Success(true);
  }

  private Result<bool> ValidateTypeScriptPlugin(PluginManifest manifest)
  {
    if (!manifest.EntryPoint.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) &&
        !manifest.EntryPoint.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
      return Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.InvalidTypeScriptEntryPoint", "TypeScript plugin entry point must be a .ts or .js file."));
    return Result<bool>.Success(true);
  }

  private Result<bool> ValidatePythonPlugin(PluginManifest manifest)
  {
    if (!manifest.EntryPoint.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
      return Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.InvalidPythonEntryPoint", "Python plugin entry point must be a .py file."));
    return Result<bool>.Success(true);
  }

  public Task<Result<bool>> IsPluginModifiedAsync(
    string pluginDirectoryPath,
    DateTimeOffset lastScanTime,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(pluginDirectoryPath))
      return Task.FromResult(Result<bool>.Failure(Error.Validation(
          "PluginDiscovery.DirEmptyModCheck", "Plugin directory path cannot be empty for modification check.")));

    if (!Directory.Exists(pluginDirectoryPath))
      return Task.FromResult(Result<bool>.Success(true));

    try
    {
      var manifestPath = Path.Combine(pluginDirectoryPath, PluginManifestFileName);
      if (!File.Exists(manifestPath))
        return Task.FromResult(Result<bool>.Success(true));

      var lastWriteTimeManifest = File.GetLastWriteTimeUtc(manifestPath);
      if (lastWriteTimeManifest > lastScanTime.UtcDateTime)
        return Task.FromResult(Result<bool>.Success(true));

      var entryPointName = GetEntryPointFromManifest(manifestPath); // Helper method from previous response
      if (!string.IsNullOrWhiteSpace(entryPointName))
      {
        var entryPointPath = Path.Combine(pluginDirectoryPath, entryPointName);
        if (File.Exists(entryPointPath) && File.GetLastWriteTimeUtc(entryPointPath) > lastScanTime.UtcDateTime)
          return Task.FromResult(Result<bool>.Success(true));
      }

      // More comprehensive check: any file modification in the directory.
      // This can be slow for large directories. Consider if this level of detail is needed.
      var files = Directory.GetFiles(pluginDirectoryPath, "*.*", SearchOption.AllDirectories);
      foreach (var file in files)
      {
        if (File.GetLastWriteTimeUtc(file) > lastScanTime.UtcDateTime)
          return Task.FromResult(Result<bool>.Success(true));
      }

      return Task.FromResult(Result<bool>.Success(false));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to check plugin modification time: {PluginDirectory}", pluginDirectoryPath);
      return Task.FromResult(Result<bool>.Failure(Error.Failure(
          "PluginDiscovery.ModCheckFailed", $"Failed to check modification time for '{pluginDirectoryPath}': {ex.Message}")));
    }
  }

  private string? GetEntryPointFromManifest(string manifestPath)
  {
    try
    {
      if (!File.Exists(manifestPath)) return null;
      var manifestJson = File.ReadAllText(manifestPath);
      // Using JsonDocument for a more direct and potentially safer parsing of a single property
      using (JsonDocument doc = JsonDocument.Parse(manifestJson))
      {
        if (doc.RootElement.TryGetProperty("entryPoint", out JsonElement entryPointElement) &&
            entryPointElement.ValueKind == JsonValueKind.String)
        {
          return entryPointElement.GetString();
        }
      }
    }
    catch (JsonException jEx)
    {
      _logger.LogWarning(jEx, "JSON parsing error while trying to get entryPoint from manifest {ManifestPath} for modification check.", manifestPath);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Could not read entryPoint from manifest {ManifestPath} for modification check.", manifestPath);
    }
    return null;
  }
}