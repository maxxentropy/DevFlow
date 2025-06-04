
using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevFlow.Infrastructure.Services;

/// <summary>
/// Hosted service responsible for initializing the plugin runtime system on application startup.
/// Discovers plugins, validates them, and registers them with the system.
/// </summary>
public sealed class PluginRuntimeInitializationService : IHostedService
{
  private readonly IPluginRuntimeManager _runtimeManager;
  private readonly IPluginDiscoveryService _discoveryService;
  private readonly IPluginRepository _pluginRepository;
  private readonly IOptions<DevFlowOptions> _options;
  private readonly ILogger<PluginRuntimeInitializationService> _logger;

  public PluginRuntimeInitializationService(
      IPluginRuntimeManager runtimeManager,
      IPluginDiscoveryService discoveryService,
      IPluginRepository pluginRepository,
      IOptions<DevFlowOptions> options,
      ILogger<PluginRuntimeInitializationService> logger)
  {
    _runtimeManager = runtimeManager;
    _discoveryService = discoveryService;
    _pluginRepository = pluginRepository;
    _options = options;
    _logger = logger;
  }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing plugin runtime system");

        try
        {
            // 🆕 ADD THIS CALL FIRST
            await ValidatePluginDirectoriesAsync(cancellationToken);

            // Existing code continues...
            await _runtimeManager.InitializeAsync(cancellationToken);
            _logger.LogDebug("Plugin runtime managers initialized");

            await DiscoverAndRegisterPluginsAsync(cancellationToken);

            _logger.LogInformation("Plugin runtime system initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize plugin runtime system");
            throw;
        }
    }
    public async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Shutting down plugin runtime system");

    try
    {
      await _runtimeManager.DisposeAsync(cancellationToken);
      _logger.LogInformation("Plugin runtime system shut down successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during plugin runtime system shutdown");
      // Don't rethrow during shutdown
    }
  }

  private async Task DiscoverAndRegisterPluginsAsync(CancellationToken cancellationToken)
  {
    var pluginDirectories = _options.Value.Plugins.PluginDirectories;
    _logger.LogDebug("Scanning {Count} plugin directories", pluginDirectories.Count);

    foreach (var directory in pluginDirectories)
    {
      if (!Directory.Exists(directory))
      {
        _logger.LogWarning("Plugin directory does not exist: {Directory}", directory);
        continue;
      }

      try
      {
        await ProcessPluginDirectoryAsync(directory, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to process plugin directory: {Directory}", directory);
        // Continue with other directories
      }
    }
  }

  private async Task ProcessPluginDirectoryAsync(string directory, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Processing plugin directory: {Directory}", directory);

    var manifestsResult = await _discoveryService.DiscoverPluginsAsync(directory, cancellationToken);
    if (manifestsResult.IsFailure)
    {
      _logger.LogWarning("Failed to discover plugins in {Directory}: {Error}",
          directory, manifestsResult.Error.Message);
      return;
    }

    var manifests = manifestsResult.Value;
    _logger.LogDebug("Found {Count} plugin manifests in {Directory}", manifests.Count, directory);

    foreach (var manifest in manifests)
    {
      try
      {
        await ProcessPluginManifestAsync(manifest, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to process plugin manifest: {PluginName}", manifest.Name);
        // Continue with other plugins
      }
    }
  }

  private async Task ProcessPluginManifestAsync(PluginManifest manifest, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Processing plugin: {PluginName} v{Version}", manifest.Name, manifest.Version);

    // Load plugin from manifest
    var pluginResult = await _discoveryService.LoadPluginAsync(manifest, cancellationToken);
    if (pluginResult.IsFailure)
    {
      _logger.LogWarning("Failed to load plugin {PluginName}: {Error}",
          manifest.Name, pluginResult.Error.Message);
      return;
    }

    var plugin = pluginResult.Value;

    // Check if plugin already exists (avoid duplicates)
    var exists = await _pluginRepository.ExistsAsync(
        plugin.Metadata.Name,
        plugin.Metadata.Version.ToString(),
        cancellationToken);

    if (exists)
    {
      _logger.LogDebug("Plugin {PluginName} v{Version} already registered, skipping",
          plugin.Metadata.Name, plugin.Metadata.Version);
      return;
    }

    // Validate plugin before registration
    await ValidateAndRegisterPluginAsync(plugin, cancellationToken);
  }

  private async Task ValidateAndRegisterPluginAsync(Plugin plugin, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Validating plugin: {PluginName}", plugin.Metadata.Name);

    var validationResult = await _runtimeManager.ValidatePluginAsync(plugin, cancellationToken);

    if (validationResult.IsFailure || !validationResult.Value)
    {
      var errorMessage = validationResult.Error?.Message ?? "Validation failed";
      _logger.LogWarning("Plugin validation failed for {PluginName}: {Error}",
          plugin.Metadata.Name, errorMessage);

      // Mark plugin as having validation errors but still register it
      plugin.Validate(false, errorMessage);
    }
    else
    {
      _logger.LogDebug("Plugin validation successful: {PluginName}", plugin.Metadata.Name);
      plugin.Validate(true);
    }

    // Register plugin in repository
    try
    {
      await _pluginRepository.AddAsync(plugin, cancellationToken);
      await _pluginRepository.SaveChangesAsync(cancellationToken);

      _logger.LogInformation("Successfully registered plugin: {PluginName} v{Version} ({Language}) - Status: {Status}",
          plugin.Metadata.Name,
          plugin.Metadata.Version,
          plugin.Metadata.Language,
          plugin.Status);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to register plugin {PluginName} in repository", plugin.Metadata.Name);
      throw;
    }
  }


  /// <summary>
  /// Validates that plugin directories exist and creates them if necessary.
  /// Also creates language-specific subdirectories and documentation.
  /// </summary>
  private async Task ValidatePluginDirectoriesAsync(CancellationToken cancellationToken)
  {
    var options = _options.Value.Plugins;

    // Validate configuration - add this method to PluginOptions if it doesn't exist
    ValidatePluginOptions(options);

    var resolvedDirectories = GetResolvedPluginDirectories(options).ToList();

    _logger.LogInformation("Validating {Count} plugin directories", resolvedDirectories.Count);

    foreach (var directory in resolvedDirectories)
    {
      if (!Directory.Exists(directory))
      {
        _logger.LogInformation("Plugin directory does not exist, creating: {Directory}", directory);
        try
        {
          Directory.CreateDirectory(directory);

          // Create language-specific subdirectories
          var languageDirectories = new[] { "csharp", "typescript", "python", "_templates", "samples" };
          foreach (var langDir in languageDirectories)
          {
            var fullPath = Path.Combine(directory, langDir);
            Directory.CreateDirectory(fullPath);
            _logger.LogDebug("Created subdirectory: {Directory}", fullPath);
          }

          // Create README if it doesn't exist
          var readmePath = Path.Combine(directory, "README.md");
          if (!File.Exists(readmePath))
          {
            await File.WriteAllTextAsync(readmePath, GetPluginDirectoryReadme(), cancellationToken);
            _logger.LogDebug("Created plugin directory README: {ReadmePath}", readmePath);
          }

          _logger.LogInformation("Successfully created plugin directory structure: {Directory}", directory);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to create plugin directory: {Directory}", directory);
          throw;
        }
      }
      else
      {
        _logger.LogDebug("Plugin directory exists: {Directory}", directory);

        // Verify language subdirectories exist
        var languageDirectories = new[] { "csharp", "typescript", "python" };
        foreach (var langDir in languageDirectories)
        {
          var fullPath = Path.Combine(directory, langDir);
          if (!Directory.Exists(fullPath))
          {
            Directory.CreateDirectory(fullPath);
            _logger.LogDebug("Created missing language subdirectory: {Directory}", fullPath);
          }
        }
      }
    }
  }

  /// <summary>
  /// Validates plugin configuration options.
  /// </summary>
  private static void ValidatePluginOptions(PluginOptions options)
  {
    if (!options.PluginDirectories.Any())
      throw new InvalidOperationException("At least one plugin directory must be specified.");

    if (options.ExecutionTimeoutMs <= 0)
      throw new InvalidOperationException("Plugin execution timeout must be greater than zero.");

    if (options.MaxMemoryMb <= 0)
      throw new InvalidOperationException("Plugin maximum memory must be greater than zero.");

    if (options.EnableHotReload && options.ScanIntervalSeconds <= 0)
      throw new InvalidOperationException("Plugin scan interval must be greater than zero when hot-reload is enabled.");
  }

  /// <summary>
  /// Gets resolved plugin directories with environment variables expanded.
  /// </summary>
  private static IEnumerable<string> GetResolvedPluginDirectories(PluginOptions options)
  {
    return options.PluginDirectories
      .Select(dir => Environment.ExpandEnvironmentVariables(dir))
      .Select(dir => Path.IsPathRooted(dir) ? dir : Path.GetFullPath(dir));
  }

  /// <summary>
  /// Generates the README content for the plugin directory.
  /// </summary>
  private static string GetPluginDirectoryReadme()
  {
    return """
    # DevFlow Plugins Directory
        
    This directory contains plugins that extend DevFlow's automation capabilities.
        
    ## Directory Structure
        
    ```
    plugins/
    ├── csharp/          # C# plugins (.NET 8+)
    ├── typescript/      # TypeScript/JavaScript plugins (Node.js)
    ├── python/          # Python plugins (3.8+)
    ├── _templates/      # Plugin templates for development
    └── samples/         # Sample/reference plugins
    ```
        
    ## Plugin Development
        
    Each plugin must include a `plugin.json` manifest file describing:
    - Plugin metadata (name, version, description)
    - Language and entry point
    - Required capabilities and dependencies
    - Configuration schema
        
    ### Sample plugin.json
        
    ```json
    {
      "name": "MyPlugin",
      "version": "1.0.0",
      "description": "Description of what this plugin does",
      "language": "CSharp",
      "entryPoint": "MyPlugin.cs",
      "capabilities": ["file_read", "file_write"],
      "dependencies": [],
      "configuration": {
        "settingName": "defaultValue"
      }
    }
    ```
        
    ## Security
        
    Plugins run in isolated environments with restricted access to:
    - File system (limited to working directory)
    - Network access (configurable)
    - System resources (memory and CPU limits)
        
    Only install plugins from trusted sources.
        
    ## Getting Started
        
    1. Create a new directory under the appropriate language folder
    2. Add your plugin.json manifest
    3. Implement your plugin following the language-specific conventions
    4. Restart DevFlow to auto-discover your plugin
        
    For detailed examples, see the samples/ directory.
    """;
  }
}