namespace DevFlow.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the DevFlow server.
/// </summary>
public sealed class DevFlowOptions
{
  public const string SectionName = "DevFlow";

  /// <summary>
  /// Gets or sets the database connection string.
  /// </summary>
  public string ConnectionString { get; set; } = "Data Source=devflow.db";

  /// <summary>
  /// Gets or sets the plugin configuration.
  /// </summary>
  public PluginOptions Plugins { get; set; } = new();

  /// <summary>
  /// Gets or sets the MCP server configuration.
  /// </summary>
  public McpServerOptions McpServer { get; set; } = new();

  /// <summary>
  /// Gets or sets the logging configuration.
  /// </summary>
  public LoggingOptions Logging { get; set; } = new();
}

/// <summary>
/// Plugin-related configuration options.
/// </summary>
public sealed class PluginOptions
{
  /// <summary>
  /// Gets or sets the plugin directories to scan.
  /// </summary>
  public List<string> PluginDirectories { get; set; } = new() { "./plugins" };

  /// <summary>
  /// Gets or sets whether to enable plugin hot-reload.
  /// </summary>
  public bool EnableHotReload { get; set; } = true;

  /// <summary>
  /// Gets or sets the plugin execution timeout in milliseconds.
  /// </summary>
  public int ExecutionTimeoutMs { get; set; } = 30000;

  /// <summary>
  /// Gets or sets the maximum memory usage per plugin in MB.
  /// </summary>
  public int MaxMemoryMb { get; set; } = 256;
}

/// <summary>
/// MCP server configuration options.
/// </summary>
public sealed class McpServerOptions
{
  /// <summary>
  /// Gets or sets the server name.
  /// </summary>
  public string Name { get; set; } = "DevFlow";

  /// <summary>
  /// Gets or sets the server version.
  /// </summary>
  public string Version { get; set; } = "1.0.0";

  /// <summary>
  /// Gets or sets the server description.
  /// </summary>
  public string Description { get; set; } = "MCP Development Workflow Automation Server";

  /// <summary>
  /// Gets or sets the HTTP port (if HTTP transport is enabled).
  /// </summary>
  public int? HttpPort { get; set; }

  /// <summary>
  /// Gets or sets whether to enable stdio transport.
  /// </summary>
  public bool EnableStdio { get; set; } = true;

  /// <summary>
  /// Gets or sets whether to enable HTTP transport.
  /// </summary>
  public bool EnableHttp { get; set; } = true;

  /// <summary>
  /// Gets or sets whether to enable WebSocket transport.
  /// </summary>
  public bool EnableWebSocket { get; set; } = true;
}

/// <summary>
/// Logging configuration options.
/// </summary>
public sealed class LoggingOptions
{
  /// <summary>
  /// Gets or sets the minimum log level.
  /// </summary>
  public string MinimumLevel { get; set; } = "Information";

  /// <summary>
  /// Gets or sets whether to enable structured logging.
  /// </summary>
  public bool EnableStructuredLogging { get; set; } = true;

  /// <summary>
  /// Gets or sets the log file path.
  /// </summary>
  public string? FilePath { get; set; } = "logs/devflow-.log";

  /// <summary>
  /// Gets or sets whether to enable console logging.
  /// </summary>
  public bool EnableConsole { get; set; } = true;
}