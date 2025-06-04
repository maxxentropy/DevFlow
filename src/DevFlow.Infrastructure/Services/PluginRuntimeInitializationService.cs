
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
      // Initialize runtime managers
      await _runtimeManager.InitializeAsync(cancellationToken);
      _logger.LogDebug("Plugin runtime managers initialized");

      // Discover and register plugins
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
}