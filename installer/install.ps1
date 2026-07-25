param(
    [switch]$NoAutoStart
)

$ErrorActionPreference = "Stop"

# Bump this alongside each release tag - shown in Windows' "Installed apps" list.
$appVersion = "2.0.0"

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

# Copy the uninstaller into the install directory too, so Windows' "Installed
# apps" entry (and its UninstallString) keeps working even if the user deletes
# the original downloaded/extracted release folder.
Copy-Item -Path (Join-Path $scriptDir "uninstall.ps1") -Destination $installDir -Force
Copy-Item -Path (Join-Path $scriptDir "uninstall.bat") -Destination $installDir -Force

$startupDir = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startupDir "iTunes-RPC.lnk"
$exePath = Join-Path $installDir "iTunesRPC.exe"

if (-not $NoAutoStart) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $installDir
    $shortcut.Description = "iTunes now-playing sync for Discord Rich Presence"
    $shortcut.Save()
}

# Register in Windows' "Installed apps" (Settings > Apps), per-user, no admin
# required, so it can be uninstalled from there even without the release zip.
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\iTunes-RPC"
New-Item -Path $uninstallKey -Force | Out-Null
$installedSizeKb = [math]::Round((Get-ChildItem $installDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1KB)
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "iTunes-RPC"
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value $appVersion
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "hvtim"
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $exePath
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""
Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name "EstimatedSize" -Value $installedSizeKb -Type DWord

if ($NoAutoStart) {
    Write-Host "Installed. Autostart at login was skipped - enable it anytime from Settings." -ForegroundColor Green
} else {
    Write-Host "Installed. iTunes-RPC will now start automatically at login." -ForegroundColor Green
}
Write-Host "It also now shows up in Windows Settings > Apps > Installed apps."
Write-Host ""

Get-Process iTunesRPC -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
Start-Process -FilePath $exePath -WorkingDirectory $installDir

Write-Host "Started iTunes-RPC. A tray icon should now appear - open Settings from there to enter your Discord Application ID." -ForegroundColor Green
