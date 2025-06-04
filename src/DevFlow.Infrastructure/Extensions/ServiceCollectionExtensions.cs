using DevFlow.Application.Plugins;
using DevFlow.Application.Workflows;
using DevFlow.Infrastructure.Configuration;
using DevFlow.Infrastructure.Persistence;
using DevFlow.Infrastructure.Persistence.Repositories;
using DevFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DevFlow.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds infrastructure services to the service collection.
  /// </summary>
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    // Configure DevFlowOptions
    services.Configure<DevFlowOptions>(
        configuration.GetSection(DevFlowOptions.SectionName));

    // Configure database
    services.AddDbContext<DevFlowDbContext>((serviceProvider, options) =>
    {
      var devFlowOptions = serviceProvider.GetRequiredService<IOptions<DevFlowOptions>>().Value;
      options.UseSqlite(devFlowOptions.ConnectionString);

      // Enable sensitive data logging in development
      if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development")
      {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
      }
    });

    // Register repositories
    services.AddScoped<IWorkflowRepository, WorkflowRepository>();
    services.AddScoped<IPluginRepository, PluginRepository>();

    // Register services
    services.AddScoped<IDbInitializer, DbInitializer>();
    // Add plugin system
    services.AddPluginSystem();

    return services;
  }

  //public static IServiceCollection AddPluginSystem(this IServiceCollection services)
  //{
  //  return services
  //      .AddPluginRuntime()
  //      .AddPluginIntegration();
  //}

  private static IServiceCollection AddPluginIntegration(this IServiceCollection services)
  {
    // Initialize plugin runtime managers on startup
    services.AddHostedService<PluginRuntimeInitializationService>();

    // Add plugin directory configuration
    services.Configure<PluginOptions>(options =>
    {
      options.PluginDirectories = new List<string> { "./plugins" };
      options.EnableHotReload = true;
      options.ExecutionTimeoutMs = 30000;
      options.MaxMemoryMb = 256;
    });

    return services;
  }
}