#!/usr/bin/env pwsh

# Test script for listing plugins via MCP
Write-Host "Testing DevFlow MCP Server - List Plugins" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

# MCP request for listing plugins
$listPluginsRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "list_plugins"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 10

Write-Host "Sending request to list plugins..." -ForegroundColor Yellow
Write-Host "Request: $listPluginsRequest" -ForegroundColor Cyan

try {
    # Send request via stdin/stdout
    $response = $listPluginsRequest | dotnet run --no-build 2>$null
    
    if ($response -and $response.Trim() -ne "") {
        Write-Host "Raw response received:" -ForegroundColor Green
        Write-Host "'$response'" -ForegroundColor White
        
        # Try to parse as JSON
        try {
            $jsonResponse = $response | ConvertFrom-Json
            Write-Host "Parsed JSON response:" -ForegroundColor Green
            $jsonResponse | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor White
        }
        catch {
            Write-Host "Failed to parse response as JSON: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "Raw response length: $($response.Length)" -ForegroundColor Yellow
            Write-Host "First 100 chars: '$($response.Substring(0, [Math]::Min(100, $response.Length)))'" -ForegroundColor Yellow
        }
    } else {
        Write-Host "No response received or empty response" -ForegroundColor Red
    }
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Test completed." -ForegroundColor Green

