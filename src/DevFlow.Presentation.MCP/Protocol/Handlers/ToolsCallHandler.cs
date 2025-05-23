// File: src/DevFlow.Presentation.MCP/Protocol/Handlers/ToolsCallHandler.cs
using DevFlow.Application.Workflows.Commands;
using DevFlow.Application.Workflows.Queries;
using DevFlow.Domain.Common;
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
    private readonly ILogger<ToolsCallHandler> _logger;

    public ToolsCallHandler(IMediator mediator, ILogger<ToolsCallHandler> logger)
    {
        _mediator = mediator;
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
            "create_workflow" => await ExecuteCreateWorkflowAsync(request.Arguments, cancellationToken),
            "start_workflow" => await ExecuteStartWorkflowAsync(request.Arguments, cancellationToken),
            "add_workflow_step" => await ExecuteAddWorkflowStepAsync(request.Arguments, cancellationToken),
            "get_workflow" => await ExecuteGetWorkflowAsync(request.Arguments, cancellationToken),
            "list_workflows" => await ExecuteListWorkflowsAsync(request.Arguments, cancellationToken),
            _ when request.Name.StartsWith("plugin_") => await ExecutePluginAsync(request, cancellationToken),
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
            Is = true
        };
    }

    private async Task<McpToolResult> ExecutePluginAsync(ToolsCallRequest request, CancellationToken cancellationToken)
    {
        // For now, return a placeholder - plugin execution will be implemented with the plugin engine
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new()
                {
                    Type = "text", 
                    Text = $"Plugin execution for '{request.Name}' is not yet implemented. Plugin engine development is in progress."
                }
            }
        };
    }

    private record ToolsCallRequest
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("arguments")]
        public Dictionary<string, object>? Arguments { get; init; }
    }
}