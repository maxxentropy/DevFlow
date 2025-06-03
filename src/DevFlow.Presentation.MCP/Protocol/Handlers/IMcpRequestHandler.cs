using DevFlow.Presentation.MCP.Protocol.Models;

namespace DevFlow.Presentation.MCP.Protocol.Handlers;

/// <summary>
/// Interface for MCP request handlers.
/// </summary>
public interface IMcpRequestHandler
{
  /// <summary>
  /// Handles an MCP request and returns the result.
  /// </summary>
  /// <param name="request">The MCP request</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>The response result</returns>
  Task<object?> HandleAsync(McpRequest request, CancellationToken cancellationToken = default);
}