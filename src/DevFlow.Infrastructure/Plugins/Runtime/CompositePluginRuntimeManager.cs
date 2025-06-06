using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Plugins.Runtime;

/// <summary>
/// Composite runtime manager that delegates to appropriate language-specific runtime managers.
/// Acts as a unified entry point for plugin execution while maintaining separation of concerns.
/// </summary>
public sealed class CompositePluginRuntimeManager : IPluginRuntimeManager
{
  private readonly IPluginRuntimeManagerFactory _runtimeManagerFactory;
  private readonly ILogger<CompositePluginRuntimeManager> _logger;

  public CompositePluginRuntimeManager(
      IPluginRuntimeManagerFactory runtimeManagerFactory,
      ILogger<CompositePluginRuntimeManager> logger)
  {
    _runtimeManagerFactory = runtimeManagerFactory;
    _logger = logger;
  }

  public IReadOnlyList<PluginLanguage> SupportedLanguages
  {
    get
    {
      try
      {
        var allLanguages = _runtimeManagerFactory.GetAllRuntimeManagers()
            .SelectMany(rm => rm.SupportedLanguages)
            .Distinct()
            .ToList();

        return allLanguages.AsReadOnly();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get supported languages from runtime managers");
        return Array.Empty<PluginLanguage>();
      }
    }
  }

  public string RuntimeId => "composite-runtime";

  public async Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "CompositeRuntime.PluginNull", "Plugin cannot be null."));

    if (context is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "CompositeRuntime.ContextNull", "Execution context cannot be null."));

    // --- MODIFICATION START ---
    // Find a manager that supports the language, even if it's not currently "available"
    var managersForLanguage = _runtimeManagerFactory.GetRuntimeManagersForLanguage(plugin.Metadata.Language).ToList();
    if (!managersForLanguage.Any())
    {
      var langError = Error.Validation(
          "CompositeRuntime.LanguageNotSupported",
          $"The plugin language '{plugin.Metadata.Language}' is not supported by any registered runtime manager.");
      _logger.LogWarning("Execution failed for {PluginName}: {Error}", plugin.Metadata.Name, langError.Message);
      return Result<PluginExecutionResult>.Failure(langError);
    }

    // Now, find a manager that can actually execute it (i.e., is available)
    var runtimeManager = managersForLanguage.FirstOrDefault(m => m.CanExecutePlugin(plugin));
    if (runtimeManager is null)
    {
      var runtimeError = Error.Failure(
          "CompositeRuntime.RuntimeUnavailable",
          $"A runtime manager for '{plugin.Metadata.Language}' was found, but it is not available. Check server logs for initialization errors (e.g., Python or Node.js not found in PATH).");

      _logger.LogWarning("Execution failed for {PluginName}: {Error}", plugin.Metadata.Name, runtimeError.Message);
      return Result<PluginExecutionResult>.Failure(runtimeError);
    }
    // --- MODIFICATION END ---

    _logger.LogDebug("Delegating execution to runtime manager: {RuntimeId} for plugin: {PluginName}",
        runtimeManager.RuntimeId, plugin.Metadata.Name);

    try
    {
      var result = await runtimeManager.ExecuteAsync(plugin, context, cancellationToken);

      if (result.IsSuccess)
      {
        _logger.LogDebug("Plugin execution completed successfully via {RuntimeId}: {PluginName}",
            runtimeManager.RuntimeId, plugin.Metadata.Name);
      }
      else
      {
        _logger.LogWarning("Plugin execution failed via {RuntimeId}: {PluginName}. Error: {Error}",
            runtimeManager.RuntimeId, plugin.Metadata.Name, result.Error.Message);
      }

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unhandled exception during plugin execution delegation: {PluginName}", plugin.Metadata.Name);
      return Result<PluginExecutionResult>.Failure(Error.Failure(
          "CompositeRuntime.DelegationFailed", $"Plugin execution delegation failed: {ex.Message}"));
    }
  }

  public async Task<Result<bool>> ValidatePluginAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<bool>.Failure(Error.Validation(
          "CompositeRuntime.PluginNull", "Plugin cannot be null."));

    var runtimeManager = _runtimeManagerFactory.GetRuntimeManager(plugin);
    if (runtimeManager is null)
    {
      var error = Error.Validation(
          "CompositeRuntime.NoCompatibleManager",
          $"No compatible runtime manager found for plugin '{plugin.Metadata.Name}' with language '{plugin.Metadata.Language}'.");

      _logger.LogWarning("No compatible runtime manager found for validation: {PluginName} ({Language})",
          plugin.Metadata.Name, plugin.Metadata.Language);

      return Result<bool>.Failure(error);
    }

    _logger.LogDebug("Delegating validation to runtime manager: {RuntimeId} for plugin: {PluginName}",
        runtimeManager.RuntimeId, plugin.Metadata.Name);

    try
    {
      var result = await runtimeManager.ValidatePluginAsync(plugin, cancellationToken);

      _logger.LogDebug("Plugin validation completed via {RuntimeId}: {PluginName}. Valid: {IsValid}",
          runtimeManager.RuntimeId, plugin.Metadata.Name, result.IsSuccess ? result.Value : false);

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unhandled exception during plugin validation delegation: {PluginName}", plugin.Metadata.Name);
      return Result<bool>.Failure(Error.Failure(
          "CompositeRuntime.ValidationDelegationFailed", $"Plugin validation delegation failed: {ex.Message}"));
    }
  }

  public bool CanExecutePlugin(Plugin plugin)
  {
    if (plugin is null)
      return false;

    try
    {
      var runtimeManager = _runtimeManagerFactory.GetRuntimeManager(plugin);
      var canExecute = runtimeManager?.CanExecutePlugin(plugin) ?? false;

      _logger.LogDebug("Plugin execution capability check: {PluginName} ({Language}) = {CanExecute}",
          plugin.Metadata.Name, plugin.Metadata.Language, canExecute);

      return canExecute;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to check plugin execution capability: {PluginName}", plugin.Metadata.Name);
      return false;
    }
  }

  public async Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Initializing composite plugin runtime manager");

    try
    {
      var runtimeManagers = _runtimeManagerFactory.GetAllRuntimeManagers().ToList();

      if (!runtimeManagers.Any())
      {
        _logger.LogWarning("No runtime managers found during initialization");
        return Result.Success(); // Not necessarily an error if no plugins are needed yet
      }

      var initializationTasks = runtimeManagers.Select(rm => rm.InitializeAsync(cancellationToken));
      var results = await Task.WhenAll(initializationTasks);

      var failures = results.Where(r => r.IsFailure).ToList();
      if (failures.Any())
      {
        var errorMessages = failures.Select(f => f.Error.Message);
        var combinedError = string.Join("; ", errorMessages);

        _logger.LogError("Failed to initialize some runtime managers: {Errors}", combinedError);

        return Result.Failure(Error.Failure(
            "CompositeRuntime.InitializationFailed",
            $"Failed to initialize runtime managers: {combinedError}"));
      }

      _logger.LogInformation("Composite plugin runtime manager initialized successfully with {Count} runtime managers",
          runtimeManagers.Count);

      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize composite plugin runtime manager");
      return Result.Failure(Error.Failure(
          "CompositeRuntime.InitializationException", $"Initialization failed with exception: {ex.Message}"));
    }
  }

  public async Task DisposeAsync(CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Disposing composite plugin runtime manager");

    try
    {
      var runtimeManagers = _runtimeManagerFactory.GetAllRuntimeManagers().ToList();
      var disposalTasks = runtimeManagers.Select(rm => rm.DisposeAsync(cancellationToken));

      await Task.WhenAll(disposalTasks);

      _logger.LogInformation("Composite plugin runtime manager disposed successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to dispose some runtime managers");
      // Don't rethrow during disposal
    }
  }
}