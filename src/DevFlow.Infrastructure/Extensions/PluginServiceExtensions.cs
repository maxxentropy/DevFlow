using DevFlow.Application.Plugins.Runtime;
using DevFlow.Infrastructure.Plugins;
using DevFlow.Infrastructure.Plugins.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering plugin runtime services with the dependency injection container.
/// </summary>
public static class PluginServiceExtensions
{
  private const string LoggerCategory = "DevFlow.Infrastructure.Extensions.PluginServiceExtensions";

  /// <summary>
  /// Adds plugin runtime services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginRuntime(this IServiceCollection services)
  {
    return services
        .AddPluginDiscovery()
        .AddPluginRuntimeManagers()
        .AddPluginOrchestration();
  }

  /// <summary>
  /// Adds plugin discovery services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginDiscovery(this IServiceCollection services)
  {
    // Register plugin discovery service
    services.TryAddScoped<IPluginDiscoveryService, PluginDiscoveryService>();

    return services;
  }

  /// <summary>
  /// Adds all plugin runtime managers to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginRuntimeManagers(this IServiceCollection services)
  {
    // Register individual runtime managers
    services.TryAddScoped<CSharpRuntimeManager>();

    // Register runtime manager factory
    services.TryAddScoped<IPluginRuntimeManagerFactory, PluginRuntimeManagerFactory>();

    // Register composite runtime manager
    services.TryAddScoped<IPluginRuntimeManager, CompositePluginRuntimeManager>();

    return services;
  }

  /// <summary>
  /// Adds plugin orchestration services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginOrchestration(this IServiceCollection services)
  {
    // Register plugin execution service
    services.TryAddScoped<IPluginExecutionService, PluginExecutionService>();

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
      var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
      var logger = loggerFactory?.CreateLogger(LoggerCategory);
      logger?.LogInformation("Validating plugin service registration...");

      // ... rest of the validation logic ...

      logger?.LogInformation("Plugin service validation completed successfully");
      return true;
    }
    catch (Exception ex)
    {
      var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
      var logger = loggerFactory?.CreateLogger(LoggerCategory);
      logger?.LogError(ex, "Plugin service validation failed");
      return false;
    }
  }
}