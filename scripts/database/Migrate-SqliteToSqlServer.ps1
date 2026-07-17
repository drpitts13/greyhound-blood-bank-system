#Requires -Version 5.1
<#
.SYNOPSIS
    Migrates the local SQLite dev database to SQL Server Express.

.DESCRIPTION
    Copies data from %LOCALAPPDATA%\BloodBankLIS\bloodbank.dev.db into the
    BloodBankLIS database on localhost\SQLEXPRESS02. Stop the API before running.
#>
param(
    [string]$SqliteConnectionString,
    [string]$SqlServerConnectionString = "Server=localhost\SQLEXPRESS02;Database=BloodBankLIS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $repoRoot

$sqlitePath = if ($SqliteConnectionString) {
    # Let the migrator resolve; still check file lock target when using default layout.
    $null
} else {
    Join-Path $env:LOCALAPPDATA "BloodBankLIS\bloodbank.dev.db"
}

if (-not $SqliteConnectionString) {
    if (-not (Test-Path $sqlitePath)) {
        Write-Error "SQLite database not found: $sqlitePath. Run the API in SQLite dev mode first, or pass -SqliteConnectionString."
    }

    Write-Host "Source SQLite: $sqlitePath"
} else {
    Write-Host "Source SQLite: (custom connection string)"
}

$service = Get-Service -Name "MSSQL`$SQLEXPRESS02" -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Write-Warning "SQL Server Express service MSSQL`$SQLEXPRESS02 was not found. Ensure SQL Server 2025 Express is installed."
} elseif ($service.Status -ne "Running") {
    Write-Host "Starting SQL Server Express service..."
    Start-Service "MSSQL`$SQLEXPRESS02"
}

$args = @("run", "--project", "src/BloodBankLIS.DbMigrator", "--")
if ($SqliteConnectionString) {
    $args += @("--sqlite", $SqliteConnectionString)
}
if ($SqlServerConnectionString) {
    $args += @("--sqlserver", $SqlServerConnectionString)
}

Write-Host "Target SQL Server: $SqlServerConnectionString"
Write-Host "Running migrator..."
& dotnet @args
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Verification queries (run in SSMS or sqlcmd):"
Write-Host "  SELECT COUNT(*) AS Patients FROM Patients;"
Write-Host "  SELECT COUNT(*) AS BloodUnits FROM BloodProducts;"
Write-Host "  SELECT COUNT(*) AS Orders FROM Orders;"
Write-Host ""
Write-Host "Migration finished. Start the API with SQL Server dev configuration to verify."
