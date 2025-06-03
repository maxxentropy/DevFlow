using DevFlow.Presentation.MCP.Protocol.Handlers;
using DevFlow.Presentation.MCP.Protocol.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol;

/// <summary>
/// Main MCP server that handles protocol communication and request routing.
/// </summary>
public sealed class McpServer
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<McpServer> _logger;
  private readonly Dictionary<string, Type> _handlerMappings;
  private readonly JsonSerializerOptions _jsonOptions;

  public McpServer(IServiceProvider serviceProvider, ILogger<McpServer> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _handlerMappings = InitializeHandlerMappings();
    _jsonOptions = new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      WriteIndented = true,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
  }

  /// <summary>
  /// Processes an incoming MCP request and returns the appropriate response.
  /// </summary>
  /// <param name="requestJson">The JSON-RPC request</param>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>The JSON-RPC response</returns>
  public async Task<string> ProcessRequestAsync(string requestJson, CancellationToken cancellationToken = default)
  {
    _logger.LogDebug("Processing MCP request: {RequestJson}", requestJson);

    try
    {
      // Parse the incoming request
      var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
      if (request is null)
      {
        return CreateErrorResponse("", -32700, "Parse error", "Invalid JSON-RPC request");
      }

      // Route to appropriate handler
      var result = await RouteRequestAsync(request, cancellationToken);

      // Create successful response
      var response = new McpResponse
      {
        Id = request.Id,
        Result = result
      };

      var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
      _logger.LogDebug("MCP response: {ResponseJson}", responseJson);

      return responseJson;
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning(ex, "Invalid request parameters");
      return CreateErrorResponse("", -32602, "Invalid params", ex.Message);
    }
    catch (NotSupportedException ex)
    {
      _logger.LogWarning(ex, "Method not found: {Message}", ex.Message);
      return CreateErrorResponse("", -32601, "Method not found", ex.Message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Internal server error processing MCP request");
      return CreateErrorResponse("", -32603, "Internal error", "An internal server error occurred");
    }
  }

  /// <summary>
  /// Processes a batch of MCP requests.
  /// </summary>
  /// <param name="requestsJson">Array of JSON-RPC requests</param>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>Array of JSON-RPC responses</returns>
  public async Task<string> ProcessBatchRequestAsync(string requestsJson, CancellationToken cancellationToken = default)
  {
    _logger.LogDebug("Processing MCP batch request");

    try
    {
      var requests = JsonSerializer.Deserialize<McpRequest[]>(requestsJson, _jsonOptions);
      if (requests is null || requests.Length == 0)
      {
        return CreateErrorResponse("", -32700, "Parse error", "Invalid JSON-RPC batch request");
      }

      var tasks = requests.Select(request => ProcessSingleRequestAsync(request, cancellationToken));
      var responses = await Task.WhenAll(tasks);

      var batchResponse = JsonSerializer.Serialize(responses, _jsonOptions);
      _logger.LogDebug("MCP batch response completed with {Count} responses", responses.Length);

      return batchResponse;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing batch request");
      return CreateErrorResponse("", -32603, "Internal error", "Batch processing failed");
    }
  }

  /// <summary>
  /// Routes an MCP request to the appropriate handler.
  /// </summary>
  private async Task<object?> RouteRequestAsync(McpRequest request, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Routing MCP request: {Method}", request.Method);

    if (!_handlerMappings.TryGetValue(request.Method, out var handlerType))
    {
      throw new NotSupportedException($"Method '{request.Method}' is not supported");
    }

    var handler = _serviceProvider.GetRequiredService(handlerType) as IMcpRequestHandler;
    if (handler is null)
    {
      throw new InvalidOperationException($"Handler for method '{request.Method}' is not properly registered");
    }

    return await handler.HandleAsync(request, cancellationToken);
  }

  /// <summary>
  /// Processes a single request and returns a response object.
  /// </summary>
  private async Task<McpResponse> ProcessSingleRequestAsync(McpRequest request, CancellationToken cancellationToken)
  {
    try
    {
      var result = await RouteRequestAsync(request, cancellationToken);
      return new McpResponse
      {
        Id = request.Id,
        Result = result
      };
    }
    catch (ArgumentException ex)
    {
      return new McpResponse
      {
        Id = request.Id,
        Error = new McpError
        {
          Code = -32602,
          Message = "Invalid params",
          Data = ex.Message
        }
      };
    }
    catch (NotSupportedException ex)
    {
      return new McpResponse
      {
        Id = request.Id,
        Error = new McpError
        {
          Code = -32601,
          Message = "Method not found",
          Data = ex.Message
        }
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing request {RequestId}", request.Id);
      return new McpResponse
      {
        Id = request.Id,
        Error = new McpError
        {
          Code = -32603,
          Message = "Internal error",
          Data = "An internal server error occurred"
        }
      };
    }
  }

  /// <summary>
  /// Creates a JSON-RPC error response.
  /// </summary>
  private string CreateErrorResponse(string id, int code, string message, string? data = null)
  {
    var errorResponse = new McpResponse
    {
      Id = id,
      Error = new McpError
      {
        Code = code,
        Message = message,
        Data = data
      }
    };

    return JsonSerializer.Serialize(errorResponse, _jsonOptions);
  }

  /// <summary>
  /// Initializes the mapping between MCP methods and their corresponding handlers.
  /// </summary>
  private static Dictionary<string, Type> InitializeHandlerMappings()
  {
    return new Dictionary<string, Type>
    {
      ["initialize"] = typeof(InitializeHandler),
      ["tools/list"] = typeof(ToolsListHandler),
      ["tools/call"] = typeof(ToolsCallHandler),
      ["resources/list"] = typeof(ResourcesListHandler),
      ["resources/read"] = typeof(ResourcesReadHandler),
      ["prompts/list"] = typeof(PromptsListHandler),
      ["prompts/get"] = typeof(PromptsGetHandler)
    };
  }

  /// <summary>
  /// Gets the list of supported MCP methods.
  /// </summary>
  public IEnumerable<string> GetSupportedMethods()
  {
    return _handlerMappings.Keys;
  }

  /// <summary>
  /// Validates that all required handlers are registered in the service container.
  /// </summary>
  public bool ValidateHandlerRegistration()
  {
    try
    {
      foreach (var handlerType in _handlerMappings.Values)
      {
        var handler = _serviceProvider.GetService(handlerType);
        if (handler is null)
        {
          _logger.LogError("Handler {HandlerType} is not registered in the service container", handlerType.Name);
          return false;
        }
      }

      _logger.LogInformation("All MCP handlers are properly registered");
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error validating handler registration");
      return false;
    }
  }

  /// <summary>
  /// Gets server statistics and health information.
  /// </summary>
  public McpServerStats GetServerStats()
  {
    return new McpServerStats
    {
      SupportedMethods = _handlerMappings.Keys.ToList(),
      RegisteredHandlers = _handlerMappings.Count,
      ProtocolVersion = "2024-11-05",
      ServerName = "DevFlow MCP Server",
      ServerVersion = "1.0.0"
    };
  }
}

/// <summary>
/// Represents server statistics and health information.
/// </summary>
public record McpServerStats
{
  public required List<string> SupportedMethods { get; init; }
  public required int RegisteredHandlers { get; init; }
  public required string ProtocolVersion { get; init; }
  public required string ServerName { get; init; }
  public required string ServerVersion { get; init; }
}