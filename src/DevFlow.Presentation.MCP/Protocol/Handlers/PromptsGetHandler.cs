// File: src/DevFlow.Presentation.MCP/Protocol/Handlers/PromptsGetHandler.cs
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

            var promptContent = GeneratePromptContent(getRequest.Name, getRequest.Arguments);

            var response = new PromptsGetResponse
            {
                Description = $"Generated prompt for {getRequest.Name}",
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

            _logger.LogInformation("Generated prompt for: {PromptName}", getRequest.Name);
            return Task.FromResult<object?>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate prompt");
            throw;
        }
    }

    private string GeneratePromptContent(string promptName, Dictionary<string, object>? arguments)
    {
        return promptName switch
        {
            "create_workflow_prompt" => GenerateCreateWorkflowPrompt(arguments),
            "debug_workflow_prompt" => GenerateDebugWorkflowPrompt(arguments),
            _ => throw new ArgumentException($"Unknown prompt: {promptName}")
        };
    }

    private string GenerateCreateWorkflowPrompt(Dictionary<string, object>? arguments)
    {
        var purpose = arguments?.GetValueOrDefault("purpose")?.ToString() ?? "general automation";
        var complexity = arguments?.GetValueOrDefault("complexity")?.ToString() ?? "medium";

        return $@"I need help creating a workflow for {purpose}. The workflow should be {complexity} in complexity.

Please provide:
1. A descriptive name for the workflow
2. A detailed description of what the workflow should accomplish
3. Suggested steps that should be included in the workflow
4. Any specific plugins or tools that might be needed
5. Expected inputs and outputs for each step

Consider best practices for workflow design and maintainability.";
    }

    private string GenerateDebugWorkflowPrompt(Dictionary<string, object>? arguments)
    {
        var workflowId = arguments?.GetValueOrDefault("workflowId")?.ToString() ?? "unknown";
        var errorMessage = arguments?.GetValueOrDefault("errorMessage")?.ToString();

        var prompt = $@"I need help debugging a workflow with ID: {workflowId}";

        if (!string.IsNullOrEmpty(errorMessage))
        {
            prompt += $@"

The workflow is failing with this error: {errorMessage}";
        }

        prompt += @"

Please help me:
1. Identify potential causes of the issue
2. Suggest debugging steps to isolate the problem
3. Recommend fixes or workarounds
4. Provide guidance on preventing similar issues in the future

Include specific commands or tools I can use to investigate the problem.";

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
        public required string Description { get; init; }

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
