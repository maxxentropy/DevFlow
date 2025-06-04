using DevFlow.Application.Plugins.Runtime;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Plugins.Runtime;

/// <summary>
/// Default implementation of plugin runtime manager factory.
/// Manages the discovery and selection of appropriate runtime managers for plugin execution.
/// </summary>
public sealed class PluginRuntimeManagerFactory : IPluginRuntimeManagerFactory
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<PluginRuntimeManagerFactory> _logger;

  public PluginRuntimeManagerFactory(
      IServiceProvider serviceProvider,
      ILogger<PluginRuntimeManagerFactory> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public IEnumerable<IPluginRuntimeManager> GetAllRuntimeManagers()
  {
    try
    {
      var runtimeManagers = new List<IPluginRuntimeManager>();

      // Get C# runtime manager
      var csharpRuntime = _serviceProvider.GetService<CSharpRuntimeManager>();
      if (csharpRuntime is not null)
      {
        runtimeManagers.Add(csharpRuntime);
        _logger.LogDebug("C# runtime manager available");
      }

      // Get TypeScript runtime manager
      var typescriptRuntime = _serviceProvider.GetService<TypeScriptRuntimeManager>();
      if (typescriptRuntime is not null)
      {
        runtimeManagers.Add(typescriptRuntime);
        _logger.LogDebug("TypeScript runtime manager available");
      }

      // Get Python runtime manager
      var pythonRuntime = _serviceProvider.GetService<PythonRuntimeManager>();
      if (pythonRuntime is not null)
      {
        runtimeManagers.Add(pythonRuntime);
        _logger.LogDebug("Python runtime manager available");
      }

      _logger.LogDebug("Retrieved {Count} runtime managers", runtimeManagers.Count);
      return runtimeManagers;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get runtime managers");
      return Enumerable.Empty<IPluginRuntimeManager>();
    }
  }

  public IPluginRuntimeManager? GetRuntimeManager(Plugin plugin)
  {
    if (plugin is null)
    {
      _logger.LogWarning("Cannot get runtime manager for null plugin");
      return null;
    }

    try
    {
      var runtimeManagers = GetAllRuntimeManagers();
      var compatibleManager = runtimeManagers.FirstOrDefault(rm => rm.CanExecutePlugin(plugin));

      if (compatibleManager is null)
      {
        _logger.LogWarning("No compatible runtime manager found for plugin: {PluginName} ({Language})",
            plugin.Metadata.Name, plugin.Metadata.Language);
      }
      else
      {
        _logger.LogDebug("Found compatible runtime manager: {RuntimeId} for plugin: {PluginName}",
            compatibleManager.RuntimeId, plugin.Metadata.Name);
      }

      return compatibleManager;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get runtime manager for plugin: {PluginName}", plugin.Metadata.Name);
      return null;
    }
  }

  public IEnumerable<IPluginRuntimeManager> GetRuntimeManagersForLanguage(PluginLanguage language)
  {
    try
    {
      var allManagers = GetAllRuntimeManagers();
      var compatibleManagers = allManagers.Where(rm => rm.SupportedLanguages.Contains(language)).ToList();

      _logger.LogDebug("Found {Count} runtime managers for language: {Language}",
          compatibleManagers.Count, language);

      return compatibleManagers;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get runtime managers for language: {Language}", language);
      return Enumerable.Empty<IPluginRuntimeManager>();
    }
  }

  public bool IsLanguageSupported(PluginLanguage language)
  {
    try
    {
      var supportedManagers = GetRuntimeManagersForLanguage(language);
      var isSupported = supportedManagers.Any();

      _logger.LogDebug("Language {Language} is {SupportStatus}",
          language, isSupported ? "supported" : "not supported");

      return isSupported;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to check language support for: {Language}", language);
      return false;
    }
  }

  public IPluginRuntimeManager? GetRuntimeManagerById(string runtimeId)
  {
    if (string.IsNullOrWhiteSpace(runtimeId))
    {
      _logger.LogWarning("Cannot get runtime manager for null or empty runtime ID");
      return null;
    }

    try
    {
      var runtimeManagers = GetAllRuntimeManagers();
      var manager = runtimeManagers.FirstOrDefault(rm =>
          string.Equals(rm.RuntimeId, runtimeId, StringComparison.OrdinalIgnoreCase));

      if (manager is null)
      {
        _logger.LogWarning("No runtime manager found with ID: {RuntimeId}", runtimeId);
      }
      else
      {
        _logger.LogDebug("Found runtime manager with ID: {RuntimeId}", runtimeId);
      }

      return manager;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get runtime manager by ID: {RuntimeId}", runtimeId);
      return null;
    }
  }
}