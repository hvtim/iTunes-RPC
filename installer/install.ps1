$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $scriptDir "app"
$installDir = Join-Path $env:LOCALAPPDATA "iTunes-RPC"

if (!(Test-Path $sourceDir)) {
    Write-Host "Could not find the app files next to this script (expected '$sourceDir')." -ForegroundColor Red
    Write-Host "Make sure you extracted the whole release zip before running install.bat."
    exit 1
}

Write-Host "Installing iTunes-RPC to $installDir ..."
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $sourceDir "*") -Destination $installDir -Recurse -Force

$startupDir = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupDir "iTunes-RPC.lnk"
$exePath = Join-Path $installDir "iTunesRPC.exe"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "iTunes now-playing sync for Discord Rich Presence"
$shortcut.Save()

Write-Host "Installed. iTunes-RPC will now start automatically at login." -ForegroundColor Green
Write-Host ""

Get-Process iTunesRPC -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
Start-Process -FilePath $exePath -WorkingDirectory $installDir

Write-Host "Started iTunes-RPC. A tray icon should now appear - open Settings from there to enter your Discord Application ID." -ForegroundColor Green
