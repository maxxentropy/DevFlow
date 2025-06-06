using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Infrastructure.Plugins;
using DevFlow.Infrastructure.Plugins.Runtime;
using DevFlow.Infrastructure.Plugins.Security;
using DevFlow.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Extensions;

/// <summary>
/// Updated plugin service registration that replaces the original methods to avoid conflicts.
/// </summary>
public static class PluginServiceExtensions
{
  /// <summary>
  /// Adds the complete enhanced plugin system with security, dependency resolution, and optimized runtimes.
  /// This replaces the original AddPluginSystem method.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <param name="configureSecurityOptions">Optional configuration for plugin security options</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginSystem(
      this IServiceCollection services,
      Action<PluginSecurityOptions>? configureSecurityOptions = null)
  {
    return services
        .AddPluginSecurity(configureSecurityOptions)
        .AddPluginDependencyManagement()
        .AddPluginRuntimes()
        .AddPluginExecution()
        .AddPluginDiscovery()
        .AddPluginInitialization();
  }

  /// <summary>
  /// Adds plugin security and sandboxing services.
  /// </summary>
  private static IServiceCollection AddPluginSecurity(
      this IServiceCollection services,
      Action<PluginSecurityOptions>? configureOptions = null)
  {
    // Configure security options
    if (configureOptions != null)
    {
      services.Configure(configureOptions);
    }
    else
    {
      services.Configure<PluginSecurityOptions>(options =>
      {
        // Apply secure defaults
        options.AllowNetworkAccess = false;
        options.AllowFileIO = true;
        options.AllowReflection = false;
        options.AllowedFileSystemPaths = new List<string>();
        options.AllowedEnvironmentVariables = new List<string> { "PATH", "TEMP", "TMP", "HOME", "USERPROFILE" };
        options.RestrictedAssemblies = new List<string> { "System.Management", "Microsoft.Win32", "System.Diagnostics.Process" };
      });
    }

    // Register security manager
    services.TryAddSingleton(provider =>
    {
      var logger = provider.GetRequiredService<ILogger<PluginSecurityManager>>();
      var options = provider.GetService<PluginSecurityOptions>();
      return new PluginSecurityManager(logger, options);
    });

    return services;
  }

  /// <summary>
  /// Adds enhanced plugin dependency management with multi-package-manager support.
  /// </summary>
  public static IServiceCollection AddPluginDependencyManagement(this IServiceCollection services)
  {
    // Register HttpClient for package downloads
    services.TryAddSingleton<HttpClient>(provider =>
    {
      var client = new HttpClient();
      client.Timeout = TimeSpan.FromMinutes(5);
      client.DefaultRequestHeaders.Add("User-Agent", "DevFlow-Plugin-System/2.0");
      return client;
    });

    // Register enhanced dependency resolver
    services.TryAddScoped<IPluginDependencyResolver, PluginDependencyResolver>();

    return services;
  }

  /// <summary>
  /// Adds enhanced plugin runtime managers with improved C# compilation and caching.
  /// </summary>
  public static IServiceCollection AddPluginRuntimes(this IServiceCollection services)
  {
    // Register enhanced runtime managers
    services.TryAddSingleton(provider =>
    {
      var logger = provider.GetRequiredService<ILogger<CSharpRuntimeManager>>();
      var dependencyResolver = provider.GetRequiredService<IPluginDependencyResolver>();
      return new CSharpRuntimeManager(logger, dependencyResolver);
    });

    services.TryAddSingleton<TypeScriptRuntimeManager>();
    services.TryAddSingleton<PythonRuntimeManager>();

    // Register runtime manager factory
    services.TryAddSingleton<IPluginRuntimeManagerFactory, PluginRuntimeManagerFactory>();

    // Register composite runtime manager as the primary implementation
    services.TryAddSingleton<IPluginRuntimeManager, CompositePluginRuntimeManager>();

    return services;
  }

  /// <summary>
  /// Adds plugin execution services with security integration.
  /// </summary>
  public static IServiceCollection AddPluginExecution(this IServiceCollection services)
  {
    // Register plugin execution service
    services.TryAddScoped<IPluginExecutionService, PluginExecutionService>();

    return services;
  }

  /// <summary>
  /// Adds plugin discovery services.
  /// </summary>
  public static IServiceCollection AddPluginDiscovery(this IServiceCollection services)
  {
    // Register plugin discovery service
    services.TryAddScoped<IPluginDiscoveryService, PluginDiscoveryService>();

    return services;
  }

  /// <summary>
  /// Adds plugin initialization services.
  /// </summary>
  public static IServiceCollection AddPluginInitialization(this IServiceCollection services)
  {
    // Add plugin initialization as a hosted service
    services.AddHostedService<PluginRuntimeInitializationService>();

    return services;
  }

  /// <summary>
  /// Validates that all required plugin services are properly registered.
  /// </summary>
  /// <param name="serviceProvider">The service provider</param>
  /// <returns>True if all services are properly registered</returns>
  public static bool ValidatePluginServices(this IServiceProvider serviceProvider)
  {
    try
    {
      var logger = serviceProvider.GetService<ILogger<object>>();
      logger?.LogInformation("Validating plugin service registration...");

      // Validate core services
      var discoveryService = serviceProvider.GetRequiredService<IPluginDiscoveryService>();
      var runtimeManagerFactory = serviceProvider.GetRequiredService<IPluginRuntimeManagerFactory>();
      var runtimeManager = serviceProvider.GetRequiredService<IPluginRuntimeManager>();
      var executionService = serviceProvider.GetRequiredService<IPluginExecutionService>();
      var dependencyResolver = serviceProvider.GetRequiredService<IPluginDependencyResolver>();
      var pluginRepository = serviceProvider.GetRequiredService<IPluginRepository>();

      // Validate runtime managers are available
      var runtimeManagers = runtimeManagerFactory.GetAllRuntimeManagers().ToList();
      if (!runtimeManagers.Any())
      {
        logger?.LogWarning("No plugin runtime managers are registered");
        return false;
      }

      logger?.LogInformation("Plugin service validation completed successfully. {Count} runtime managers available",
          runtimeManagers.Count);

      return true;
    }
    catch (Exception ex)
    {
      var logger = serviceProvider.GetService<ILogger<object>>();
      logger?.LogError(ex, "Plugin service validation failed");
      return false;
    }
  }

  /// <summary>
  /// Performs a comprehensive health check of the plugin system.
  /// </summary>
  /// <param name="serviceProvider">The service provider</param>
  /// <returns>Health check result with detailed information</returns>
  public static async Task<PluginSystemHealthCheck> PerformPluginHealthCheckAsync(
      this IServiceProvider serviceProvider,
      CancellationToken cancellationToken = default)
  {
    var healthCheck = new PluginSystemHealthCheck
    {
      Timestamp = DateTimeOffset.UtcNow,
      IsHealthy = true,
      Components = new Dictionary<string, ComponentHealth>()
    };

    try
    {
      var logger = serviceProvider.GetService<ILogger<object>>();

      // Check runtime managers
      await CheckRuntimeManagersHealthAsync(serviceProvider, healthCheck, logger, cancellationToken);

      // Check dependency resolver
      await CheckDependencyResolverHealthAsync(serviceProvider, healthCheck, logger, cancellationToken);

      // Check plugin repository
      await CheckPluginRepositoryHealthAsync(serviceProvider, healthCheck, logger, cancellationToken);

      // Determine overall health
      healthCheck.IsHealthy = healthCheck.Components.Values.All(c => c.IsHealthy);

      return healthCheck;
    }
    catch (Exception ex)
    {
      healthCheck.IsHealthy = false;
      healthCheck.Error = ex.Message;
      return healthCheck;
    }
  }

  private static async Task CheckRuntimeManagersHealthAsync(
      IServiceProvider serviceProvider,
      PluginSystemHealthCheck healthCheck,
      ILogger? logger,
      CancellationToken cancellationToken)
  {
    try
    {
      var runtimeManagerFactory = serviceProvider.GetRequiredService<IPluginRuntimeManagerFactory>();
      var runtimeManagers = runtimeManagerFactory.GetAllRuntimeManagers().ToList();

      foreach (var runtimeManager in runtimeManagers)
      {
        try
        {
          var initResult = await runtimeManager.InitializeAsync(cancellationToken);
          healthCheck.Components[runtimeManager.RuntimeId] = new ComponentHealth
          {
            IsHealthy = initResult.IsSuccess,
            Message = initResult.IsSuccess ? "Runtime manager operational" : initResult.Error.Message,
            LastChecked = DateTimeOffset.UtcNow
          };
        }
        catch (Exception ex)
        {
          healthCheck.Components[runtimeManager.RuntimeId] = new ComponentHealth
          {
            IsHealthy = false,
            Message = $"Runtime manager initialization failed: {ex.Message}",
            LastChecked = DateTimeOffset.UtcNow
          };
        }
      }
    }
    catch (Exception ex)
    {
      healthCheck.Components["RuntimeManagers"] = new ComponentHealth
      {
        IsHealthy = false,
        Message = $"Failed to check runtime managers: {ex.Message}",
        LastChecked = DateTimeOffset.UtcNow
      };
    }
  }

  private static async Task CheckDependencyResolverHealthAsync(
      IServiceProvider serviceProvider,
      PluginSystemHealthCheck healthCheck,
      ILogger? logger,
      CancellationToken cancellationToken)
  {
    await Task.Run(() =>
    {
      try
      {
        var dependencyResolver = serviceProvider.GetRequiredService<IPluginDependencyResolver>();

        // For now, just check that the service can be resolved
        healthCheck.Components["DependencyResolver"] = new ComponentHealth
        {
          IsHealthy = true,
          Message = "Dependency resolver service available",
          LastChecked = DateTimeOffset.UtcNow
        };
      }
      catch (Exception ex)
      {
        healthCheck.Components["DependencyResolver"] = new ComponentHealth
        {
          IsHealthy = false,
          Message = $"Dependency resolver health check failed: {ex.Message}",
          LastChecked = DateTimeOffset.UtcNow
        };
      }
    });
  }

  private static async Task CheckPluginRepositoryHealthAsync(
      IServiceProvider serviceProvider,
      PluginSystemHealthCheck healthCheck,
      ILogger? logger,
      CancellationToken cancellationToken)
  {
    try
    {
      var pluginRepository = serviceProvider.GetRequiredService<IPluginRepository>();

      // Test repository by attempting to get all plugins
      var plugins = await pluginRepository.GetAllAsync(cancellationToken);

      healthCheck.Components["PluginRepository"] = new ComponentHealth
      {
        IsHealthy = true,
        Message = $"Plugin repository operational. {plugins.Count} plugins registered.",
        LastChecked = DateTimeOffset.UtcNow
      };
    }
    catch (Exception ex)
    {
      healthCheck.Components["PluginRepository"] = new ComponentHealth
      {
        IsHealthy = false,
        Message = $"Plugin repository health check failed: {ex.Message}",
        LastChecked = DateTimeOffset.UtcNow
      };
    }
  }
}

/// <summary>
/// Represents the health status of the plugin system.
/// </summary>
public sealed class PluginSystemHealthCheck
{
  public required DateTimeOffset Timestamp { get; init; }
  public required bool IsHealthy { get; set; }
  public required Dictionary<string, ComponentHealth> Components { get; init; }
  public string? Error { get; set; }
}

/// <summary>
/// Represents the health status of an individual component.
/// </summary>
public sealed class ComponentHealth
{
  public required bool IsHealthy { get; init; }
  public required string Message { get; init; }
  public required DateTimeOffset LastChecked { get; init; }
}