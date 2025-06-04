#!/usr/bin/env pwsh

# Test script for listing plugins via HTTP MCP endpoint
Write-Host "Testing DevFlow MCP Server via HTTP - List Plugins" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

# MCP request for listing plugins
$listPluginsRequest = @{
    jsonrpc = "2.0"
    id = "1"
    method = "tools/call"
    params = @{
        name = "list_plugins"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 10

Write-Host "Sending HTTP request to list plugins..." -ForegroundColor Yellow
Write-Host "Request: $listPluginsRequest" -ForegroundColor Cyan

try {
    # Send HTTP POST request to MCP endpoint
    $response = Invoke-RestMethod -Uri "http://localhost:5000/mcp" -Method POST -Body $listPluginsRequest -ContentType "application/json" -ErrorAction Stop
    
    Write-Host "Response received:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor White
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "Test completed." -ForegroundColor Green

