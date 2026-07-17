# Starts the Blood Bank LIS API and Web UI, then opens the browser.
$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

# Stop prior API/Web instances so a rebuild is picked up and ports are free.
Get-Process BloodBankLIS.Api, BloodBankLIS.Web -ErrorAction SilentlyContinue | Stop-Process -Force

$apiProject = Join-Path $Root "src\BloodBankLIS.Api\BloodBankLIS.Api.csproj"
$webProject = Join-Path $Root "src\BloodBankLIS.Web\BloodBankLIS.Web.csproj"
$webUrl = "http://localhost:5291"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "The .NET SDK was not found. Install .NET 10 SDK and try again." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

# API first (Web depends on it)
Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "`"$apiProject`"", "--launch-profile", "http" `
    -WorkingDirectory $Root `
    -WindowStyle Minimized

Start-Sleep -Seconds 4

Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "`"$webProject`"", "--launch-profile", "http" `
    -WorkingDirectory $Root `
    -WindowStyle Minimized

Start-Sleep -Seconds 5
Start-Process $webUrl
