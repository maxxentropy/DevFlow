# DevFlow Plugin Execution Testing

This document explains how to test the DevFlow MCP Server and plugin execution functionality.

## Prerequisites

- .NET 8.0 SDK installed
- PowerShell 7+ (for test scripts)
- DevFlow server running on port 5000 (default)

## Starting the Server

1. Navigate to the Host project:
   ```bash
   cd src/DevFlow.Host
   ```

2. Start the server:
   ```bash
   dotnet run
   ```

3. The server will start on `http://localhost:5000` by default

## Running Tests

### Quick Test (Recommended)

For a fast validation that everything is working:

```powershell
# From the repository root
.\Test-Simple.ps1
```

This script will:
- ✅ Check server health
- ✅ List registered plugins  
- ✅ Execute the HelloWorld plugin

### Comprehensive Test

For detailed testing with full output:

```powershell
# From the repository root
.\Test-PluginExecution.ps1
```

Optional parameters:
```powershell
.\Test-PluginExecution.ps1 -ServerUrl "http://localhost:5000" -PluginName "HelloWorldPlugin" -InputData "Your Name"
```

## Expected Results

### Successful Plugin Execution

When the HelloWorld plugin executes successfully, you should see:

```json
{
  "success": true,
  "message": "Greetings, Test User! Welcome to DevFlow Plugin System. [2025-06-04 15:00:00 UTC]",
  "outputFile": "/tmp/devflow-plugin-helloworldplugin-abc12345/hello-output.txt",
  "fileSize": 542,
  "executionTimeMs": 45.23,
  "timestamp": "2025-06-04 15:00:00 UTC",
  "logs": [
    "HelloWorld plugin execution started",
    "Configuration loaded - Greeting: Greetings, OutputPath: ./hello-output.txt",
    "Working directory: /tmp/devflow-plugin-helloworldplugin-abc12345",
    "Output written to file: /tmp/devflow-plugin-helloworldplugin-abc12345/hello-output.txt",
    "Execution completed in 45.23ms"
  ]
}
```

### Available Tools

The MCP server should expose these tools:

**Plugin Execution Tools:**
- `execute_plugin_helloworldplugin` - Execute the HelloWorld plugin

**Plugin Management Tools:**
- `list_plugins` - List all registered plugins
- `get_plugin_capabilities` - Get plugin execution capabilities
- `validate_plugin` - Validate plugin can be executed
- `discover_plugins` - Trigger plugin discovery

**Workflow Management Tools:**
- `create_workflow` - Create a new workflow
- `start_workflow` - Start workflow execution
- `add_workflow_step` - Add step to workflow
- `get_workflow` - Get workflow details
- `list_workflows` - List workflows

## Troubleshooting

### Server Not Starting

1. **Port in use**: Check if port 5000 is already in use
   ```powershell
   netstat -an | findstr :5000
   ```

2. **Database issues**: Delete the database file and restart:
   ```bash
   rm src/DevFlow.Host/devflow.db
   ```

### Plugin Not Found

1. **Check plugin registration**: Verify the HelloWorld plugin is registered
   ```powershell
   .\Test-Simple.ps1  # Look at the plugin list output
   ```

2. **Check plugin directory**: Ensure plugin files exist:
   ```bash
   ls plugins/csharp/HelloWorldPlugin/
   # Should show: plugin.json, HelloWorldPlugin.cs
   ```

3. **Check server logs**: Look for plugin discovery messages in the server output

### Plugin Execution Fails

1. **Check plugin validation**: Use the validate_plugin tool
2. **Review server logs**: Look for detailed error messages
3. **Check file permissions**: Ensure the server can create temporary directories

## Manual Testing with curl

You can also test manually using curl:

### Health Check
```bash
curl http://localhost:5000/health
```

### List Tools
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "tools/list",
    "params": {}
  }'
```

### Execute Plugin
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "2",
    "method": "tools/call",
    "params": {
      "name": "execute_plugin_helloworldplugin",
      "arguments": {
        "inputData": "Test User"
      }
    }
  }'
```

## Next Steps

Once basic plugin execution is working:

1. **Create additional plugins** in different languages
2. **Test workflow creation and execution**
3. **Integrate with external MCP clients**
4. **Add custom plugin capabilities and configurations**

