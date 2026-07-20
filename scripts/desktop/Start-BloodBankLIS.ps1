# Starts the Greyhound Blood Bank LIS API and Web UI, then opens the browser.
$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

# Stop prior API/Web instances so a rebuild is picked up and ports are free.
Get-Process BloodBankLIS.Api, BloodBankLIS.Web -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$apiProject = Join-Path $Root "src\BloodBankLIS.Api\BloodBankLIS.Api.csproj"
$webProject = Join-Path $Root "src\BloodBankLIS.Web\BloodBankLIS.Web.csproj"
$apiUrl = "http://localhost:5177"
$webUrl = "http://localhost:5291"
$logDir = Join-Path $env:LOCALAPPDATA "BloodBankLIS\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$apiOut = Join-Path $logDir "api-stdout.log"
$apiErr = Join-Path $logDir "api-stderr.log"
$webOut = Join-Path $logDir "web-stdout.log"
$webErr = Join-Path $logDir "web-stderr.log"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "The .NET SDK was not found. Install .NET 10 SDK and try again." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

function Wait-HttpReady {
    param(
        [string]$Url,
        [string]$Name,
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            # Do not follow redirects: Development HTTP profiles often emit an HTTPS
            # redirect that has no listener, which would look like a failed startup.
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2 -MaximumRedirection 0
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                Write-Host "$Name is ready at $Url" -ForegroundColor Green
                return $true
            }
        }
        catch {
            $statusCode = $null
            try { $statusCode = [int]$_.Exception.Response.StatusCode } catch { }
            if ($statusCode -ge 200 -and $statusCode -lt 500) {
                Write-Host "$Name is ready at $Url" -ForegroundColor Green
                return $true
            }
        }

        Start-Sleep -Seconds 2
    }

    Write-Host "$Name did not become ready within $TimeoutSeconds seconds." -ForegroundColor Red
    return $false
}

function Show-LogTail {
    param([string[]]$Paths)
    foreach ($path in $Paths) {
        if (Test-Path $path) {
            Write-Host "---- $path ----" -ForegroundColor Yellow
            Get-Content $path -Tail 30
        }
    }
}

Write-Host "Starting Greyhound Blood Bank LIS..."
Write-Host "  Logs: $logDir"

# API first (Web depends on it)
Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", $apiProject, "--launch-profile", "http") `
    -WorkingDirectory $Root `
    -RedirectStandardOutput $apiOut `
    -RedirectStandardError $apiErr `
    -WindowStyle Hidden | Out-Null

if (-not (Wait-HttpReady -Url $apiUrl -Name "API" -TimeoutSeconds 180)) {
    Write-Host ""
    Write-Host "API failed to start. Last log lines:" -ForegroundColor Red
    Show-LogTail -Paths @($apiOut, $apiErr)
    Read-Host "Press Enter to close"
    exit 1
}

Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", $webProject, "--launch-profile", "http") `
    -WorkingDirectory $Root `
    -RedirectStandardOutput $webOut `
    -RedirectStandardError $webErr `
    -WindowStyle Hidden | Out-Null

if (-not (Wait-HttpReady -Url $webUrl -Name "Web UI" -TimeoutSeconds 180)) {
    Write-Host ""
    Write-Host "Web UI failed to start. Last log lines:" -ForegroundColor Red
    Show-LogTail -Paths @($webOut, $webErr)
    Read-Host "Press Enter to close"
    exit 1
}

Start-Process $webUrl
Write-Host "Greyhound Blood Bank LIS is running at $webUrl" -ForegroundColor Green
