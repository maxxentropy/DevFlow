DevFlow MCP Server
A professional MCP (Model Context Protocol) server for development workflow automation, supporting C#, TypeScript, and Python plugins. DevFlow enables robust, language-agnostic tool creation and execution within a structured and extensible environment.

Quick Start
Prerequisites
.NET 8.0 SDK
PowerShell 7+ (for test scripts)
1. Build the Solution
Navigate to the source directory and build the project:

Bash

cd src
dotnet build
2. Run the Server
Navigate to the host project and run the server. It will start on http://localhost:5000 by default.

Bash

cd DevFlow.Host
dotnet run
3. Run a Quick Test
From the root of the repository, run the simple test script to verify that the server is healthy and plugins are loaded correctly.

PowerShell

.\Test-Simple.ps1
You should see output indicating a successful health check, a list of registered plugins, and a successful execution of the HelloWorldPlugin.

Available Tools
The server exposes its capabilities as tools that can be invoked via the MCP tools/call method.

Plugin Execution Tools
These tools are generated dynamically based on the available plugins. The tool name is derived from the plugin name in its plugin.json file.

execute_plugin_helloworldplugin: Executes the sample C# HelloWorld plugin.
execute_plugin_apiintegrationplugin: Executes the Python API Integration plugin.
execute_plugin_dataprocessingplugin: Executes the Python Data Processing plugin.
execute_plugin_filemanipulationplugin: Executes the TypeScript File Manipulation plugin.
Plugin Management Tools
list_plugins: Lists all registered plugins and their status.
get_plugin_capabilities: Gets detailed execution capabilities for a specific plugin.
validate_plugin: Validates that a plugin and its dependencies are ready for execution.
discover_plugins: Manually triggers plugin discovery (this happens automatically on startup).
Workflow Management Tools
create_workflow: Creates a new workflow definition.
start_workflow: Starts the execution of a defined workflow.
add_workflow_step: Adds a new step to a workflow definition.
get_workflow: Retrieves the details and status of a specific workflow.
list_workflows: Lists all available workflows.
Testing the Setup
The repository includes several scripts for testing the server and plugin functionality.

Quick Test (Recommended)
For a fast validation that the server is running and plugins are loaded, use Test-Simple.ps1 from the repository root. This script performs three key actions:

✅ Checks the server's /health endpoint.
✅ Lists all registered plugins using the list_plugins tool.
✅ Executes the HelloWorldPlugin with a test input.
<!-- end list -->

PowerShell

# From the repository root
.\Test-Simple.ps1
Comprehensive Test
For a more detailed test of plugin execution with parameterization, use Test-PluginExecution.ps1.

PowerShell

# From the repository root with default parameters
.\Test-PluginExecution.ps1

# With custom parameters
.\Test-PluginExecution.ps1 -PluginName "HelloWorldPlugin" -InputData "Custom User"
Expected Plugin Execution Result
A successful execution of the HelloWorldPlugin will return a JSON object similar to this:

JSON

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
Architecture Overview
The system implements Clean Architecture to ensure separation of concerns, modularity, and testability.

DevFlow.Domain: Contains core business logic, entities (Plugin, Workflow), value objects, and domain events. This layer has no dependencies on other layers.
DevFlow.Application: Contains application-level logic, including CQRS commands and queries (using MediatR), DTOs, and interfaces for infrastructure services (IPluginRepository, IWorkflowRepository).
DevFlow.Infrastructure: Implements the interfaces defined in the Application layer. It handles persistence with Entity Framework Core, plugin runtime management (for C#, TypeScript, Python), and communication with external services.
DevFlow.Presentation.MCP: Implements the Model Context Protocol (MCP) layer, defining handlers for protocol methods like tools/list and tools/call.
DevFlow.Host: The main executable project that composes the application, configures services, and runs the web server.
DevFlow.SharedKernel: Contains shared code, base classes (e.g., AggregateRoot, ValueObject), and cross-cutting concerns like the Result class for error handling.
Plugin System
The DevFlow plugin system is a core feature, allowing for the extension of its capabilities with custom logic written in multiple languages.

Plugin Directory Structure
Plugins are loaded from the /plugins directory at the root of the repository. The structure is organized by language:

plugins/
├── csharp/
│   └── HelloWorldPlugin/
│       ├── HelloWorldPlugin.cs
│       └── plugin.json
├── python/
│   ├── ApiIntegrationPlugin/
│   └── DataProcessingPlugin/
└── typescript/
    └── FileManipulationPlugin/
Plugin Manifest
Each plugin must contain a plugin.json file in its root directory. This manifest defines the plugin's metadata and behavior.

JSON

{
  "name": "HelloWorldPlugin",
  "version": "1.0.0",
  "description": "A simple Hello World plugin...",
  "language": "CSharp",
  "entryPoint": "HelloWorldPlugin.cs",
  "capabilities": [
    "file_read",
    "file_write",
    "configuration_access"
  ],
  "dependencies": [
    "nuget:Newtonsoft.Json@^13.0.1"
  ],
  "configuration": {
    "greeting": "Hello",
    "outputPath": "./hello-output.txt"
  }
}
language: The plugin's language (CSharp, TypeScript, Python).
entryPoint: The main file for the plugin.
capabilities: A list of permissions the plugin requires.
dependencies: A list of package dependencies (e.g., nuget:, pip:, npm:).
configuration: Default configuration parameters that can be overridden at execution time.
Troubleshooting
Server Not Starting
Port 5000 in use: Check if another process is using the default port.
PowerShell

netstat -an | findstr :5000
Database issues: If the server fails to start due to a database error, you can reset it by deleting the devflow.db file in src/DevFlow.Host/ and restarting the server. The database will be recreated automatically.
Plugin Not Found or Fails to Execute
Check Plugin Registration: Use Test-Simple.ps1 to see the list of registered plugins. If your plugin isn't listed, check the server logs for discovery errors.
Verify Directory Structure: Ensure your plugin is in the correct plugins/{language} directory and contains a valid plugin.json.
Check Server Logs: The server output console provides detailed logs on plugin discovery, validation, and execution. Look for warnings or errors related to your plugin.
Manual API Testing with curl
You can interact with the MCP server directly using curl.

Health Check
Bash

curl http://localhost:5000/health
List Available Tools
Bash

curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "tools/list",
    "params": {}
  }'
Execute a Plugin
Bash

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
License
This project is licensed under the MIT License. See the LICENSE.md file for details.