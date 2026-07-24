$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $scriptDir "app"
$installDir = Join-Path $env:LOCALAPPDATA "iTunes-Sync"

if (!(Test-Path $sourceDir)) {
    Write-Host "Could not find the app files next to this script (expected '$sourceDir')." -ForegroundColor Red
    Write-Host "Make sure you extracted the whole release zip before running install.bat."
    exit 1
}

Write-Host "Installing iTunes-Sync to $installDir ..."
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $sourceDir "*") -Destination $installDir -Recurse -Force

$configPath = Join-Path $installDir "config.json"
$clientId = $null

if (Test-Path $configPath) {
    try {
        $existing = Get-Content $configPath -Raw | ConvertFrom-Json
        if ($existing.ClientId -and $existing.ClientId -ne "YOUR_DISCORD_CLIENT_ID_HERE") {
            $clientId = $existing.ClientId
            Write-Host "Found an existing configuration - keeping your current Client ID."
        }
    } catch {}
}

if (-not $clientId) {
    Write-Host ""
    Write-Host "iTunes-Sync needs a Discord application Client ID to run." -ForegroundColor Yellow
    Write-Host "See README.md ('One-time setup', step 1) for how to create one - takes about a minute."
    Write-Host ""
    $clientId = Read-Host "Paste your Discord Application (Client) ID"
}

$config = [ordered]@{
    ClientId       = $clientId
    LargeImageKey  = "itunes_logo"
    PollIntervalMs = 2000
}
$config | ConvertTo-Json | Set-Content -Path $configPath -Encoding UTF8

$startupDir = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupDir "iTunes-Sync.lnk"
$exePath = Join-Path $installDir "iTunesSync.exe"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "iTunes now-playing sync for Discord Rich Presence"
$shortcut.Save()

Write-Host ""
Write-Host "Installed. iTunes-Sync will now start automatically at login." -ForegroundColor Green
Write-Host "It only connects to iTunes if iTunes is already open, so it's safe to leave enabled."
Write-Host ""

Get-Process iTunesSync -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
Start-Process -FilePath $exePath -WorkingDirectory $installDir

Write-Host "Started iTunes-Sync. Done!" -ForegroundColor Green
