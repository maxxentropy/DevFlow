using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevFlow.Infrastructure.Services;

/// <summary>
/// Hosted service responsible for initializing the plugin runtime system on application startup.
/// Discovers plugins, validates them, and registers them with the system.
/// </summary>
public sealed class PluginRuntimeInitializationService : IHostedService
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IPluginRuntimeManager _runtimeManager;
  private readonly IPluginDiscoveryService _discoveryService;
  private readonly IOptions<DevFlowOptions> _options;
  private readonly ILogger<PluginRuntimeInitializationService> _logger;

  public PluginRuntimeInitializationService(
      IServiceScopeFactory scopeFactory,
      IPluginRuntimeManager runtimeManager,
      IPluginDiscoveryService discoveryService,
      IOptions<DevFlowOptions> options,
      ILogger<PluginRuntimeInitializationService> logger)
  {
    _scopeFactory = scopeFactory;
    _runtimeManager = runtimeManager;
    _discoveryService = discoveryService;
    _options = options;
    _logger = logger;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Plugin runtime system initialization starting...");

    // Create a new scope to resolve scoped services like the repository
    using var scope = _scopeFactory.CreateScope();
    var pluginRepository = scope.ServiceProvider.GetRequiredService<IPluginRepository>();

    try
    {
      await ValidatePluginDirectoriesAsync(cancellationToken);

      await _runtimeManager.InitializeAsync(cancellationToken);
      _logger.LogDebug("Composite plugin runtime manager initialized.");

      // Pass the resolved repository to the discovery method
      await DiscoverAndRegisterPluginsAsync(pluginRepository, cancellationToken);

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

  private async Task DiscoverAndRegisterPluginsAsync(IPluginRepository pluginRepository, CancellationToken cancellationToken)
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
        await ProcessPluginManifestAsync(manifest, pluginRepository, cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An unexpected error occurred while processing plugin manifest for '{PluginName}'.", manifest.Name);
      }
    }
  }

  private async Task ProcessPluginManifestAsync(PluginManifest manifest, IPluginRepository pluginRepository, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Processing plugin: {PluginName} v{Version}", manifest.Name, manifest.Version);

    var allPlugins = await pluginRepository.GetAllAsync(cancellationToken);
    var existingPlugin = allPlugins
        .FirstOrDefault(p => p.Metadata.Name == manifest.Name && p.Metadata.Version.ToString() == manifest.Version);

    Plugin pluginToValidate;

    if (existingPlugin != null)
    {
      // If plugin exists and is NOT in an error state, we can skip it on startup.
      if (existingPlugin.Status != PluginStatus.Error)
      {
        _logger.LogDebug("Plugin {PluginName} v{Version} already exists with status {Status}. Skipping startup registration.",
            manifest.Name, manifest.Version, existingPlugin.Status);
        return;
      }

      // The plugin exists but failed before. We'll use this existing entity to retry validation.
      pluginToValidate = existingPlugin;
      _logger.LogInformation("Re-validating failed plugin: {PluginName} v{Version}", manifest.Name, manifest.Version);
    }
    else
    {
      // This is a completely new plugin. Load it from the manifest.
      var pluginResult = await _discoveryService.LoadPluginAsync(manifest, cancellationToken);
      if (pluginResult.IsFailure)
      {
        _logger.LogWarning("Failed to load plugin '{PluginName}': {Error}", manifest.Name, pluginResult.Error.Message);
        return;
      }
      pluginToValidate = pluginResult.Value;
    }

    // Now, call the validation logic with the correct plugin object (either new or existing).
    await ValidateAndRegisterPluginAsync(pluginToValidate, manifest, pluginRepository, cancellationToken);
  }

  private async Task ValidateAndRegisterPluginAsync(Plugin plugin, PluginManifest manifest, IPluginRepository pluginRepository, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Validating plugin: {PluginName} ({Language})", plugin.Metadata.Name, plugin.Metadata.Language);

    var validationResult = await _runtimeManager.ValidatePluginAsync(plugin, cancellationToken);

    var hashResult = await _discoveryService.GetPluginSourceHashAsync(manifest, cancellationToken);
    if (hashResult.IsSuccess)
    {
      plugin.UpdateSourceHash(hashResult.Value);
    }
    else
    {
      _logger.LogWarning("Could not generate source hash for {PluginName}: {Error}", plugin.Metadata.Name, hashResult.Error.Message);
    }

    if (validationResult.IsFailure || !validationResult.Value)
    {
      string errorMessage = validationResult.IsFailure ? validationResult.Error.Message : "The runtime manager's validation check returned false.";
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
      var existingPlugin = await pluginRepository.GetByIdAsync(plugin.Id, cancellationToken);
      if (existingPlugin != null)
      {
        await pluginRepository.UpdateAsync(plugin, cancellationToken);
      }
      else
      {
        await pluginRepository.AddAsync(plugin, cancellationToken);
      }
      await pluginRepository.SaveChangesAsync(cancellationToken);

      _logger.LogInformation("Successfully registered/updated plugin: {PluginName} v{Version} - Final Status: {Status}",
          plugin.Metadata.Name,
          plugin.Metadata.Version,
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