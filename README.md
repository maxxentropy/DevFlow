# DevFlow MCP Server

*A professional Model Context Protocol (MCP) server for development-workflow automation.*

DevFlow lets you create and run **language-agnostic** tools through a clean, extensible plugin system supporting **C#**, **TypeScript**, and **Python**.  
Its layered architecture (Clean Architecture) keeps business logic, infrastructure, and presentation concerns neatly separated.

---

## Table of Contents

1. [Features](#features)  
2. [Quick Start](#quick-start)  
3. [Available Tools](#available-tools)  
4. [Testing the Setup](#testing-the-setup)  
5. [Architecture Overview](#architecture-overview)  
6. [Plugin System](#plugin-system)  
7. [Troubleshooting](#troubleshooting)  
8. [Manual API Testing](#manual-api-testing)  
9. [License](#license)

---

## Features

- **MCP 1.0** compliant REST/JSON-RPC façade  
- **Hot-loadable plugins** in C#, TypeScript, and Python  
- **Workflow engine** (create, run, query, list)  
- Built-in **health check** endpoint  
- **PowerShell test scripts** for CI/CD smoke tests  
- **Clean Architecture** with CQRS & Mediator pattern  

---

## Quick Start

### Prerequisites

| Software   | Version |
|------------|---------|
| .NET SDK   | **8.0** |
| PowerShell | **7.x** |

### 1 — Build the solution

```bash
cd src
dotnet build
```

### 2 — Run the server

```bash
cd DevFlow.Host
dotnet run          # → http://localhost:5000
```

### 3 — Smoke‑test the install

```powershell
# From repository root
.\Test-Simple.ps1
```

You should see:

- ✅ **/health** check succeeded  
- ✅ List of registered plugins  
- ✅ **HelloWorldPlugin** executed successfully  

---

## Available Tools

### Plugin‑Execution Tools *(generated)*

| Tool | Language | Purpose |
|------|----------|---------|
| `execute_plugin_helloworldplugin` | C# | Sample Hello World |
| `execute_plugin_apiintegrationplugin` | Python | External API integration |
| `execute_plugin_dataprocessingplugin` | Python | Batch data processing |
| `execute_plugin_filemanipulationplugin` | TypeScript | File search / transform |

### Plugin‑Management Tools
- `list_plugins` — list all plugins  
- `get_plugin_capabilities` — detailed capabilities for a plugin  
- `validate_plugin` — ensure plugin & deps resolve  
- `discover_plugins` — force a plugin rescan  

### Workflow‑Management Tools
- `create_workflow`, `add_workflow_step`, `start_workflow`  
- `get_workflow`, `list_workflows`

---

## Testing the Setup

### Quick Test

```powershell
.\Test-Simple.ps1
```

Performs health check, lists plugins, and runs **HelloWorldPlugin**.

### Comprehensive Test

```powershell
# default
.\Test-PluginExecution.ps1

# custom
.\Test-PluginExecution.ps1 -PluginName "HelloWorldPlugin" -InputData "Custom User"
```

#### Expected JSON result

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

---

## Architecture Overview

```
┌───────────────────┐
│  DevFlow.Domain   │
└───────────────────┘
          ▲
          │
┌───────────────────┐
│ DevFlow.Application│
└───────────────────┘
          ▲
          │
┌───────────────────┐
│ DevFlow.Infrastructure │
└───────────────────┘
          ▲
          │
┌───────────────────┐
│ DevFlow.Presentation.MCP │
└───────────────────┘
          ▲
          │
┌───────────────────┐
│    DevFlow.Host   │
└───────────────────┘
```

Shared utilities live in **DevFlow.SharedKernel** (e.g., `Result<T>`).

---

## Plugin System

### Directory Layout

```
plugins/
├─ csharp/HelloWorldPlugin/
│  ├─ HelloWorldPlugin.cs
│  └─ plugin.json
├─ python/ApiIntegrationPlugin/
├─ python/DataProcessingPlugin/
└─ typescript/FileManipulationPlugin/
```

### `plugin.json` Manifest

```json
{
  "name": "HelloWorldPlugin",
  "version": "1.0.0",
  "description": "A simple Hello World plugin",
  "language": "CSharp",
  "entryPoint": "HelloWorldPlugin.cs",
  "capabilities": ["file_read", "file_write", "configuration_access"],
  "dependencies": ["nuget:Newtonsoft.Json@^13.0.1"],
  "configuration": {
    "greeting": "Hello",
    "outputPath": "./hello-output.txt"
  }
}
```

| Field          | Description                                 |
|----------------|---------------------------------------------|
| `language`     | `CSharp`, `TypeScript`, or `Python`         |
| `entryPoint`   | Main source file                            |
| `capabilities` | Permissions requested by the plugin         |
| `dependencies` | `nuget:`, `npm:`, or `pip:` package refs    |
| `configuration`| Default values (overridable at call)        |

---

## Troubleshooting

### Server will not start

- **Port 5000 in use**

  ```powershell
  netstat -an | findstr :5000
  ```

- **Database error**  
  Delete `src/DevFlow.Host/devflow.db` and restart; EF Core will recreate it.

### Plugin issues

1. **Not discovered** — run `.\Test-Simple.ps1` and inspect logs.  
2. **Bad structure** — confirm folder under `plugins/{language}` and valid `plugin.json`.  
3. **Execution error** — check server console for stack traces.

---

## Manual API Testing

```bash
# Health
curl http://localhost:5000/health

# List tools
curl -X POST http://localhost:5000/mcp       -H "Content-Type: application/json"       -d '{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}'

# Execute plugin
curl -X POST http://localhost:5000/mcp       -H "Content-Type: application/json"       -d '{"jsonrpc":"2.0","id":"2","method":"tools/call","params":{"name":"execute_plugin_helloworldplugin","arguments":{"inputData":"Test User"}}}'
```

---

## License

DevFlow MCP Server is released under the **MIT License**.  
See [LICENSE.md](LICENSE.md) for details.
