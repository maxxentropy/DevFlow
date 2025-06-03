using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Models;

/// <summary>
/// Represents the capabilities of an MCP server.
/// </summary>
public record McpServerCapabilities
{
  /// <summary>
  /// Tools that the server can execute.
  /// </summary>
  [JsonPropertyName("tools")]
  public McpToolsCapability? Tools { get; init; }

  /// <summary>
  /// Resources that the server can provide.
  /// </summary>
  [JsonPropertyName("resources")]
  public McpResourcesCapability? Resources { get; init; }

  /// <summary>
  /// Prompts that the server can provide.
  /// </summary>
  [JsonPropertyName("prompts")]
  public McpPromptsCapability? Prompts { get; init; }

  /// <summary>
  /// Logging capabilities.
  /// </summary>
  [JsonPropertyName("logging")]
  public McpLoggingCapability? Logging { get; init; }
}

/// <summary>
/// Represents tools capability.
/// </summary>
public record McpToolsCapability
{
  /// <summary>
  /// Whether the server supports listing tools.
  /// </summary>
  [JsonPropertyName("listChanged")]
  public bool ListChanged { get; init; } = true;
}

/// <summary>
/// Represents resources capability.
/// </summary>
public record McpResourcesCapability
{
  /// <summary>
  /// Whether the server supports subscribing to resource changes.
  /// </summary>
  [JsonPropertyName("subscribe")]
  public bool Subscribe { get; init; } = true;

  /// <summary>
  /// Whether the server supports listing resources.
  /// </summary>
  [JsonPropertyName("listChanged")]
  public bool ListChanged { get; init; } = true;
}

/// <summary>
/// Represents prompts capability.
/// </summary>
public record McpPromptsCapability
{
  /// <summary>
  /// Whether the server supports listing prompts.
  /// </summary>
  [JsonPropertyName("listChanged")]
  public bool ListChanged { get; init; } = true;
}

/// <summary>
/// Represents logging capability.
/// </summary>
public record McpLoggingCapability;