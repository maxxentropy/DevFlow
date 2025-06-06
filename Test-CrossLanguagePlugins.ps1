# Test-CrossLanguagePlugins.ps1
# Automated testing script for DevFlow MCP Server Python and TypeScript plugins

param(
    [string]$ServerUrl = "http://localhost:5000"
)

# Set error action preference to stop on errors
$ErrorActionPreference = "Stop"

#region Helper Functions
# Color functions for better output
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info    { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error   { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Step    { param($Message) Write-Host "🔧 $Message" -ForegroundColor Magenta }

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
    
    Write-Step "Executing Test: $TestName"
    
    try {
        $jsonRequest = $Request | ConvertTo-Json -Depth 10
        Write-Info "Request Body: $jsonRequest"
        
        $response = Invoke-RestMethod -Uri "$ServerUrl/mcp" -Method Post -Body $jsonRequest -ContentType "application/json" -TimeoutSec 60
        
        if ($response.error) {
            Write-Error "$TestName failed with MCP error: $($response.error.message)"
            return $null
        }
        
        $result = $response.result
        
        if ($result.isError) {
             Write-Error "$TestName failed. Plugin reported an error."
             Write-Host ($result | ConvertTo-Json -Depth 5) -ForegroundColor Red
             return $null
        }

        Write-Success "$TestName completed successfully"
        
        if ($ShowFullResponse) {
            Write-Info "Full Response:"
            Write-Host ($result | ConvertTo-Json -Depth 5) -ForegroundColor Gray
        } else {
            Write-Info "Response Content:"
            $textContent = $result.content | Where-Object { $_.type -eq 'text' } | Select-Object -ExpandProperty text
            Write-Host $textContent -ForegroundColor Gray
        }
        
        return $result
    }
    catch {
        Write-Error "$TestName failed with exception: $($_.Exception.Message)"
        if ($_.Exception.Response) {
            $errorResponse = $_.Exception.Response.GetResponseStream()
            $streamReader = New-Object System.IO.StreamReader($errorResponse)
            $errorBody = $streamReader.ReadToEnd()
            Write-Warning "Error Response Body: $errorBody"
        }
        return $null
    }
}
#endregion

# Main test execution
Write-Host "" 
Write-Host "🚀 DevFlow Cross-Language Plugin Test Suite" -ForegroundColor Yellow
Write-Host "===========================================" -ForegroundColor Yellow
Write-Host "Server URL: $ServerUrl"
Write-Host ""

# Test 1: Health Check
if (!(Test-ServerHealth)) {
    Write-Error "Health check failed - aborting tests."
    exit 1
}

# Test 2: List available tools to ensure plugins are registered
Write-Step "Listing all available tools..."
$listToolsRequest = @{
    jsonrpc = "2.0"
    id      = "list-all-tools"
    method  = "tools/list"
    params  = @{}
}
$toolsResult = Test-McpRequest -TestName "List All Tools" -Request $listToolsRequest
if (!$toolsResult) {
    Write-Error "Failed to list tools - aborting tests."
    exit 1
}
$availableTools = $toolsResult.tools.name
Write-Info "Available Tools: $($availableTools -join ', ')"


# Test 3: Execute ApiIntegrationPlugin (Python)
Write-Host ""
Write-Warning "--- Testing Python: ApiIntegrationPlugin ---"
$apiTestRequest = @{
    jsonrpc = "2.0"
    id      = "test-python-api"
    method  = "tools/call"
    params  = @{
        name      = "execute_plugin_apiintegrationplugin"
        arguments = @{
            inputData = @{
                operation = "request"
                method    = "GET"
                url       = "https://httpbin.org/get"
                params    = @{ test = "devflow" }
            }
        }
    }
}
Test-McpRequest -TestName "ApiIntegrationPlugin (GET httpbin.org)" -Request $apiTestRequest

# Test 4: Execute DataProcessingPlugin (Python)
Write-Host ""
Write-Warning "--- Testing Python: DataProcessingPlugin ---"
$dataProcessingRequest = @{
    jsonrpc = "2.0"
    id      = "test-python-data"
    method  = "tools/call"
    params  = @{
        name      = "execute_plugin_dataprocessingplugin"
        arguments = @{
            inputData = @{
                operation  = "write"
                outputPath = "test_output.csv"
                format     = "csv"
                data       = @(
                    @{ id = 1; name = "product_a"; price = 100 },
                    @{ id = 2; name = "product_b"; price = 250 }
                )
            }
        }
    }
}
Test-McpRequest -TestName "DataProcessingPlugin (Write CSV)" -Request $dataProcessingRequest -ShowFullResponse

# Test 5: Execute FileManipulationPlugin (TypeScript)
Write-Host ""
Write-Warning "--- Testing TypeScript: FileManipulationPlugin ---"
$fileManipulationRequest = @{
    jsonrpc = "2.0"
    id      = "test-typescript-file"
    method  = "tools/call"
    params  = @{
        name      = "execute_plugin_filemanipulationplugin"
        arguments = @{
            inputData = @{
                operation = "write"
                filePath  = "ts_plugin_output.txt"
                content   = "Hello from the TypeScript plugin test script!"
            }
        }
    }
}
Test-McpRequest -TestName "FileManipulationPlugin (Write Text File)" -Request $fileManipulationRequest -ShowFullResponse


# Summary
Write-Host ""
Write-Host "📊 Test Summary" -ForegroundColor Yellow
Write-Host "==============="
Write-Success "All tests completed."