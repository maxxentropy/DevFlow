using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;


/// <summary>
/// Middleware to handle MCP protocol-specific concerns
/// </summary>
public class McpProtocolMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<McpProtocolMiddleware> _logger;

  public McpProtocolMiddleware(RequestDelegate next, ILogger<McpProtocolMiddleware> logger)
  {
    _next = next;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    // Add MCP-specific headers for protocol compliance
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
      // Use Append to avoid duplicate key errors
      context.Response.Headers.Append("X-MCP-Server", "DevFlow/1.0.0");
      context.Response.Headers.Append("X-Protocol-Version", "2024-11-05");
      context.Response.Headers.Append("Access-Control-Expose-Headers", "X-MCP-Server,X-Protocol-Version");

      _logger.LogDebug("Processing MCP request: {Method} {Path} from {RemoteIp}",
          context.Request.Method,
          context.Request.Path,
          context.Connection.RemoteIpAddress);
    }

    await _next(context);
  }
}