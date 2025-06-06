﻿using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DevFlow.Infrastructure.Plugins;

/// <summary>
/// Default implementation of plugin execution service.
/// Coordinates plugin retrieval, context creation, execution, and cleanup.
/// </summary>
public sealed class PluginExecutionService : IPluginExecutionService
{
  private readonly IPluginRepository _pluginRepository;
  private readonly IPluginRuntimeManager _runtimeManager;
  private readonly IPluginRuntimeManagerFactory _runtimeManagerFactory;
  private readonly IPluginDiscoveryService _discoveryService;
  private readonly ILogger<PluginExecutionService> _logger;

  public PluginExecutionService(
      IPluginRepository pluginRepository,
      IPluginRuntimeManager runtimeManager,
      IPluginRuntimeManagerFactory runtimeManagerFactory,
      IPluginDiscoveryService discoveryService,
      ILogger<PluginExecutionService> logger)
  {
    _pluginRepository = pluginRepository;
    _runtimeManager = runtimeManager;
    _runtimeManagerFactory = runtimeManagerFactory;
    _discoveryService = discoveryService;
    _logger = logger;
  }

  public async Task<Result<PluginExecutionResult>> ExecutePluginAsync(
    PluginId pluginId,
    object? inputData = null,
    IReadOnlyDictionary<string, object>? executionParameters = null,
    CancellationToken cancellationToken = default)
  {
    if (pluginId is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "PluginExecution.PluginIdNull", "Plugin ID cannot be null."));

    string? workingDirectory = null;

    try
    {
      _logger.LogInformation("Executing plugin: {PluginId}", pluginId.Value);

      var plugin = await _pluginRepository.GetByIdAsync(pluginId, cancellationToken);
      if (plugin is null)
      {
        return Result<PluginExecutionResult>.Failure(Error.NotFound(
            "PluginExecution.PluginNotFound", $"Plugin with ID '{pluginId.Value}' was not found."));
      }

      var manifest = await _discoveryService.ScanPluginDirectoryAsync(plugin.PluginPath, cancellationToken);
      if (manifest.IsSuccess)
      {
        var currentHashResult = await _discoveryService.GetPluginSourceHashAsync(manifest.Value, cancellationToken);
        if (currentHashResult.IsSuccess && currentHashResult.Value != plugin.SourceHash)
        {
          _logger.LogWarning("Plugin {PluginName} is dirty. Source hash mismatch. Forcing re-validation.", plugin.Metadata.Name);
          var validationResult = await _runtimeManager.ValidatePluginAsync(plugin, cancellationToken);
          plugin.UpdateSourceHash(currentHashResult.Value);
          plugin.Validate(validationResult.IsSuccess && validationResult.Value, validationResult.IsFailure ? validationResult.Error.Message : null);
          await _pluginRepository.UpdateAsync(plugin, cancellationToken);
          await _pluginRepository.SaveChangesAsync(cancellationToken);
          _logger.LogInformation("Plugin {PluginName} re-validated. New status: {Status}", plugin.Metadata.Name, plugin.Status);
        }
      }

      if (plugin.Status != PluginStatus.Available)
      {
        return Result<PluginExecutionResult>.Failure(Error.Validation(
            "PluginExecution.NotAvailable", $"Plugin '{plugin.Metadata.Name}' is not in an 'Available' state (Current: {plugin.Status}). Error: {plugin.ErrorMessage}"));
      }

      workingDirectory = CreateWorkingDirectory(plugin.Metadata.Name);
      var contextResult = PluginExecutionContext.Create(workingDirectory, inputData, executionParameters);
      if (contextResult.IsFailure)
      {
        return Result<PluginExecutionResult>.Failure(contextResult.Error);
      }

      var executionResult = await _runtimeManager.ExecuteAsync(plugin, contextResult.Value, cancellationToken);
      if (executionResult.IsSuccess)
      {
        _logger.LogInformation("Plugin execution completed successfully: {PluginName} ({Duration}ms)", plugin.Metadata.Name, executionResult.Value.ExecutionDuration.TotalMilliseconds);
      }
      else
      {
        _logger.LogWarning("Plugin execution failed: {PluginName}. Error: {Error}", plugin.Metadata.Name, executionResult.Error.Message);
      }
      return executionResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unhandled exception during plugin execution: {PluginId}", pluginId.Value);
      return Result<PluginExecutionResult>.Failure(Error.Failure(
          "PluginExecution.UnhandledException", $"Plugin execution failed with exception: {ex.Message}"));
    }
    finally
    {
      if (!string.IsNullOrEmpty(workingDirectory))
      {
        await CleanupWorkingDirectoryAsync(workingDirectory);
      }
    }
  }

  // ... The other methods (ValidatePluginExecutionAsync, GetPluginCapabilitiesAsync, etc.) remain the same ...
  // ... as their definitions in your codebase are correct. I'm omitting them for brevity, 
  // ... but they should remain in your file.
  public async Task<Result<bool>> ValidatePluginExecutionAsync(PluginId pluginId, CancellationToken cancellationToken = default)
  {
    // ... implementation ...
    if (pluginId is null)
      return Result<bool>.Failure(Error.Validation(
          "PluginExecution.PluginIdNull", "Plugin ID cannot be null."));

    try
    {
      _logger.LogDebug("Validating plugin execution: {PluginId}", pluginId.Value);

      var plugin = await _pluginRepository.GetByIdAsync(pluginId, cancellationToken);
      if (plugin is null)
      {
        var error = Error.NotFound(
            "PluginExecution.PluginNotFound",
            $"Plugin with ID '{pluginId.Value}' was not found.");
        return Result<bool>.Failure(error);
      }

      if (!_runtimeManager.CanExecutePlugin(plugin))
      {
        _logger.LogWarning("No compatible runtime manager for plugin: {PluginName} ({Language})",
            plugin.Metadata.Name, plugin.Metadata.Language);
        return Result<bool>.Success(false);
      }

      var validationResult = await _runtimeManager.ValidatePluginAsync(plugin, cancellationToken);
      if (validationResult.IsFailure)
      {
        _logger.LogDebug("Plugin validation failed: {PluginName}. Error: {Error}",
            plugin.Metadata.Name, validationResult.Error.Message);
        return validationResult;
      }

      _logger.LogDebug("Plugin validation completed: {PluginName}. Valid: {IsValid}",
          plugin.Metadata.Name, validationResult.Value);

      return validationResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate plugin execution: {PluginId}", pluginId.Value);
      return Result<bool>.Failure(Error.Failure(
          "PluginExecution.ValidationException", $"Plugin validation failed with exception: {ex.Message}"));
    }
  }

  public async Task<Result<PluginExecutionCapabilities>> GetPluginCapabilitiesAsync(PluginId pluginId, CancellationToken cancellationToken = default)
  {
    // ... implementation ...
    if (pluginId is null)
      return Result<PluginExecutionCapabilities>.Failure(Error.Validation(
          "PluginExecution.PluginIdNull", "Plugin ID cannot be null."));

    try
    {
      _logger.LogDebug("Getting plugin capabilities: {PluginId}", pluginId.Value);

      var plugin = await _pluginRepository.GetByIdAsync(pluginId, cancellationToken);
      if (plugin is null)
      {
        var error = Error.NotFound(
            "PluginExecution.PluginNotFound",
            $"Plugin with ID '{pluginId.Value}' was not found.");
        return Result<PluginExecutionCapabilities>.Failure(error);
      }

      var runtimeManager = _runtimeManagerFactory.GetRuntimeManager(plugin);
      if (runtimeManager is null)
      {
        var capabilities = PluginExecutionCapabilities.CreateNotExecutable(
            plugin.Metadata.Language.ToString(),
            new[] { $"No compatible runtime manager found for language '{plugin.Metadata.Language}'" });

        return Result<PluginExecutionCapabilities>.Success(capabilities);
      }

      var validationResult = await runtimeManager.ValidatePluginAsync(plugin, cancellationToken);
      if (validationResult.IsFailure)
      {
        var capabilities = PluginExecutionCapabilities.CreateNotExecutable(
            plugin.Metadata.Language.ToString(),
            new[] { $"Plugin validation failed: {validationResult.Error.Message}" });

        return Result<PluginExecutionCapabilities>.Success(capabilities);
      }

      if (!validationResult.Value)
      {
        var capabilities = PluginExecutionCapabilities.CreateNotExecutable(
            plugin.Metadata.Language.ToString(),
            new[] { "Plugin failed validation checks" });

        return Result<PluginExecutionCapabilities>.Success(capabilities);
      }

      var executableCapabilities = PluginExecutionCapabilities.CreateExecutable(
          plugin.Metadata.Language.ToString(),
          runtimeManager.RuntimeId,
          plugin.Capabilities.ToList(),
          Array.Empty<string>());

      _logger.LogDebug("Plugin capabilities determined: {PluginName} can execute via {RuntimeId}",
          plugin.Metadata.Name, runtimeManager.RuntimeId);

      return Result<PluginExecutionCapabilities>.Success(executableCapabilities);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get plugin capabilities: {PluginId}", pluginId.Value);
      return Result<PluginExecutionCapabilities>.Failure(Error.Failure(
          "PluginExecution.CapabilitiesException", $"Failed to get plugin capabilities: {ex.Message}"));
    }
  }
  private static string CreateWorkingDirectory(string pluginName)
  {
    var sanitizedPluginName = string.Join("_", pluginName.Split(Path.GetInvalidFileNameChars()));
    var uniqueId = Guid.NewGuid().ToString("N")[..8];
    var directoryName = $"devflow-plugin-{sanitizedPluginName}-{uniqueId}";
    var workingDirectory = Path.Combine(Path.GetTempPath(), directoryName);
    Directory.CreateDirectory(workingDirectory);
    return workingDirectory;
  }

  private async Task CleanupWorkingDirectoryAsync(string workingDirectory)
  {
    try
    {
      if (Directory.Exists(workingDirectory))
      {
        await Task.Delay(100);
        Directory.Delete(workingDirectory, true);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to cleanup working directory: {WorkingDirectory}", workingDirectory);
    }
  }
}