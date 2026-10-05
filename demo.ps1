# Hybrid demo in one go: build, start the fleet API and this laptop's agent in their own windows, open the dashboard.
# Usage: .\demo.ps1 [-Mock] [-Url http://0.0.0.0:5000]
param([switch]$Mock, [string]$Url = 'http://localhost:5000')

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'Building solution...'
dotnet build OemAiAssistant.slnx -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Build failed - fix the errors above and run demo.ps1 again.' -ForegroundColor Red
    exit 1
}

# Wildcard hosts are for binding only; the browser and the local agent must call localhost.
$local = ($Url -replace '://(0\.0\.0\.0|\+|\*)', '://localhost').TrimEnd('/')

$apiArgs = "--urls $Url --Demo:Fleet=true"
if ($Mock) { $apiArgs += ' --Ai:Provider=Mock' }
Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle='OemAi API'; dotnet run --project src/OemAi.Api --no-build -- $apiArgs"

Write-Host "Waiting for the API at $local ..."
$deadline = (Get-Date).AddSeconds(60)
$ready = $false
while (-not $ready -and (Get-Date) -lt $deadline) {
    try {
        Invoke-WebRequest "$local/api/devices" -UseBasicParsing -TimeoutSec 5 | Out-Null
        $ready = $true
    } catch {
        Start-Sleep -Seconds 1
    }
}
if (-not $ready) {
    Write-Host "API did not answer $local/api/devices within 60 s - check the 'OemAi API' window." -ForegroundColor Red
    exit 1
}

Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle='OemAi Agent'; dotnet run --project src/OemAi.Agent --no-build -- --server $local"
Start-Process $local

Write-Host ''
Write-Host 'OemAi demo is running.' -ForegroundColor Green
Write-Host "  Dashboard : $local"
Write-Host "  Swagger   : $local/swagger"
Write-Host "  Stop      : close the 'OemAi API' and 'OemAi Agent' windows."
Write-Host ''
Write-Host 'LAN demo (other laptops report to this dashboard):'
Write-Host '  1. On this machine : .\demo.ps1 -Url http://0.0.0.0:5000   (allow it if Windows Firewall prompts)'
Write-Host '  2. On each laptop  : dotnet run --project src/OemAi.Agent -- --server http://<this-machine-IP>:5000'
