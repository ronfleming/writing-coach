# Kill processes using the development ports and verify they're free.
# Ports: 5042 (Client), 7071 (API), 10000-10002 (Azurite blob/queue/table)

$ports = @(5042, 7071, 10000, 10001, 10002)
$killed = @()

foreach ($port in $ports) {
    $pids = netstat -ano | Select-String ":$port\s+.*LISTENING" | ForEach-Object {
        ($_ -split '\s+')[-1]
    } | Select-Object -Unique

    foreach ($processId in $pids) {
        if ($processId -and $processId -ne '0' -and $processId -notin $killed) {
            $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
            $name = if ($proc) { $proc.ProcessName } else { "unknown" }
            Write-Host "Killing $name (PID $processId) on port $port..." -ForegroundColor Yellow
            taskkill /PID $processId /T /F 2>$null | Out-Null
            $killed += $processId
        }
    }
}

if ($killed.Count -eq 0) {
    Write-Host "No processes found on any development ports." -ForegroundColor Cyan
    exit 0
}

# Give the OS time to release the sockets
Start-Sleep -Seconds 2

# Verify all ports are free
$stuck = @()
foreach ($port in $ports) {
    $still = netstat -ano | Select-String ":$port\s+.*LISTENING"
    if ($still) { $stuck += $port }
}

if ($stuck.Count -gt 0) {
    Write-Host "Warning: ports still in use: $($stuck -join ', ')" -ForegroundColor Red
    Write-Host "You may need to run this script as Administrator." -ForegroundColor Red
    exit 1
}

Write-Host "Done. All development ports are free." -ForegroundColor Green
