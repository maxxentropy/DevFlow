using DevFlow.Application.Workflows.Commands;
using DevFlow.Application.Workflows.Queries;
using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Presentation.MCP.Protocol.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Handles MCP tools/call requests.
/// </summary>
public sealed class ToolsCallHandler : IMcpRequestHandler
{
  private readonly IMediator _mediator;
  private readonly IPluginRepository _pluginRepository;
  private readonly IPluginExecutionService _pluginExecutionService;
  private readonly ILogger<ToolsCallHandler> _logger;

  public ToolsCallHandler(
      IMediator mediator,
      IPluginRepository pluginRepository,
      IPluginExecutionService pluginExecutionService,
      ILogger<ToolsCallHandler> logger)
  {
    _mediator = mediator;
    _pluginRepository = pluginRepository;
    _pluginExecutionService = pluginExecutionService;
    _logger = logger;
  }

  public async Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Handling MCP tools/call request");

    try
    {
      var callRequest = JsonSerializer.Deserialize<ToolsCallRequest>(
          JsonSerializer.Serialize(request.Params),
          new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      if (callRequest is null)
      {
        throw new ArgumentException("Invalid tools/call request parameters");
      }

      var result = await ExecuteToolAsync(callRequest, cancellationToken);
      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to execute tool");

      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Error executing tool: {ex.Message}"
                    }
                },
        IsError = true
      };
    }
  }

  private async Task<McpToolResult> ExecuteToolAsync(ToolsCallRequest request, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Executing tool: {ToolName}", request.Name);

    return request.Name switch
    {
      // Existing workflow tools
      "create_workflow" => await ExecuteCreateWorkflowAsync(request.Arguments, cancellationToken),
      "start_workflow" => await ExecuteStartWorkflowAsync(request.Arguments, cancellationToken),
      "add_workflow_step" => await ExecuteAddWorkflowStepAsync(request.Arguments, cancellationToken),
      "get_workflow" => await ExecuteGetWorkflowAsync(request.Arguments, cancellationToken),
      "list_workflows" => await ExecuteListWorkflowsAsync(request.Arguments, cancellationToken),

      // Plugin management tools
      "list_plugins" => await ExecuteListPluginsAsync(request.Arguments, cancellationToken),
      "get_plugin_capabilities" => await ExecuteGetPluginCapabilitiesAsync(request.Arguments, cancellationToken),
      "validate_plugin" => await ExecuteValidatePluginAsync(request.Arguments, cancellationToken),
      "discover_plugins" => await ExecuteDiscoverPluginsAsync(request.Arguments, cancellationToken),

      // Dynamic plugin execution
      _ when request.Name.StartsWith("execute_plugin_") => await ExecutePluginAsync(request, cancellationToken),

      _ => throw new ArgumentException($"Unknown tool: {request.Name}")
    };
  }

  private async Task<McpToolResult> ExecuteCreateWorkflowAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var name = arguments?.GetValueOrDefault("name")?.ToString() ?? throw new ArgumentException("Missing 'name' parameter");
    var description = arguments?.GetValueOrDefault("description")?.ToString() ?? throw new ArgumentException("Missing 'description' parameter");

    var command = new CreateWorkflowCommand(name, description);
    var result = await _mediator.Send(command, cancellationToken);

    if (result.IsSuccess)
    {
      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Successfully created workflow '{name}' with ID: {result.Value.Value}"
                    }
                }
      };
    }

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Failed to create workflow: {result.Error.Message}"
                }
            },
      IsError = true
    };
  }

  private async Task<McpToolResult> ExecuteStartWorkflowAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var workflowIdStr = arguments?.GetValueOrDefault("workflowId")?.ToString() ?? throw new ArgumentException("Missing 'workflowId' parameter");
    var workflowId = WorkflowId.From(workflowIdStr);

    var command = new StartWorkflowCommand(workflowId);
    var result = await _mediator.Send(command, cancellationToken);

    if (result.IsSuccess)
    {
      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Successfully started workflow {workflowId.Value}"
                    }
                }
      };
    }

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Failed to start workflow: {result.Error.Message}"
                }
            },
      IsError = true
    };
  }

  private async Task<McpToolResult> ExecuteAddWorkflowStepAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var workflowIdStr = arguments?.GetValueOrDefault("workflowId")?.ToString() ?? throw new ArgumentException("Missing 'workflowId' parameter");
    var stepName = arguments?.GetValueOrDefault("stepName")?.ToString() ?? throw new ArgumentException("Missing 'stepName' parameter");
    var pluginIdStr = arguments?.GetValueOrDefault("pluginId")?.ToString() ?? throw new ArgumentException("Missing 'pluginId' parameter");

    var workflowId = WorkflowId.From(workflowIdStr);
    var pluginId = PluginId.From(pluginIdStr);

    var configuration = arguments?.GetValueOrDefault("configuration") as Dictionary<string, object>;
    var order = arguments?.GetValueOrDefault("order") as int?;

    var command = new AddWorkflowStepCommand(workflowId, stepName, pluginId, configuration, order);
    var result = await _mediator.Send(command, cancellationToken);

    if (result.IsSuccess)
    {
      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Successfully added step '{stepName}' to workflow {workflowId.Value}"
                    }
                }
      };
    }

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Failed to add step: {result.Error.Message}"
                }
            },
      IsError = true
    };
  }

  private async Task<McpToolResult> ExecuteGetWorkflowAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var workflowIdStr = arguments?.GetValueOrDefault("workflowId")?.ToString() ?? throw new ArgumentException("Missing 'workflowId' parameter");
    var workflowId = WorkflowId.From(workflowIdStr);

    var query = new GetWorkflowQuery(workflowId);
    var result = await _mediator.Send(query, cancellationToken);

    if (result.IsSuccess)
    {
      var workflow = result.Value;
      var workflowJson = JsonSerializer.Serialize(workflow, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });

      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Workflow Details:\n{workflowJson}"
                    }
                }
      };
    }

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Failed to get workflow: {result.Error.Message}"
                }
            },
      IsError = true
    };
  }

  private async Task<McpToolResult> ExecuteListWorkflowsAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var pageNumber = arguments?.GetValueOrDefault("pageNumber") as int? ?? 1;
    var pageSize = arguments?.GetValueOrDefault("pageSize") as int? ?? 10;
    var status = arguments?.GetValueOrDefault("status")?.ToString();
    var searchTerm = arguments?.GetValueOrDefault("searchTerm")?.ToString();

    var query = new GetWorkflowsQuery(pageNumber, pageSize, status, searchTerm);
    var result = await _mediator.Send(query, cancellationToken);

    if (result.IsSuccess)
    {
      var workflows = result.Value;
      var workflowsJson = JsonSerializer.Serialize(workflows, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });

      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Workflows (Page {workflows.PageNumber} of {workflows.TotalPages}, Total: {workflows.TotalCount}):\n{workflowsJson}"
                    }
                }
      };
    }

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Failed to list workflows: {result.Error.Message}"
                }
            },
      IsError = true
    };
  }

  private async Task<McpToolResult> ExecuteListPluginsAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var language = arguments?.GetValueOrDefault("language")?.ToString();
    var status = arguments?.GetValueOrDefault("status")?.ToString();

    var plugins = await _pluginRepository.GetAllAsync(cancellationToken);

    var filteredPlugins = plugins.AsEnumerable();

    if (!string.IsNullOrEmpty(language))
    {
      if (Enum.TryParse<PluginLanguage>(language, true, out var lang))
      {
        filteredPlugins = filteredPlugins.Where(p => p.Metadata.Language == lang);
      }
    }

    if (!string.IsNullOrEmpty(status))
    {
      if (Enum.TryParse<PluginStatus>(status, true, out var stat))
      {
        filteredPlugins = filteredPlugins.Where(p => p.Status == stat);
      }
    }

    var pluginList = filteredPlugins.Select(p => new
    {
      id = p.Id.Value.ToString(),
      name = p.Metadata.Name,
      version = p.Metadata.Version.ToString(),
      description = p.Metadata.Description,
      language = p.Metadata.Language.ToString(),
      status = p.Status.ToString(),
      capabilities = p.Capabilities,
      lastExecuted = p.LastExecutedAt,
      executionCount = p.ExecutionCount
    }).ToList();

    var result = JsonSerializer.Serialize(pluginList, new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      WriteIndented = true
    });

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Found {pluginList.Count} plugins:\n{result}"
                }
            }
    };
  }

  private async Task<McpToolResult> ExecuteGetPluginCapabilitiesAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var pluginIdStr = arguments?.GetValueOrDefault("pluginId")?.ToString() ?? throw new ArgumentException("Missing 'pluginId' parameter");
    var pluginId = PluginId.From(pluginIdStr);

    var result = await _pluginExecutionService.GetPluginCapabilitiesAsync(pluginId, cancellationToken);

    if (result.IsSuccess)
    {
      var capabilities = result.Value;
      var capabilitiesJson = JsonSerializer.Serialize(capabilities, new JsonSerializerOptions
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
      });

      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Plugin Capabilities:\n{capabilitiesJson}"
                    }
                }
      };
    }

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Failed to get plugin capabilities: {result.Error.Message}"
                }
            },
      IsError = true
    };
  }

  private async Task<McpToolResult> ExecuteValidatePluginAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var pluginIdStr = arguments?.GetValueOrDefault("pluginId")?.ToString() ?? throw new ArgumentException("Missing 'pluginId' parameter");
    var pluginId = PluginId.From(pluginIdStr);

    var result = await _pluginExecutionService.ValidatePluginExecutionAsync(pluginId, cancellationToken);

    if (result.IsSuccess)
    {
      var isValid = result.Value;
      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Plugin validation result: {(isValid ? "VALID" : "INVALID")}"
                    }
                }
      };
    }

    return new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = $"Failed to validate plugin: {result.Error.Message}"
                }
            },
      IsError = true
    };
  }

  private Task<McpToolResult> ExecuteDiscoverPluginsAsync(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
  {
    var force = arguments?.GetValueOrDefault("force") as bool? ?? false;

    // Plugin discovery happens automatically at startup via PluginRuntimeInitializationService
    // This could trigger a manual re-scan in the future
    return Task.FromResult(new McpToolResult
    {
      Content = new List<McpContent>
            {
                new()
                {
                    Type = "text",
                    Text = "Plugin discovery is currently handled automatically at startup. Manual discovery will be implemented in a future version."
                }
            }
    });
  }

  private async Task<McpToolResult> ExecutePluginAsync(ToolsCallRequest request, CancellationToken cancellationToken)
  {
    try
    {
      // Extract plugin name from tool name (execute_plugin_pluginname)
      var pluginName = request.Name.Substring("execute_plugin_".Length).Replace("_", " ");

      // Find plugin by name
      var plugins = await _pluginRepository.GetAllAsync(cancellationToken);
      var plugin = plugins.FirstOrDefault(p =>
          p.Metadata.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase) ||
          p.Metadata.Name.ToLowerInvariant().Replace(" ", "_") == pluginName);

      if (plugin == null)
      {
        return new McpToolResult
        {
          Content = new List<McpContent>
                    {
                        new()
                        {
                            Type = "text",
                            Text = $"Plugin '{pluginName}' not found"
                        }
                    },
          IsError = true
        };
      }

      // Extract execution parameters
      var inputData = request.Arguments?.GetValueOrDefault("inputData");
      var executionParameters = request.Arguments?.GetValueOrDefault("executionParameters") as Dictionary<string, object>;

      // Execute plugin
      var result = await _pluginExecutionService.ExecutePluginAsync(
          plugin.Id,
          inputData,
          executionParameters,
          cancellationToken);

      if (result.IsSuccess)
      {
        var executionResult = result.Value;
        var output = new
        {
          success = executionResult.IsSuccess,
          output = executionResult.OutputData,
          executionTime = executionResult.ExecutionDuration.TotalMilliseconds,
          memoryUsed = executionResult.PeakMemoryUsageBytes,
          logs = executionResult.Logs
        };

        return new McpToolResult
        {
          Content = new List<McpContent>
                    {
                        new()
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(output, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                WriteIndented = true
                            })
                        }
                    }
        };
      }

      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Plugin execution failed: {result.Error.Message}"
                    }
                },
        IsError = true
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to execute plugin: {ToolName}", request.Name);
      return new McpToolResult
      {
        Content = new List<McpContent>
                {
                    new()
                    {
                        Type = "text",
                        Text = $"Plugin execution error: {ex.Message}"
                    }
                },
        IsError = true
      };
    }
  }

  private record ToolsCallRequest
  {
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; init; }
  }
}