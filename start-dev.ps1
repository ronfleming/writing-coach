# Writing Coach - Development Startup Script
# Usage: .\start-dev.ps1

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Writing Coach - Dev Environment" -ForegroundColor Cyan  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean up any orphan processes
Write-Host "[1/4] Cleaning up ports..." -ForegroundColor Yellow
& "$PSScriptRoot\kill-ports.ps1"
Write-Host ""

# Step 2: Start Azurite in a new window
Write-Host "[2/4] Starting Azurite..." -ForegroundColor Yellow
$azuriteWindow = Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Azurite Storage Emulator' -ForegroundColor Cyan; azurite --silent --location '$PSScriptRoot\.azurite'" -PassThru
Write-Host "      Azurite running (PID: $($azuriteWindow.Id))" -ForegroundColor Green
Start-Sleep -Seconds 2

# Step 3: Start API in a new window
Write-Host "[3/4] Starting API (Azure Functions)..." -ForegroundColor Yellow
$apiWindow = Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'API - Azure Functions' -ForegroundColor Cyan; cd '$PSScriptRoot\Api'; func start" -PassThru
Write-Host "      API running (PID: $($apiWindow.Id))" -ForegroundColor Green
Start-Sleep -Seconds 3

# Step 4: Start Client in a new window  
Write-Host "[4/4] Starting Client (Blazor)..." -ForegroundColor Yellow
$clientWindow = Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Client - Blazor WASM' -ForegroundColor Cyan; cd '$PSScriptRoot\Client'; dotnet watch run" -PassThru
Write-Host "      Client running (PID: $($clientWindow.Id))" -ForegroundColor Green
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "  All services started!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Azurite: http://127.0.0.1:10000" -ForegroundColor White
Write-Host "  API:     http://localhost:7071" -ForegroundColor White
Write-Host "  Client:  http://localhost:5042" -ForegroundColor White
Write-Host ""
Write-Host "Each service runs in its own window." -ForegroundColor Gray
Write-Host "Press Enter here to stop all services." -ForegroundColor Yellow
Write-Host ""

# Wait for user to press Enter
Read-Host "Press Enter to stop all services"

# Cleanup
Write-Host ""
Write-Host "Stopping services..." -ForegroundColor Yellow

# Function to kill a process tree (process and all its children)
function Stop-ProcessTree {
    param([int]$ProcessId)
    
    # Get all child processes
    $children = Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $ProcessId }
    foreach ($child in $children) {
        Stop-ProcessTree -ProcessId $child.ProcessId
    }
    
    # Kill this process
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

# Kill each window and its children
if ($azuriteWindow -and !$azuriteWindow.HasExited) {
    Stop-ProcessTree -ProcessId $azuriteWindow.Id
    Write-Host "  Azurite stopped" -ForegroundColor Gray
}
if ($apiWindow -and !$apiWindow.HasExited) {
    Stop-ProcessTree -ProcessId $apiWindow.Id
    Write-Host "  API stopped" -ForegroundColor Gray
}
if ($clientWindow -and !$clientWindow.HasExited) {
    Stop-ProcessTree -ProcessId $clientWindow.Id
    Write-Host "  Client stopped" -ForegroundColor Gray
}

# Give processes a moment to terminate
Start-Sleep -Seconds 1

# Final cleanup of any orphans
Write-Host "  Cleaning up ports..." -ForegroundColor Gray
& "$PSScriptRoot\kill-ports.ps1" 2>$null

Write-Host ""
Write-Host "All services stopped." -ForegroundColor Green
Write-Host ""
