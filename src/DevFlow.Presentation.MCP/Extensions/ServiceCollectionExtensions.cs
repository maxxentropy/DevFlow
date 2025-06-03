using DevFlow.Presentation.MCP.Protocol;
using DevFlow.Presentation.MCP.Protocol.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DevFlow.Presentation.MCP.Extensions;

/// <summary>
/// Extension methods for registering MCP services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds MCP server and all related services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddMcpServer(this IServiceCollection services)
  {
    return services
        .AddMcpHandlers()
        .AddMcpCore();
  }

  /// <summary>
  /// Adds MCP server and all related services with configuration.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <param name="configure">Configuration action</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddMcpServer(this IServiceCollection services, Action<McpServerOptions> configure)
  {
    services.Configure(configure);
    return services.AddMcpServer();
  }

  /// <summary>
  /// Adds all MCP request handlers to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddMcpHandlers(this IServiceCollection services)
  {
    // Register all MCP request handlers
    services.TryAddScoped<InitializeHandler>();
    services.TryAddScoped<ToolsListHandler>();
    services.TryAddScoped<ToolsCallHandler>();
    services.TryAddScoped<ResourcesListHandler>();
    services.TryAddScoped<ResourcesReadHandler>();
    services.TryAddScoped<PromptsListHandler>();
    services.TryAddScoped<PromptsGetHandler>();

    return services;
  }

  /// <summary>
  /// Adds core MCP services to the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for method chaining</returns>
  public static IServiceCollection AddMcpCore(this IServiceCollection services)
  {
    // Register the main MCP server
    services.TryAddScoped<McpServer>();

    // Register MCP server factory for creating configured instances
    services.TryAddScoped<IMcpServerFactory, McpServerFactory>();

    // Register MCP server health check
    services.TryAddScoped<IMcpServerHealthCheck, McpServerHealthCheck>();

    return services;
  }

  /// <summary>
  /// Adds MCP logging configuration.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <param name="configure">Logging configuration action</param>
  /// <returns>The service collection for method chaining</returns>
  //public static IServiceCollection AddMcpLogging(this IServiceCollection services, Action<ILoggingBuilder>? configure = null)
  //{
  //  services.AddLogging(builder =>
  //  {
  //    builder.AddConsole();
  //    builder.AddDebug();
  //    configure?.Invoke(builder);
  //  });

  //  return services;
  //}

  /// <summary>
  /// Validates that all required MCP services are properly registered.
  /// </summary>
  /// <param name="serviceProvider">The service provider</param>
  /// <returns>True if all services are properly registered</returns>
  public static bool ValidateMcpServices(this IServiceProvider serviceProvider)
  {
    try
    {
      var logger = serviceProvider.GetRequiredService<ILogger<McpServer>>();
      logger.LogInformation("Validating MCP service registration...");

      // Validate core services
      var mcpServer = serviceProvider.GetRequiredService<McpServer>();
      var serverFactory = serviceProvider.GetRequiredService<IMcpServerFactory>();
      var healthCheck = serviceProvider.GetRequiredService<IMcpServerHealthCheck>();

      // Validate all handlers
      var handlers = new IMcpRequestHandler[]
      {
        serviceProvider.GetRequiredService<InitializeHandler>(),
        serviceProvider.GetRequiredService<ToolsListHandler>(),
        serviceProvider.GetRequiredService<ToolsCallHandler>(),
        serviceProvider.GetRequiredService<ResourcesListHandler>(),
        serviceProvider.GetRequiredService<ResourcesReadHandler>(),
        serviceProvider.GetRequiredService<PromptsListHandler>(),
        serviceProvider.GetRequiredService<PromptsGetHandler>()
      };

      // Validate server handler registration
      var isValid = mcpServer.ValidateHandlerRegistration();

      logger.LogInformation("MCP service validation completed: {IsValid}", isValid ? "SUCCESS" : "FAILED");
      return isValid;
    }
    catch (Exception ex)
    {
      var logger = serviceProvider.GetService<ILogger<McpServer>>();
      logger?.LogError(ex, "MCP service validation failed");
      return false;
    }
  }
}

/// <summary>
/// Configuration options for the MCP server.
/// </summary>
public class McpServerOptions
{
  /// <summary>
  /// The server name to report in capabilities.
  /// </summary>
  public string ServerName { get; set; } = "DevFlow MCP Server";

  /// <summary>
  /// The server version to report in capabilities.
  /// </summary>
  public string ServerVersion { get; set; } = "1.0.0";

  /// <summary>
  /// The server description to report in capabilities.
  /// </summary>
  public string ServerDescription { get; set; } = "MCP Development Workflow Automation Server";

  /// <summary>
  /// The MCP protocol version to use.
  /// </summary>
  public string ProtocolVersion { get; set; } = "2024-11-05";

  /// <summary>
  /// Whether to enable detailed logging for debugging.
  /// </summary>
  public bool EnableDetailedLogging { get; set; } = false;

  /// <summary>
  /// Whether to enable request/response logging.
  /// </summary>
  public bool EnableRequestResponseLogging { get; set; } = false;

  /// <summary>
  /// Maximum number of concurrent requests to process.
  /// </summary>
  public int MaxConcurrentRequests { get; set; } = 100;

  /// <summary>
  /// Request timeout in milliseconds.
  /// </summary>
  public int RequestTimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Factory interface for creating MCP server instances.
/// </summary>
public interface IMcpServerFactory
{
  /// <summary>
  /// Creates a new MCP server instance.
  /// </summary>
  /// <returns>A configured MCP server instance</returns>
  McpServer CreateServer();
}

/// <summary>
/// Default implementation of the MCP server factory.
/// </summary>
public class McpServerFactory : IMcpServerFactory
{
  private readonly IServiceProvider _serviceProvider;

  public McpServerFactory(IServiceProvider serviceProvider)
  {
    _serviceProvider = serviceProvider;
  }

  public McpServer CreateServer()
  {
    return _serviceProvider.GetRequiredService<McpServer>();
  }
}

/// <summary>
/// Interface for MCP server health checking.
/// </summary>
public interface IMcpServerHealthCheck
{
  /// <summary>
  /// Performs a health check on the MCP server.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>Health check result</returns>
  Task<McpHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of MCP server health check.
/// </summary>
public class McpServerHealthCheck : IMcpServerHealthCheck
{
  private readonly McpServer _mcpServer;
  private readonly ILogger<McpServerHealthCheck> _logger;

  public McpServerHealthCheck(McpServer mcpServer, ILogger<McpServerHealthCheck> logger)
  {
    _mcpServer = mcpServer;
    _logger = logger;
  }

  public async Task<McpHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      // Validate handler registration
      var handlersValid = _mcpServer.ValidateHandlerRegistration();
      if (!handlersValid)
      {
        return new McpHealthCheckResult
        {
          IsHealthy = false,
          Message = "One or more MCP handlers are not properly registered",
          Timestamp = DateTimeOffset.UtcNow
        };
      }

      // Get server stats
      var stats = _mcpServer.GetServerStats();

      // Perform a simple test request (initialize)
      var testRequest = """
                {
                    "jsonrpc": "2.0",
                    "id": "health-check",
                    "method": "initialize",
                    "params": {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {},
                        "clientInfo": {
                            "name": "health-check",
                            "version": "1.0.0"
                        }
                    }
                }
                """;

      var response = await _mcpServer.ProcessRequestAsync(testRequest, cancellationToken);

      return new McpHealthCheckResult
      {
        IsHealthy = !string.IsNullOrEmpty(response) && !response.Contains("error"),
        Message = $"MCP server is healthy. Handlers: {stats.RegisteredHandlers}, Methods: {stats.SupportedMethods.Count}",
        Timestamp = DateTimeOffset.UtcNow,
        ServerStats = stats
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "MCP server health check failed");
      return new McpHealthCheckResult
      {
        IsHealthy = false,
        Message = $"Health check failed: {ex.Message}",
        Timestamp = DateTimeOffset.UtcNow
      };
    }
  }
}

/// <summary>
/// Result of an MCP server health check.
/// </summary>
public record McpHealthCheckResult
{
  /// <summary>
  /// Whether the server is healthy.
  /// </summary>
  public required bool IsHealthy { get; init; }

  /// <summary>
  /// Health check message.
  /// </summary>
  public required string Message { get; init; }

  /// <summary>
  /// Timestamp of the health check.
  /// </summary>
  public required DateTimeOffset Timestamp { get; init; }

  /// <summary>
  /// Server statistics (optional).
  /// </summary>
  public McpServerStats? ServerStats { get; init; }
}