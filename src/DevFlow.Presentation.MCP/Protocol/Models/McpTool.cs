using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Models;

/// <summary>
/// Represents an MCP tool that can be executed.
/// </summary>
public record McpTool
{
  /// <summary>
  /// The tool name.
  /// </summary>
  [JsonPropertyName("name")]
  public required string Name { get; init; }

  /// <summary>
  /// The tool description.
  /// </summary>
  [JsonPropertyName("description")]
  public required string Description { get; init; }

  /// <summary>
  /// The tool input schema (JSON Schema).
  /// </summary>
  [JsonPropertyName("inputSchema")]
  public required object InputSchema { get; init; }
}

/// <summary>
/// Represents the result of a tool execution.
/// </summary>
public record McpToolResult
{
  /// <summary>
  /// The tool execution content.
  /// </summary>
  [JsonPropertyName("content")]
  public required List<McpContent> Content { get; init; } = new();

  /// <summary>
  /// Whether the tool execution was successful.
  /// </summary>
  [JsonPropertyName("isError")]
  public bool IsError { get; init; } = false;
}

/// <summary>
/// Represents content in MCP messages.
/// </summary>
public record McpContent
{
  /// <summary>
  /// The content type.
  /// </summary>
  [JsonPropertyName("type")]
  public required string Type { get; init; }

  /// <summary>
  /// The content text.
  /// </summary>
  [JsonPropertyName("text")]
  public string? Text { get; init; }

  /// <summary>
  /// The content data.
  /// </summary>
  [JsonPropertyName("data")]
  public object? Data { get; init; }
}