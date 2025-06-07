using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Infrastructure.Plugins;
using DevFlow.Infrastructure.Plugins.Runtime;
using DevFlow.Infrastructure.Plugins.Security;
using DevFlow.Infrastructure.Services;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Extensions;

/// <summary>
/// Enhanced plugin service registration with security, dependency management, and runtime isolation.
/// </summary>
public static class EnhancedPluginServiceExtensions
{
  /// <summary>
  /// Adds the complete enhanced plugin system with security, dependency resolution, and optimized runtimes.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <param name="configureOptions">Optional configuration for plugin security options</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddEnhancedPluginSystem(
      this IServiceCollection services,
      Action<PluginSecurityOptions>? configureOptions = null)
  {
    return services
        .AddPluginSecurity(configureOptions)
        .AddPluginDependencyManagement()
        .AddEnhancedPluginRuntimes()
        .AddPluginExecution()
        .AddPluginDiscovery()
        .AddPluginInitialization();
  }

  /// <summary>
  /// Adds plugin security and sandboxing services.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <param name="configureOptions">Optional configuration for security options</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginSecurity(
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
    services.TryAddSingleton<PluginSecurityManager>();

    return services;
  }

  /// <summary>
  /// Adds enhanced plugin dependency management with multi-package-manager support.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginDependencyManagement(this IServiceCollection services)
  {
    // Register HttpClient for package downloads
    services.AddSingleton<HttpClient>(provider =>
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
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddEnhancedPluginRuntimes(this IServiceCollection services)
  {
    // Register enhanced runtime managers
    services.TryAddSingleton<CSharpRuntimeManager>();
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
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddPluginExecution(this IServiceCollection services)
  {
    // Register plugin execution service
    services.TryAddScoped<IPluginExecutionService, PluginExecutionService>();

    return services;
  }

  /// <summary>
  /// Adds plugin discovery services.
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
  /// Adds plugin initialization services.
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
  /// Validates that all required enhanced plugin services are properly registered.
  /// </summary>
  /// <param name="serviceProvider">The service provider</param>
  /// <returns>True if all services are properly registered</returns>
  public static bool ValidateEnhancedPluginServices(this IServiceProvider serviceProvider)
  {
    try
    {
      var logger = serviceProvider.GetService<ILogger<object>>();
      logger?.LogInformation("Validating enhanced plugin service registration...");

      // Validate core services
      var discoveryService = serviceProvider.GetRequiredService<IPluginDiscoveryService>();
      var runtimeManagerFactory = serviceProvider.GetRequiredService<IPluginRuntimeManagerFactory>();
      var runtimeManager = serviceProvider.GetRequiredService<IPluginRuntimeManager>();
      var executionService = serviceProvider.GetRequiredService<IPluginExecutionService>();
      var dependencyResolver = serviceProvider.GetRequiredService<IPluginDependencyResolver>();
      var pluginRepository = serviceProvider.GetRequiredService<IPluginRepository>();
      var securityManager = serviceProvider.GetRequiredService<PluginSecurityManager>();

      // Validate runtime managers are available
      var runtimeManagers = runtimeManagerFactory.GetAllRuntimeManagers().ToList();
      if (!runtimeManagers.Any())
      {
        logger?.LogWarning("No plugin runtime managers are registered");
        return false;
      }

      // Validate specific runtime managers
      var csharpRuntime = serviceProvider.GetService<CSharpRuntimeManager>();
      var typescriptRuntime = serviceProvider.GetService<TypeScriptRuntimeManager>();
      var pythonRuntime = serviceProvider.GetService<PythonRuntimeManager>();

      var availableRuntimes = new List<string>();
      if (csharpRuntime != null) availableRuntimes.Add("C#");
      if (typescriptRuntime != null) availableRuntimes.Add("TypeScript");
      if (pythonRuntime != null) availableRuntimes.Add("Python");

      logger?.LogInformation("Enhanced plugin service validation completed successfully. " +
          "Available runtimes: [{Runtimes}], Security: {SecurityEnabled}",
          string.Join(", ", availableRuntimes), securityManager != null);

      return true;
    }
    catch (Exception ex)
    {
      var logger = serviceProvider.GetService<ILogger<object>>();
      logger?.LogError(ex, "Enhanced plugin service validation failed");
      return false;
    }
  }

  /// <summary>
  /// Performs a comprehensive health check of the enhanced plugin system.
  /// </summary>
  /// <param name="serviceProvider">The service provider</param>
  /// <returns>Health check result with detailed information</returns>
  public static async Task<PluginSystemHealthCheck> PerformHealthCheckAsync(
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
      await CheckRuntimeManagersHealth(serviceProvider, healthCheck, logger, cancellationToken);

      // Check dependency resolver
      await CheckDependencyResolverHealth(serviceProvider, healthCheck, logger, cancellationToken);

      // Check security manager
      await CheckSecurityManagerHealth(serviceProvider, healthCheck, logger, cancellationToken);

      // Check plugin repository
      await CheckPluginRepositoryHealth(serviceProvider, healthCheck, logger, cancellationToken);

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

  private static async Task CheckRuntimeManagersHealth(
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

  private static async Task CheckDependencyResolverHealth(
      IServiceProvider serviceProvider,
      PluginSystemHealthCheck healthCheck,
      ILogger? logger,
      CancellationToken cancellationToken)
  {
    try
    {
      var dependencyResolver = serviceProvider.GetRequiredService<IPluginDependencyResolver>();

      // Create a test plugin for dependency resolution testing
      var testPluginResult = CreateTestPlugin();
      if (testPluginResult.IsSuccess)
      {
        var validationResult = await dependencyResolver.ValidateDependenciesAsync(testPluginResult.Value, cancellationToken);
        healthCheck.Components["DependencyResolver"] = new ComponentHealth
        {
          IsHealthy = validationResult.IsSuccess,
          Message = validationResult.IsSuccess ? "Dependency resolver operational" : validationResult.Error.Message,
          LastChecked = DateTimeOffset.UtcNow
        };
      }
      else
      {
        healthCheck.Components["DependencyResolver"] = new ComponentHealth
        {
          IsHealthy = false,
          Message = "Failed to create test plugin for dependency resolver health check",
          LastChecked = DateTimeOffset.UtcNow
        };
      }
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
  }

  private static async Task CheckSecurityManagerHealth(
      IServiceProvider serviceProvider,
      PluginSystemHealthCheck healthCheck,
      ILogger? logger,
      CancellationToken cancellationToken)
  {
    try
    {
      var securityManager = serviceProvider.GetRequiredService<PluginSecurityManager>();

      // Test security manager by creating a test context
      var testPluginResult = CreateTestPlugin();
      if (testPluginResult.IsSuccess)
      {
        var testContext = new PluginExecutionContext
        {
          WorkingDirectory = Path.GetTempPath(),
          ExecutionTimeout = TimeSpan.FromSeconds(30),
          MaxMemoryBytes = 100 * 1024 * 1024, // 100MB
          EnvironmentVariables = new Dictionary<string, string>(),
          ExecutionParameters = new Dictionary<string, object>(),
          CorrelationId = Guid.NewGuid().ToString()
        };

        var securityContextResult = await securityManager.CreateSecureContextAsync(
            testPluginResult.Value, testContext, cancellationToken);

        if (securityContextResult.IsSuccess)
        {
          await securityManager.ReleaseContextAsync(securityContextResult.Value.ContextId);
          healthCheck.Components["SecurityManager"] = new ComponentHealth
          {
            IsHealthy = true,
            Message = "Security manager operational",
            LastChecked = DateTimeOffset.UtcNow
          };
        }
        else
        {
          healthCheck.Components["SecurityManager"] = new ComponentHealth
          {
            IsHealthy = false,
            Message = $"Security manager context creation failed: {securityContextResult.Error.Message}",
            LastChecked = DateTimeOffset.UtcNow
          };
        }
      }
      else
      {
        healthCheck.Components["SecurityManager"] = new ComponentHealth
        {
          IsHealthy = false,
          Message = "Failed to create test plugin for security manager health check",
          LastChecked = DateTimeOffset.UtcNow
        };
      }
    }
    catch (Exception ex)
    {
      healthCheck.Components["SecurityManager"] = new ComponentHealth
      {
        IsHealthy = false,
        Message = $"Security manager health check failed: {ex.Message}",
        LastChecked = DateTimeOffset.UtcNow
      };
    }
  }

  private static async Task CheckPluginRepositoryHealth(
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

  private static Result<Plugin> CreateTestPlugin()
  {
    try
    {
      return Plugin.Create(
          "HealthCheckTestPlugin",
          new Version(1, 0, 0).ToString(),
          "Test plugin for health checks",
          PluginLanguage.CSharp,
          "test.cs",
          Path.GetTempPath(),
          new List<string> { "test" },
          new Dictionary<string, object>()
      );
    }
    catch (Exception ex)
    {
      return Result<Plugin>.Failure(Error.Failure("HealthCheck.TestPluginCreationFailed", ex.Message));
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