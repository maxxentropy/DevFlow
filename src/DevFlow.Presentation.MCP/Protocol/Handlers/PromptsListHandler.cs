using DevFlow.Presentation.MCP.Protocol.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Handles MCP prompts/list requests.
/// </summary>
public sealed class PromptsListHandler : IMcpRequestHandler
{
  private readonly ILogger<PromptsListHandler> _logger;

  public PromptsListHandler(ILogger<PromptsListHandler> logger)
  {
    _logger = logger;
  }

  public Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Handling MCP prompts/list request");

    var prompts = new List<McpPrompt>
        {
            new()
            {
                Name = "create_workflow_prompt",
                Description = "Generate a prompt for creating a new workflow",
                Arguments = new List<McpPromptArgument>
                {
                    new() { Name = "purpose", Description = "The purpose of the workflow", Required = true },
                    new() { Name = "complexity", Description = "The complexity level (simple, medium, complex)", Required = false }
                }
            },
            new()
            {
                Name = "debug_workflow_prompt",
                Description = "Generate a prompt for debugging workflow issues",
                Arguments = new List<McpPromptArgument>
                {
                    new() { Name = "workflowId", Description = "The workflow ID to debug", Required = true },
                    new() { Name = "errorMessage", Description = "The error message", Required = false }
                }
            }
        };

    var response = new PromptsListResponse { Prompts = prompts };

    _logger.LogInformation("Listed {Count} prompts", prompts.Count);
    return Task.FromResult<object?>(response);
  }

  private record PromptsListResponse
  {
    [JsonPropertyName("prompts")]
    public required List<McpPrompt> Prompts { get; init; }
  }

  private record McpPrompt
  {
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("arguments")]
    public List<McpPromptArgument>? Arguments { get; init; }
  }

  private record McpPromptArgument
  {
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }
  }
}