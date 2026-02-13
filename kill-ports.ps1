# Kill processes using the development ports (5042 for Client, 7071 for API)
# Run this if you get "address already in use" errors

$ports = @(5042, 7071)

foreach ($port in $ports) {
    $connections = netstat -ano | Select-String ":$port\s+.*LISTENING" | ForEach-Object {
        ($_ -split '\s+')[-1]
    } | Select-Object -Unique

    foreach ($processId in $connections) {
        if ($processId -and $processId -ne '0') {
            Write-Host "Killing process $processId on port $port..." -ForegroundColor Yellow
            taskkill /PID $processId /F 2>$null
        }
    }
}

Write-Host "Done. Ports 5042 and 7071 should be free now." -ForegroundColor Green
