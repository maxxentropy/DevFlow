#!/usr/bin/env pwsh

# Test script for executing HelloWorld plugin via MCP over HTTP
Write-Host "Testing DevFlow MCP Server via HTTP - Execute HelloWorld Plugin" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

# Server endpoint
$serverUrl = "http://localhost:5000/mcp"

# MCP request for executing HelloWorld plugin
$executePluginRequest = @{
    jsonrpc = "2.0"
    id = "2"
    method = "tools/call"
    params = @{
        name = "execute_plugin_helloworldplugin"
        arguments = @{
            inputData = @{}
            executionParameters = @{
                greeting = "Hello from HTTP test!"
                includeTimestamp = $true
            }
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Sending HTTP request to execute HelloWorld plugin..." -ForegroundColor Yellow
Write-Host "Request: $executePluginRequest" -ForegroundColor Cyan

try {
    # Send HTTP POST request
    $response = Invoke-RestMethod -Uri $serverUrl -Method POST -Body $executePluginRequest -ContentType "application/json" -ErrorAction Stop
    
    Write-Host "Response received:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor White
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "HTTP Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "Test completed." -ForegroundColor Green

