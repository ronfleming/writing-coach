# Writing Coach - Local Development Startup Script
# This script starts all required services for local development

Write-Host "Starting Writing Coach development environment..." -ForegroundColor Cyan

# Check for required tools
$requiredTools = @(
    @{ Name = "dotnet"; Command = "dotnet --version" },
    @{ Name = "func"; Command = "func --version" },
    @{ Name = "azurite"; Command = "azurite --version" }
)

foreach ($tool in $requiredTools) {
    try {
        $null = Invoke-Expression $tool.Command 2>&1
        Write-Host "✓ $($tool.Name) found" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ $($tool.Name) not found. Please install it first." -ForegroundColor Red
        exit 1
    }
}

# Start Azurite (Azure Storage Emulator) in background
Write-Host "`nStarting Azurite..." -ForegroundColor Yellow
$azuriteJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    azurite --silent --location .azurite --debug .azurite/debug.log
}

# Wait a moment for Azurite to start
Start-Sleep -Seconds 2

# Start Azure Functions API in background
Write-Host "Starting Azure Functions API..." -ForegroundColor Yellow
$apiJob = Start-Job -ScriptBlock {
    Set-Location "$using:PWD\Api"
    func start --port 7071
}

# Wait a moment for the API to start
Start-Sleep -Seconds 3

# Start Blazor Client
Write-Host "Starting Blazor Client..." -ForegroundColor Yellow
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Writing Coach is starting up!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nClient:  http://localhost:5042" -ForegroundColor White
Write-Host "API:     http://localhost:7071/api" -ForegroundColor White
Write-Host "`nPress Ctrl+C to stop all services" -ForegroundColor Gray
Write-Host ""

try {
    Set-Location Client
    dotnet watch run --urls "http://localhost:5042"
}
finally {
    Write-Host "`nStopping services..." -ForegroundColor Yellow
    Stop-Job $azuriteJob -ErrorAction SilentlyContinue
    Stop-Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job $azuriteJob -ErrorAction SilentlyContinue
    Remove-Job $apiJob -ErrorAction SilentlyContinue
    Write-Host "All services stopped." -ForegroundColor Green
}

