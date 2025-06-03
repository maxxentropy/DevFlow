using System.Text.Json.Serialization;

namespace DevFlow.Presentation.MCP.Protocol.Models;

/// <summary>
/// Base class for all MCP protocol messages.
/// Follows JSON-RPC 2.0 specification.
/// </summary>
public abstract record McpMessage
{
  /// <summary>
  /// The JSON-RPC version (always "2.0").
  /// </summary>
  [JsonPropertyName("jsonrpc")]
  public string JsonRpc { get; init; } = "2.0";
}

/// <summary>
/// Represents an MCP request message.
/// </summary>
public record McpRequest : McpMessage
{
  /// <summary>
  /// The request identifier.
  /// </summary>
  [JsonPropertyName("id")]
  public required string Id { get; init; }

  /// <summary>
  /// The method name to invoke.
  /// </summary>
  [JsonPropertyName("method")]
  public required string Method { get; init; }

  /// <summary>
  /// The request parameters.
  /// </summary>
  [JsonPropertyName("params")]
  public object? Params { get; init; }
}

/// <summary>
/// Represents an MCP response message.
/// </summary>
public record McpResponse : McpMessage
{
  /// <summary>
  /// The request identifier this response corresponds to.
  /// </summary>
  [JsonPropertyName("id")]
  public required string Id { get; init; }

  /// <summary>
  /// The response result (present if successful).
  /// </summary>
  [JsonPropertyName("result")]
  public object? Result { get; init; }

  /// <summary>
  /// The error information (present if failed).
  /// </summary>
  [JsonPropertyName("error")]
  public McpError? Error { get; init; }
}

/// <summary>
/// Represents an MCP notification message (no response expected).
/// </summary>
public record McpNotification : McpMessage
{
  /// <summary>
  /// The method name.
  /// </summary>
  [JsonPropertyName("method")]
  public required string Method { get; init; }

  /// <summary>
  /// The notification parameters.
  /// </summary>
  [JsonPropertyName("params")]
  public object? Params { get; init; }
}

/// <summary>
/// Represents an error in an MCP response.
/// </summary>
public record McpError
{
  /// <summary>
  /// The error code.
  /// </summary>
  [JsonPropertyName("code")]
  public required int Code { get; init; }

  /// <summary>
  /// The error message.
  /// </summary>
  [JsonPropertyName("message")]
  public required string Message { get; init; }

  /// <summary>
  /// Additional error data.
  /// </summary>
  [JsonPropertyName("data")]
  public object? Data { get; init; }
}