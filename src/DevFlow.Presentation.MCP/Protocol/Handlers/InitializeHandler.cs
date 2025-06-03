using DevFlow.Presentation.MCP.Protocol.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Handles MCP initialize requests.
/// </summary>
public sealed class InitializeHandler : IMcpRequestHandler
{
  private readonly ILogger<InitializeHandler> _logger;

  public InitializeHandler(ILogger<InitializeHandler> logger)
  {
    _logger = logger;
  }

  public Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Handling MCP initialize request");

    var response = new InitializeResponse
    {
      ProtocolVersion = "2024-11-05",
      Capabilities = new McpServerCapabilities
      {
        Tools = new McpToolsCapability { ListChanged = true },
        Resources = new McpResourcesCapability
        {
          Subscribe = true,
          ListChanged = true
        },
        Prompts = new McpPromptsCapability { ListChanged = true },
        Logging = new McpLoggingCapability()
      },
      ServerInfo = new ServerInfo
      {
        Name = "DevFlow",
        Version = "1.0.0",
        Description = "MCP Development Workflow Automation Server"
      }
    };

    _logger.LogInformation("MCP server initialized successfully");
    return Task.FromResult<object?>(response);
  }

  private record InitializeResponse
  {
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; init; }

    [JsonPropertyName("capabilities")]
    public required McpServerCapabilities Capabilities { get; init; }

    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; init; }
  }

  private record ServerInfo
  {
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
  }
}