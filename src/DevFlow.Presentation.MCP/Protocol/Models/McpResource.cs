using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Models;

/// <summary>
/// Represents an MCP resource.
/// </summary>
public record McpResource
{
  /// <summary>
  /// The resource URI.
  /// </summary>
  [JsonPropertyName("uri")]
  public required string Uri { get; init; }

  /// <summary>
  /// The resource name.
  /// </summary>
  [JsonPropertyName("name")]
  public required string Name { get; init; }

  /// <summary>
  /// The resource description.
  /// </summary>
  [JsonPropertyName("description")]
  public string? Description { get; init; }

  /// <summary>
  /// The resource MIME type.
  /// </summary>
  [JsonPropertyName("mimeType")]
  public string? MimeType { get; init; }
}

/// <summary>
/// Represents the contents of a resource.
/// </summary>
public record McpResourceContents
{
  /// <summary>
  /// The resource URI.
  /// </summary>
  [JsonPropertyName("uri")]
  public required string Uri { get; init; }

  /// <summary>
  /// The resource contents.
  /// </summary>
  [JsonPropertyName("contents")]
  public required List<McpContent> Contents { get; init; } = new();
}