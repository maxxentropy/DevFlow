using DevFlow.Application.Plugins;
using DevFlow.Domain.Plugins.Enums;
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
      var tools = new List<McpTool>();

      // Add dynamic plugin execution tools
      var pluginTools = await GetPluginExecutionToolsAsync(cancellationToken);
      tools.AddRange(pluginTools);

      // Add built-in workflow management tools
      tools.AddRange(GetWorkflowManagementTools());

      // Add plugin management tools
      tools.AddRange(GetPluginManagementTools());

      var response = new ToolsListResponse { Tools = tools };

      _logger.LogInformation("Listed {Count} available tools ({PluginCount} plugin tools, {ManagementCount} management tools)",
          tools.Count, pluginTools.Count, tools.Count - pluginTools.Count);

      return response;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to list tools");
      throw;
    }
  }

  private async Task<List<McpTool>> GetPluginExecutionToolsAsync(CancellationToken cancellationToken)
  {
    try
    {
      var plugins = await _pluginRepository.GetAllAsync(cancellationToken);

      return plugins
          .Where(p => p.Status == PluginStatus.Available)
          .Select(p => new McpTool
          {
            Name = $"execute_plugin_{SanitizePluginName(p.Metadata.Name)}",
            Description = $"Execute {p.Metadata.Name} plugin - {p.Metadata.Description} (Language: {p.Metadata.Language})",
            InputSchema = new
            {
              type = "object",
              properties = new
              {
                inputData = new
                {
                  type = "object",
                  description = "Input data to pass to the plugin",
                  additionalProperties = true
                },
                executionParameters = new
                {
                  type = "object",
                  description = "Execution parameters to override plugin defaults",
                  additionalProperties = true,
                  properties = CreateExecutionParametersSchema(p)
                }
              }
            }
          })
          .ToList();
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to load plugin execution tools");
      return new List<McpTool>();
    }
  }

  private static object CreateExecutionParametersSchema(Domain.Plugins.Entities.Plugin plugin)
  {
    var schema = new Dictionary<string, object>
    {
      ["workingDirectory"] = new
      {
        type = "string",
        description = "Working directory for plugin execution (optional)"
      },
      ["executionTimeout"] = new
      {
        type = "integer",
        description = "Maximum execution time in seconds (default: 300)"
      },
      ["maxMemoryMb"] = new
      {
        type = "integer",
        description = "Maximum memory usage in MB (default: 256)"
      }
    };

    // Add plugin-specific configuration options from the plugin's default configuration
    foreach (var configItem in plugin.Configuration)
    {
      schema[configItem.Key] = new
      {
        type = InferJsonSchemaType(configItem.Value),
        description = $"Plugin configuration parameter: {configItem.Key}"
      };
    }

    return schema;
  }

  private static string InferJsonSchemaType(object value)
  {
    return value switch
    {
      bool => "boolean",
      int or long or short or byte => "integer",
      float or double or decimal => "number",
      string => "string",
      Array or IEnumerable<object> => "array",
      _ => "object"
    };
  }

  private static string SanitizePluginName(string pluginName)
  {
    return pluginName
        .ToLowerInvariant()
        .Replace(" ", "_")
        .Replace("-", "_")
        .Replace(".", "_");
  }

  private static List<McpTool> GetWorkflowManagementTools()
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

  private static List<McpTool> GetPluginManagementTools()
  {
    return new List<McpTool>
        {
            new()
            {
                Name = "list_plugins",
                Description = "List all registered plugins with their status and capabilities",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        language = new
                        {
                            type = "string",
                            description = "Filter by programming language (CSharp, TypeScript, Python)"
                        },
                        status = new
                        {
                            type = "string",
                            description = "Filter by plugin status (Available, Error, Disabled, Registered)"
                        }
                    }
                }
            },
            new()
            {
                Name = "get_plugin_capabilities",
                Description = "Get detailed execution capabilities for a specific plugin",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pluginId = new { type = "string", description = "The plugin ID" }
                    },
                    required = new[] { "pluginId" }
                }
            },
            new()
            {
                Name = "validate_plugin",
                Description = "Validate that a plugin can be executed",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pluginId = new { type = "string", description = "The plugin ID" }
                    },
                    required = new[] { "pluginId" }
                }
            },
            new()
            {
                Name = "discover_plugins",
                Description = "Trigger manual plugin discovery (plugins are auto-discovered at startup)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        force = new
                        {
                            type = "boolean",
                            description = "Force re-scan even if plugins haven't changed (default: false)"
                        }
                    }
                }
            }
        };
  }

  public record ToolsListResponse
  {
    [JsonPropertyName("tools")]
    public required List<McpTool> Tools { get; init; }
  }
}