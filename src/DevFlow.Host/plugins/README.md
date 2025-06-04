# DevFlow Plugins Directory
    
This directory contains plugins that extend DevFlow's automation capabilities.
    
## Directory Structure
    
```
plugins/
├── csharp/          # C# plugins (.NET 8+)
├── typescript/      # TypeScript/JavaScript plugins (Node.js)
├── python/          # Python plugins (3.8+)
├── _templates/      # Plugin templates for development
└── samples/         # Sample/reference plugins
```
    
## Plugin Development
    
Each plugin must include a `plugin.json` manifest file describing:
- Plugin metadata (name, version, description)
- Language and entry point
- Required capabilities and dependencies
- Configuration schema
    
### Sample plugin.json
    
```json
{
  "name": "MyPlugin",
  "version": "1.0.0",
  "description": "Description of what this plugin does",
  "language": "CSharp",
  "entryPoint": "MyPlugin.cs",
  "capabilities": ["file_read", "file_write"],
  "dependencies": [],
  "configuration": {
    "settingName": "defaultValue"
  }
}
```
    
## Security
    
Plugins run in isolated environments with restricted access to:
- File system (limited to working directory)
- Network access (configurable)
- System resources (memory and CPU limits)
    
Only install plugins from trusted sources.
    
## Getting Started
    
1. Create a new directory under the appropriate language folder
2. Add your plugin.json manifest
3. Implement your plugin following the language-specific conventions
4. Restart DevFlow to auto-discover your plugin
    
For detailed examples, see the samples/ directory.