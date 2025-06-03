using DevFlow.Application.Plugins;
using DevFlow.Presentation.MCP.Protocol.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Handles MCP tools/list requests.
/// </summary>
public sealed class ToolsListHandler : IMcpRequestHandler
{
  private readonly IPluginRepository _pluginRepository;
  private readonly ILogger<ToolsListHandler> _logger;

  public ToolsListHandler(IPluginRepository pluginRepository, ILogger<ToolsListHandler> logger)
  {
    _pluginRepository = pluginRepository;
    _logger = logger;
  }

  public async Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Handling MCP tools/list request");

    try
    {
      var plugins = await _pluginRepository.GetAllAsync(cancellationToken);

      var tools = plugins
          .Where(p => p.Status == DevFlow.Domain.Plugins.Enums.PluginStatus.Available)
          .Select(p => new McpTool
          {
            Name = $"plugin_{p.Metadata.Name.ToLowerInvariant()}",
            Description = $"{p.Metadata.Description} (Language: {p.Metadata.Language})",
            InputSchema = new
            {
              type = "object",
              properties = new
              {
                configuration = new
                {
                  type = "object",
                  description = "Plugin configuration parameters",
                  additionalProperties = true
                },
                workflowId = new
                {
                  type = "string",
                  description = "Target workflow ID (optional for standalone execution)"
                }
              }
            }
          })
          .ToList();

      // Add built-in workflow management tools
      tools.AddRange(GetBuiltInTools());

      var response = new ToolsListResponse { Tools = tools };

      _logger.LogInformation("Listed {Count} available tools", tools.Count);
      return response;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to list tools");
      throw;
    }
  }

  private static List<McpTool> GetBuiltInTools()
  {
    return new List<McpTool>
        {
            new()
            {
                Name = "create_workflow",
                Description = "Create a new workflow with the specified name and description",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "The workflow name" },
                        description = new { type = "string", description = "The workflow description" }
                    },
                    required = new[] { "name", "description" }
                }
            },
            new()
            {
                Name = "start_workflow",
                Description = "Start execution of a workflow",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workflowId = new { type = "string", description = "The workflow ID to start" }
                    },
                    required = new[] { "workflowId" }
                }
            },
            new()
            {
                Name = "add_workflow_step",
                Description = "Add a step to a workflow",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workflowId = new { type = "string", description = "The workflow ID" },
                        stepName = new { type = "string", description = "The step name" },
                        pluginId = new { type = "string", description = "The plugin ID to execute" },
                        configuration = new { type = "object", description = "Step configuration", additionalProperties = true },
                        order = new { type = "integer", description = "Execution order (optional)" }
                    },
                    required = new[] { "workflowId", "stepName", "pluginId" }
                }
            },
            new()
            {
                Name = "get_workflow",
                Description = "Get details of a specific workflow",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workflowId = new { type = "string", description = "The workflow ID" }
                    },
                    required = new[] { "workflowId" }
                }
            },
            new()
            {
                Name = "list_workflows",
                Description = "List workflows with optional filtering",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pageNumber = new { type = "integer", description = "Page number (default: 1)" },
                        pageSize = new { type = "integer", description = "Page size (default: 10)" },
                        status = new { type = "string", description = "Filter by status" },
                        searchTerm = new { type = "string", description = "Search term for name/description" }
                    }
                }
            }
        };
  }

  private record ToolsListResponse
  {
    [JsonPropertyName("tools")]
    public required List<McpTool> Tools { get; init; }
  }
}