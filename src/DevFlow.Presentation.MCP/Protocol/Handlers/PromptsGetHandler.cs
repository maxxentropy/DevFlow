using DevFlow.Presentation.MCP.Protocol.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Handles MCP prompts/get requests.
/// </summary>
public sealed class PromptsGetHandler : IMcpRequestHandler
{
  private readonly ILogger<PromptsGetHandler> _logger;

  public PromptsGetHandler(ILogger<PromptsGetHandler> logger)
  {
    _logger = logger;
  }

  public Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Handling MCP prompts/get request");

    try
    {
      var getRequest = JsonSerializer.Deserialize<PromptsGetRequest>(
          JsonSerializer.Serialize(request.Params),
          new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      if (getRequest?.Name is null)
      {
        throw new ArgumentException("Missing 'name' parameter");
      }

      var promptContent = GetPromptContent(getRequest.Name, getRequest.Arguments);

      var response = new PromptsGetResponse
      {
        Description = GetPromptDescription(getRequest.Name),
        Messages = new List<McpPromptMessage>
                {
                    new()
                    {
                        Role = "user",
                        Content = new McpContent
                        {
                            Type = "text",
                            Text = promptContent
                        }
                    }
                }
      };

      _logger.LogInformation("Generated prompt content for '{PromptName}'", getRequest.Name);
      return Task.FromResult<object?>(response);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to get prompt");
      throw;
    }
  }

  private string GetPromptContent(string promptName, Dictionary<string, object>? arguments)
  {
    return promptName switch
    {
      "create_workflow_prompt" => GenerateCreateWorkflowPrompt(arguments),
      "debug_workflow_prompt" => GenerateDebugWorkflowPrompt(arguments),
      _ => throw new ArgumentException($"Unknown prompt: {promptName}")
    };
  }

  private string GetPromptDescription(string promptName)
  {
    return promptName switch
    {
      "create_workflow_prompt" => "Generate a prompt for creating a new workflow",
      "debug_workflow_prompt" => "Generate a prompt for debugging workflow issues",
      _ => "Unknown prompt"
    };
  }

  private string GenerateCreateWorkflowPrompt(Dictionary<string, object>? arguments)
  {
    var purpose = arguments?.GetValueOrDefault("purpose")?.ToString() ?? "general automation";
    var complexity = arguments?.GetValueOrDefault("complexity")?.ToString() ?? "medium";

    var prompt = $"""
            I need to create a new development workflow with the following requirements:

            **Purpose**: {purpose}
            **Complexity Level**: {complexity}

            Please help me design a workflow that includes:

            1. **Workflow Structure**:
               - Clear step definitions with input/output specifications
               - Appropriate error handling and rollback mechanisms
               - Dependencies between steps (sequential vs parallel execution)

            2. **Implementation Approach**:
               - Recommended plugins or tools for each step
               - Configuration parameters and their default values
               - Resource requirements and constraints

            3. **Quality & Validation**:
               - Testing strategy for the workflow
               - Monitoring and logging requirements
               - Success criteria and failure conditions

            4. **Integration Points**:
               - External systems or APIs that need to be integrated
               - Authentication and authorization requirements
               - Data transformation and validation needs

            Please provide a detailed workflow design with specific implementation guidance, including code examples where appropriate. Consider best practices for maintainability, security, and performance.
            """;

    return prompt;
  }

  private string GenerateDebugWorkflowPrompt(Dictionary<string, object>? arguments)
  {
    var workflowId = arguments?.GetValueOrDefault("workflowId")?.ToString() ?? "[WORKFLOW_ID]";
    var errorMessage = arguments?.GetValueOrDefault("errorMessage")?.ToString();

    var prompt = $"""
            I need help debugging a workflow that is experiencing issues.

            **Workflow ID**: {workflowId}
            {(errorMessage != null ? $"**Error Message**: {errorMessage}" : "")}

            Please help me systematically diagnose and resolve the issue by:

            1. **Initial Analysis**:
               - Review the workflow configuration and step definitions
               - Identify potential points of failure based on the error message
               - Check for common configuration issues or missing dependencies

            2. **Debugging Strategy**:
               - Recommend specific logging points to add for better visibility
               - Suggest debugging tools or techniques for each workflow step
               - Identify data validation checkpoints and assertion strategies

            3. **Root Cause Investigation**:
               - Analyze the execution flow and identify where the failure occurred
               - Check for resource constraints, timeout issues, or permission problems
               - Examine input data quality and transformation logic

            4. **Resolution Plan**:
               - Provide specific steps to fix the identified issues
               - Suggest preventive measures to avoid similar problems in the future
               - Recommend improvements to error handling and recovery mechanisms

            5. **Testing & Validation**:
               - Create a test plan to verify the fix works correctly
               - Suggest edge cases and error scenarios to test
               - Recommend monitoring and alerting improvements

            Please provide detailed, actionable guidance with specific commands, code snippets, or configuration changes where applicable.
            """;

    return prompt;
  }

  private record PromptsGetRequest
  {
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; init; }
  }

  private record PromptsGetResponse
  {
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("messages")]
    public required List<McpPromptMessage> Messages { get; init; }
  }

  private record McpPromptMessage
  {
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required McpContent Content { get; init; }
  }
}