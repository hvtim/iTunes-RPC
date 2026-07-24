$ErrorActionPreference = "SilentlyContinue"

Write-Host "Uninstalling iTunes-Sync..."

Get-Process iTunesSync -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$startupDir = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupDir "iTunes-Sync.lnk"
if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Removed autostart shortcut."
}

$installDir = Join-Path $env:LOCALAPPDATA "iTunes-Sync"
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
    Write-Host "Removed installed files from $installDir."
}

Write-Host ""
Write-Host "iTunes-Sync has been fully uninstalled." -ForegroundColor Green
Write-Host "Nothing else on this machine was changed - Discord and iTunes themselves are untouched."
