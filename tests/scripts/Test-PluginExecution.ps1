# Test-PluginExecution.ps1
# Automated testing script for DevFlow MCP Server plugin execution

param(
    [string]$ServerUrl = "http://localhost:5000",
    [string]$PluginName = "HelloWorldPlugin",
    [string]$InputData = "DevFlow User"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Color functions for better output
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Step { param($Message) Write-Host "🔧 $Message" -ForegroundColor Magenta }

function Test-ServerHealth {
    Write-Step "Testing server health..."
    try {
        $response = Invoke-RestMethod -Uri "$ServerUrl/health" -Method Get -TimeoutSec 10
        Write-Success "Server is healthy: $response"
        return $true
    }
    catch {
        Write-Error "Health check failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-McpRequest {
    param(
        [string]$TestName,
        [hashtable]$Request,
        [switch]$ShowFullResponse
    )
    
    Write-Step "Testing: $TestName"
    
    try {
        $jsonRequest = $Request | ConvertTo-Json -Depth 10
        
        $response = Invoke-RestMethod -Uri "$ServerUrl/mcp" -Method Post -Body $jsonRequest -ContentType "application/json" -TimeoutSec 30
        
        if ($response.error) {
            Write-Error "$TestName failed with error: $($response.error.message)"
            return $false
        }
        
        Write-Success "$TestName completed successfully"
        
        if ($ShowFullResponse) {
            Write-Info "Response:"
            Write-Host ($response.result | ConvertTo-Json -Depth 5) -ForegroundColor Gray
        }
        
        return $response.result
    }
    catch {
        Write-Error "$TestName failed with exception: $($_.Exception.Message)"
        return $false
    }
}

# Main test execution
Write-Host "" 
Write-Host "🚀 DevFlow MCP Server Plugin Execution Test" -ForegroundColor Yellow
Write-Host "===========================================" -ForegroundColor Yellow
Write-Host "Server URL: $ServerUrl"
Write-Host "Plugin: $PluginName"
Write-Host "Input Data: $InputData"
Write-Host ""

# Test 1: Health Check
if (!(Test-ServerHealth)) {
    Write-Error "Health check failed - aborting tests"
    exit 1
}

# Test 2: List Available Tools
$listToolsRequest = @{
    jsonrpc = "2.0"
    id = "test-list-tools"
    method = "tools/list"
    params = @{}
}

$toolsResult = Test-McpRequest -TestName "List Available Tools" -Request $listToolsRequest
if (!$toolsResult) {
    Write-Error "Failed to list tools - aborting tests"
    exit 1
}

# Check if HelloWorld plugin tool is available
$pluginTool = $toolsResult.tools | Where-Object { $_.name -like "*helloworldplugin*" }
if ($pluginTool) {
    Write-Success "Found HelloWorld plugin tool: $($pluginTool.name)"
    $pluginToolName = $pluginTool.name
} else {
    Write-Warning "HelloWorld plugin tool not found in available tools"
    Write-Info "Available tools: $($toolsResult.tools.name -join ', ')"
    $pluginToolName = "execute_plugin_helloworldplugin"
}

# Test 3: Execute HelloWorld Plugin
$executeRequest = @{
    jsonrpc = "2.0"
    id = "test-execute-plugin"
    method = "tools/call"
    params = @{
        name = $pluginToolName
        arguments = @{
            inputData = $InputData
            executionParameters = @{
                greeting = "Greetings"
                includeTimestamp = $true
                logLevel = "info"
            }
        }
    }
}

if (!(Test-McpRequest -TestName "Execute HelloWorld Plugin" -Request $executeRequest -ShowFullResponse)) {
    Write-Error "Plugin execution failed"
}

# Summary
Write-Host ""
Write-Host "📊 Test Summary" -ForegroundColor Yellow
Write-Host "===============\nAll tests completed" -ForegroundColor Yellow

