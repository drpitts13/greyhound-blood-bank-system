# Converts bloodbank-lis-icon.png to .ico for Windows shortcuts.
param(
    [string]$PngPath = (Join-Path $PSScriptRoot "bloodbank-lis-icon.png"),
    [string]$IcoPath = (Join-Path $PSScriptRoot "bloodbank-lis-icon.ico")
)

Add-Type -AssemblyName System.Drawing

$bmp = [System.Drawing.Bitmap]::FromFile($PngPath)
$size = 256
$resized = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($resized)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($bmp, 0, 0, $size, $size)
$g.Dispose()
$bmp.Dispose()

$icon = [System.Drawing.Icon]::FromHandle($resized.GetHicon())
$fs = [System.IO.File]::OpenWrite($IcoPath)
$icon.Save($fs)
$fs.Close()
$resized.Dispose()

Write-Host "Created $IcoPath"
