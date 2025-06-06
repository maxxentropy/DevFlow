using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Infrastructure.Plugins;
using DevFlow.Infrastructure.Plugins.Runtime;
using DevFlow.Infrastructure.Services;
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
  /// Adds the complete plugin system including runtime managers, discovery, and initialization.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginSystem(this IServiceCollection services)
  {
    return services
        .AddPluginRuntime()
        .AddPluginInitialization();
  }

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
        .AddPluginExecution()
        .AddPluginDependencyManagement();
  }

  /// <summary>
  /// Adds plugin discovery services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginDiscovery(this IServiceCollection services)
  {
    // Register plugin discovery service
    services.AddScoped<IPluginDiscoveryService, PluginDiscoveryService>();

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
    services.AddSingleton<CSharpRuntimeManager>();
    services.AddSingleton<TypeScriptRuntimeManager>();
    services.AddSingleton<PythonRuntimeManager>();

    // Register runtime manager factory
    services.AddSingleton<IPluginRuntimeManagerFactory, PluginRuntimeManagerFactory>();

    // Register composite runtime manager as the primary implementation
    services.AddSingleton<IPluginRuntimeManager, CompositePluginRuntimeManager>();

    return services;
  }

  /// <summary>
  /// Adds plugin execution services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginExecution(this IServiceCollection services)
  {
    // Register plugin execution service
    services.AddScoped<IPluginExecutionService, PluginExecutionService>();

    return services;
  }

  /// <summary>
  /// Adds plugin dependency management services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginDependencyManagement(this IServiceCollection services)
  {
    // Register HttpClient for NuGet package downloads
    services.AddSingleton<HttpClient>(provider => 
    {
      var client = new HttpClient();
      client.Timeout = TimeSpan.FromMinutes(5);
      client.DefaultRequestHeaders.Add("User-Agent", "DevFlow-Plugin-System/1.0");
      return client;
    });

    // Register dependency resolver
    services.AddScoped<IPluginDependencyResolver, PluginDependencyResolver>();

    return services;
  }

  /// <summary>
  /// Adds plugin initialization services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
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
      var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<object>>();
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
      var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<object>>();
      logger?.LogError(ex, "Plugin service validation failed");
      return false;
    }
  }
}