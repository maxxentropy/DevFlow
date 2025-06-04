# Test-Simple.ps1
# Quick test script for DevFlow MCP Server

param([string]$ServerUrl = "http://localhost:5000")

Write-Host "🧪 Quick DevFlow Test" -ForegroundColor Yellow

# Test 1: Health Check
try {
    $health = Invoke-RestMethod -Uri "$ServerUrl/health" -Method Get
    Write-Host "✅ Health: $health" -ForegroundColor Green
} catch {
    Write-Host "❌ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: List Plugins
try {
    $request = @{ jsonrpc="2.0"; id="1"; method="tools/call"; params=@{ name="list_plugins"; arguments=@{} } } | ConvertTo-Json
    $response = Invoke-RestMethod -Uri "$ServerUrl/mcp" -Method Post -Body $request -ContentType "application/json"
    
    if ($response.result) {
        Write-Host "✅ Plugins listed successfully" -ForegroundColor Green
        Write-Host $response.result.content[0].text -ForegroundColor Gray
    } else {
        Write-Host "❌ No plugins found" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Plugin list failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Execute HelloWorld Plugin
try {
    $request = @{ 
        jsonrpc="2.0"
        id="2"
        method="tools/call"
        params=@{ 
            name="execute_plugin_helloworldplugin"
            arguments=@{ inputData="Test User" } 
        } 
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$ServerUrl/mcp" -Method Post -Body $request -ContentType "application/json"
    
    if ($response.result -and !$response.result.isError) {
        Write-Host "✅ Plugin executed successfully!" -ForegroundColor Green
        Write-Host $response.result.content[0].text -ForegroundColor Gray
    } else {
        Write-Host "❌ Plugin execution failed" -ForegroundColor Red
        if ($response.result.content) {
            Write-Host $response.result.content[0].text -ForegroundColor Red
        }
    }
} catch {
    Write-Host "❌ Plugin execution failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "\n✨ Test completed!" -ForegroundColor Yellow

