// File: src/DevFlow.Presentation.MCP/Protocol/Handlers/ResourcesListHandler.cs
using DevFlow.Presentation.MCP.Protocol.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Handles MCP resources/list requests.
/// </summary>
public sealed class ResourcesListHandler : IMcpRequestHandler
{
    private readonly ILogger<ResourcesListHandler> _logger;

    public ResourcesListHandler(ILogger<ResourcesListHandler> logger)
    {
        _logger = logger;
    }

    public Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling MCP resources/list request");

        var resources = new List<McpResource>
        {
            new()
            {
                Uri = "devflow://workflows",
                Name = "Workflows",
                Description = "All available workflows",
                MimeType = "application/json"
            },
            new()
            {
                Uri = "devflow://plugins",
                Name = "Plugins",
                Description = "All registered plugins",
                MimeType = "application/json"
            },
            new()
            {
                Uri = "devflow://config",
                Name = "Configuration",
                Description = "Server configuration",
                MimeType = "application/json"
            }
        };

        var response = new ResourcesListResponse { Resources = resources };
        
        _logger.LogInformation("Listed {Count} resources", resources.Count);
        return Task.FromResult<object?>(response);
    }

    private record ResourcesListResponse
    {
        [JsonPropertyName("resources")]
        public required List<McpResource> Resources { get; init; }
    }
}