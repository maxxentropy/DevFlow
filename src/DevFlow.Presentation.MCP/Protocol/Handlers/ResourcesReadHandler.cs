// File: src/DevFlow.Presentation.MCP/Protocol/Handlers/ResourcesReadHandler.cs
using DevFlow.Application.Workflows.Queries;
using DevFlow.Presentation.MCP.Protocol.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Handles MCP resources/read requests.
/// </summary>
public sealed class ResourcesReadHandler : IMcpRequestHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<ResourcesReadHandler> _logger;

    public ResourcesReadHandler(IMediator mediator, ILogger<ResourcesReadHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling MCP resources/read request");

        try
        {
            var readRequest = JsonSerializer.Deserialize<ResourcesReadRequest>(
                JsonSerializer.Serialize(request.Params),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            if (readRequest?.Uri is null)
            {
                throw new ArgumentException("Missing 'uri' parameter");
            }

            var contents = await ReadResourceAsync(readRequest.Uri, cancellationToken);
            
            return new McpResourceContents
            {
                Uri = readRequest.Uri,
                Contents = contents
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read resource");
            throw;
        }
    }

    private async Task<List<McpContent>> ReadResourceAsync(string uri, CancellationToken cancellationToken)
    {
        return uri switch
        {
            "devflow://workflows" => await ReadWorkflowsResourceAsync(cancellationToken),
            "devflow://plugins" => await ReadPluginsResourceAsync(cancellationToken),
            "devflow://config" => ReadConfigurationResource(),
            _ => throw new ArgumentException($"Unknown resource URI: {uri}")
        };
    }

    private async Task<List<McpContent>> ReadWorkflowsResourceAsync(CancellationToken cancellationToken)
    {
        var query = new GetWorkflowsQuery(1, 100); // Get first 100 workflows
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            var workflowsJson = JsonSerializer.Serialize(result.Value, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });

            return new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = workflowsJson
                }
            };
        }

        return new List<McpContent>
        {
            new()
            {
                Type = "text",
                Text = $"Error reading workflows: {result.Error.Message}"
            }
        };
    }

    private Task<List<McpContent>> ReadPluginsResourceAsync(CancellationToken cancellationToken)
    {
        // TODO: Implement plugin listing when plugin repository is available
        return Task.FromResult(new List<McpContent>
        {
            new()
            {
                Type = "text",
                Text = "Plugin listing not yet implemented"
            }
        });
    }

    private List<McpContent> ReadConfigurationResource()
    {
        var config = new
        {
            serverName = "DevFlow MCP Server",
            version = "1.0.0",
            supportedLanguages = new[] { "CSharp", "TypeScript", "Python" },
            capabilities = new[] { "workflows", "plugins", "automation" }
        };

        var configJson = JsonSerializer.Serialize(config, 
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });

        return new List<McpContent>
        {
            new()
            {
                Type = "text",
                Text = configJson
            }
        };
    }

    private record ResourcesReadRequest
    {
        [JsonPropertyName("uri")]
        public string? Uri { get; init; }
    }
}