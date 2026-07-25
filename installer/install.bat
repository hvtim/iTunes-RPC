@echo off
choice /C YN /M "Start iTunes-RPC automatically when you log in"
if errorlevel 2 (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -NoAutoStart
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
)
echo.
pause
