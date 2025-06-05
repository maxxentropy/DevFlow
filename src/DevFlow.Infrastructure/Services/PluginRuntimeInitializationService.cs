// File: src/DevFlow.Infrastructure/Services/PluginRuntimeInitializationService.cs

using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection; // Add this using
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class PluginRuntimeInitializationService : IHostedService
{
  // Remove IPluginRuntimeManager from here
  private readonly IServiceProvider _serviceProvider; // Inject IServiceProvider
  private readonly IOptions<DevFlowOptions> _options;
  private readonly ILogger<PluginRuntimeInitializationService> _logger;

  public PluginRuntimeInitializationService(
      IServiceProvider serviceProvider, // Changed parameter
      IOptions<DevFlowOptions> options,
      ILogger<PluginRuntimeInitializationService> logger)
  {
    _serviceProvider = serviceProvider; // Store IServiceProvider
    _options = options;
    _logger = logger;
    // _runtimeManager, _discoveryService, _pluginRepository will be resolved from the scope
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Initializing plugin runtime system");

    // Create a new scope to resolve scoped services
    using (var scope = _serviceProvider.CreateScope())
    {
      // Resolve scoped services from this new scope
      var runtimeManager = scope.ServiceProvider.GetRequiredService<IPluginRuntimeManager>();
      var discoveryService = scope.ServiceProvider.GetRequiredService<IPluginDiscoveryService>();
      var pluginRepository = scope.ServiceProvider.GetRequiredService<IPluginRepository>();

      try
      {
        await ValidatePluginDirectoriesAsync(cancellationToken); // This method seems okay if it only uses _options and _logger or is static

        await runtimeManager.InitializeAsync(cancellationToken);
        _logger.LogDebug("Plugin runtime managers initialized");

        // Pass the scoped services to methods that need them
        await DiscoverAndRegisterPluginsAsync(discoveryService, pluginRepository, runtimeManager, cancellationToken);

        _logger.LogInformation("Plugin runtime system initialized successfully");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to initialize plugin runtime system");
        throw; // Rethrow to ensure the application doesn't start in a broken state
      }
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Shutting down plugin runtime system");
    // Create a scope for shutdown tasks if IPluginRuntimeManager is needed here too
    using (var scope = _serviceProvider.CreateScope())
    {
      var runtimeManager = scope.ServiceProvider.GetRequiredService<IPluginRuntimeManager>();
      try
      {
        await runtimeManager.DisposeAsync(cancellationToken);
        _logger.LogInformation("Plugin runtime system shut down successfully");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during plugin runtime system shutdown");
        // Don't rethrow during shutdown
      }
    }
  }

  // Modify DiscoverAndRegisterPluginsAsync and its callees to accept the resolved services as parameters
  private async Task DiscoverAndRegisterPluginsAsync(
      IPluginDiscoveryService discoveryService,
      IPluginRepository pluginRepository,
      IPluginRuntimeManager runtimeManager, // Add runtimeManager here
      CancellationToken cancellationToken)
  {
    var pluginDirectories = _options.Value.Plugins.GetResolvedPluginDirectories().ToList(); // Ensure GetResolvedPluginDirectories is accessible or adjust
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
        // Pass the scoped services down
        await ProcessPluginDirectoryAsync(directory, discoveryService, pluginRepository, runtimeManager, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to process plugin directory: {Directory}", directory);
      }
    }
  }

  private async Task ProcessPluginDirectoryAsync(
      string directory,
      IPluginDiscoveryService discoveryService,
      IPluginRepository pluginRepository,
      IPluginRuntimeManager runtimeManager, // Add runtimeManager
      CancellationToken cancellationToken)
  {
    _logger.LogDebug("Processing plugin directory: {Directory}", directory);

    var manifestsResult = await discoveryService.DiscoverPluginsAsync(directory, cancellationToken);
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
        // Pass the scoped services down
        await ProcessPluginManifestAsync(manifest, discoveryService, pluginRepository, runtimeManager, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to process plugin manifest: {PluginName}", manifest.Name);
      }
    }
  }

  private async Task ProcessPluginManifestAsync(
     PluginManifest manifest,
     IPluginDiscoveryService discoveryService,
     IPluginRepository pluginRepository,
     IPluginRuntimeManager runtimeManager, // Add runtimeManager
     CancellationToken cancellationToken)
  {
    _logger.LogDebug("Processing plugin: {PluginName} v{Version}", manifest.Name, manifest.Version);

    var pluginResult = await discoveryService.LoadPluginAsync(manifest, cancellationToken);
    if (pluginResult.IsFailure)
    {
      _logger.LogWarning("Failed to load plugin {PluginName}: {Error}",
          manifest.Name, pluginResult.Error.Message);
      return;
    }

    var plugin = pluginResult.Value;

    var exists = await pluginRepository.ExistsAsync(
        plugin.Metadata.Name,
        plugin.Metadata.Version.ToString(),
        cancellationToken);

    if (exists)
    {
      _logger.LogDebug("Plugin {PluginName} v{Version} already registered, skipping",
          plugin.Metadata.Name, plugin.Metadata.Version);
      return;
    }
    // Pass runtimeManager to ValidateAndRegisterPluginAsync
    await ValidateAndRegisterPluginAsync(plugin, pluginRepository, runtimeManager, cancellationToken);
  }

  private async Task ValidateAndRegisterPluginAsync(
      Plugin plugin,
      IPluginRepository pluginRepository,
      IPluginRuntimeManager runtimeManager, // Add runtimeManager here
      CancellationToken cancellationToken)
  {
    _logger.LogDebug("Validating plugin: {PluginName}", plugin.Metadata.Name);

    // Use the passed runtimeManager
    var validationResult = await runtimeManager.ValidatePluginAsync(plugin, cancellationToken);

    if (validationResult.IsFailure || !validationResult.Value)
    {
      var errorMessage = validationResult.Error?.Message ?? "Validation failed";
      _logger.LogWarning("Plugin validation failed for {PluginName}: {Error}",
          plugin.Metadata.Name, errorMessage);
      plugin.Validate(false, errorMessage);
    }
    else
    {
      _logger.LogDebug("Plugin validation successful: {PluginName}", plugin.Metadata.Name);
      plugin.Validate(true);
    }

    try
    {
      await pluginRepository.AddAsync(plugin, cancellationToken);
      await pluginRepository.SaveChangesAsync(cancellationToken);

      _logger.LogInformation("Successfully registered plugin: {PluginName} v{Version} ({Language}) - Status: {Status}",
          plugin.Metadata.Name,
          plugin.Metadata.Version,
          plugin.Metadata.Language,
          plugin.Status);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to register plugin {PluginName} in repository", plugin.Metadata.Name);
      throw; // Rethrow to indicate a problem during startup
    }
  }

  // ValidatePluginDirectoriesAsync and other helper methods that don't use scoped services
  // might not need changes if they only use _options or _logger.
  // Make sure GetResolvedPluginDirectories is accessible (e.g., make it static or pass options)
  private async Task ValidatePluginDirectoriesAsync(CancellationToken cancellationToken)
  {
    var options = _options.Value.Plugins;

    ValidatePluginOptions(options); // Ensure this is accessible

    var resolvedDirectories = GetResolvedPluginDirectories(options).ToList(); // Ensure this is accessible

    _logger.LogInformation("Validating {Count} plugin directories", resolvedDirectories.Count);

    foreach (var directory in resolvedDirectories)
    {
      if (!Directory.Exists(directory))
      {
        _logger.LogInformation("Plugin directory does not exist, creating: {Directory}", directory);
        try
        {
          Directory.CreateDirectory(directory);
          var languageDirectories = new[] { "csharp", "typescript", "python", "_templates", "samples" };
          foreach (var langDir in languageDirectories)
          {
            var fullPath = Path.Combine(directory, langDir);
            Directory.CreateDirectory(fullPath);
            _logger.LogDebug("Created subdirectory: {Directory}", fullPath);
          }
          var readmePath = Path.Combine(directory, "README.md");
          if (!File.Exists(readmePath))
          {
            await File.WriteAllTextAsync(readmePath, GetPluginDirectoryReadme(), cancellationToken); // Ensure this is accessible
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

  private static IEnumerable<string> GetResolvedPluginDirectories(PluginOptions options)
  {
    return options.PluginDirectories
      .Select(dir => Environment.ExpandEnvironmentVariables(dir))
      .Select(dir => Path.IsPathRooted(dir) ? dir : Path.GetFullPath(dir));
  }

  private static string GetPluginDirectoryReadme()
  {
    // Content from your original file
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