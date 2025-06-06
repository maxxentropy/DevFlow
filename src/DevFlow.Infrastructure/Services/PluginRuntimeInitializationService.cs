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
    _logger.LogInformation("Plugin runtime system initialization starting...");

    try
    {
      await ValidatePluginDirectoriesAsync(cancellationToken);

      await _runtimeManager.InitializeAsync(cancellationToken);
      _logger.LogDebug("Composite plugin runtime manager initialized.");

      await DiscoverAndRegisterPluginsAsync(cancellationToken);

      _logger.LogInformation("Plugin runtime system initialization finished successfully.");
    }
    catch (Exception ex)
    {
      _logger.LogCritical(ex, "A critical error occurred while initializing the plugin runtime system.");
      throw;
    }
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Shutting down plugin runtime system.");
    return _runtimeManager.DisposeAsync(cancellationToken);
  }

  private async Task DiscoverAndRegisterPluginsAsync(CancellationToken cancellationToken)
  {
    var pluginDirectories = _options.Value.Plugins.GetResolvedPluginDirectories().ToList();
    if (!pluginDirectories.Any())
    {
      _logger.LogWarning("No plugin directories are configured in appsettings.json.");
      return;
    }

    _logger.LogInformation("Scanning for plugins in {Count} configured directories...", pluginDirectories.Count);

    var discoveryResult = await _discoveryService.DiscoverPluginsAsync(pluginDirectories, cancellationToken);

    if (discoveryResult.IsFailure)
    {
      _logger.LogError("Plugin discovery failed. Error: {Error}", discoveryResult.Error.Message);
      return;
    }

    var manifests = discoveryResult.Value;
    _logger.LogInformation("Found {Count} total plugin manifests. Proceeding with loading and validation...", manifests.Count);

    foreach (var manifest in manifests)
    {
      cancellationToken.ThrowIfCancellationRequested();
      try
      {
        await ProcessPluginManifestAsync(manifest, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An unexpected error occurred while processing plugin manifest for '{PluginName}'.", manifest.Name);
      }
    }
  }

  private async Task ProcessPluginManifestAsync(PluginManifest manifest, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Processing plugin: {PluginName} v{Version}", manifest.Name, manifest.Version);

    var pluginResult = await _discoveryService.LoadPluginAsync(manifest, cancellationToken);
    if (pluginResult.IsFailure)
    {
      _logger.LogWarning("Failed to load plugin '{PluginName}': {Error}", manifest.Name, pluginResult.Error.Message);
      return;
    }

    var plugin = pluginResult.Value;

    var exists = await _pluginRepository.ExistsAsync(plugin.Metadata.Name, plugin.Metadata.Version.ToString(), cancellationToken);
    if (exists)
    {
      _logger.LogDebug("Plugin {PluginName} v{Version} already exists in the repository. Skipping registration.", plugin.Metadata.Name, plugin.Metadata.Version);
      return;
    }

    await ValidateAndRegisterPluginAsync(plugin, cancellationToken);
  }

  // --- THIS IS THE METHOD WITH THE ENHANCED LOGGING ---
  private async Task ValidateAndRegisterPluginAsync(Plugin plugin, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Validating plugin: {PluginName} ({Language})", plugin.Metadata.Name, plugin.Metadata.Language);

    var validationResult = await _runtimeManager.ValidatePluginAsync(plugin, cancellationToken);

    if (validationResult.IsFailure || !validationResult.Value)
    {
      string errorMessage;
      if (validationResult.IsFailure)
      {
        errorMessage = $"Validation failed with error: {validationResult.Error.Code} - {validationResult.Error.Message}";
      }
      else
      {
        // This case is when the validation returns a successful result but the value is false.
        errorMessage = $"The runtime manager's validation check returned false. This often means a required file (like an entry point) was not found. Check previous logs for details from the specific runtime manager.";
      }

      _logger.LogWarning("Plugin validation FAILED for {PluginName}. Reason: {Error}", plugin.Metadata.Name, errorMessage);

      plugin.Validate(false, errorMessage);
    }
    else
    {
      _logger.LogInformation("Plugin validation successful for {PluginName}", plugin.Metadata.Name);
      plugin.Validate(true);
    }

    try
    {
      await _pluginRepository.AddAsync(plugin, cancellationToken);
      await _pluginRepository.SaveChangesAsync(cancellationToken);

      _logger.LogInformation("Successfully registered plugin: {PluginName} v{Version} ({Language}) - Final Status: {Status}",
          plugin.Metadata.Name,
          plugin.Metadata.Version,
          plugin.Metadata.Language,
          plugin.Status);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to save plugin {PluginName} to the repository.", plugin.Metadata.Name);
    }
  }

  private async Task ValidatePluginDirectoriesAsync(CancellationToken cancellationToken)
  {
    var resolvedDirectories = _options.Value.Plugins.GetResolvedPluginDirectories().ToList();
    _logger.LogDebug("Validating {Count} plugin directories.", resolvedDirectories.Count);

    foreach (var directory in resolvedDirectories)
    {
      if (!Directory.Exists(directory))
      {
        _logger.LogWarning("Plugin directory does not exist, creating it: {Directory}", directory);
        try
        {
          Directory.CreateDirectory(directory);
          var readmePath = Path.Combine(directory, "README.md");
          if (!File.Exists(readmePath))
          {
            await File.WriteAllTextAsync(readmePath, GetPluginDirectoryReadme(), cancellationToken);
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to create plugin directory structure at: {Directory}", directory);
          // This might be a critical error depending on deployment strategy.
        }
      }
    }
  }

  private static string GetPluginDirectoryReadme()
  {
    return """
    # DevFlow Plugins Directory
        
    This directory contains plugins that extend DevFlow's automation capabilities.
        
    ## Directory Structure
        
    Place your language-specific plugin folders inside this directory.
    - `csharp/`
    - `typescript/`
    - `python/`
    
    Each plugin must include a `plugin.json` manifest file.
    """;
  }
}