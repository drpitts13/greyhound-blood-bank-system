# Creates or updates a desktop shortcut for HemoCore with the ABO quadrant icon.
$ErrorActionPreference = "Stop"
$Desktop = [Environment]::GetFolderPath("Desktop")
$ShortcutPath = Join-Path $Desktop "HemoCore.lnk"
$Launcher = Join-Path $PSScriptRoot "Start-BloodBankLIS.ps1"
$IconPath = Join-Path $PSScriptRoot "bloodbank-lis-icon.ico"
$PngPath = Join-Path $PSScriptRoot "bloodbank-lis-icon.png"

if (-not (Test-Path $IconPath)) {
    if (-not (Test-Path $PngPath)) {
        throw "Icon not found. Expected $PngPath"
    }
    & (Join-Path $PSScriptRoot "Convert-PngToIco.ps1")
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($ShortcutPath)
$pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
$shortcut.TargetPath = if ($pwsh) { $pwsh.Source } else { (Get-Command powershell).Source }
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$Launcher`""
$shortcut.WorkingDirectory = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$shortcut.IconLocation = "$IconPath,0"
$shortcut.Description = "Start HemoCore (API + Web). Closing the app stops the API."
$shortcut.Save()

Write-Host "Desktop shortcut created:"
Write-Host "  $ShortcutPath"
