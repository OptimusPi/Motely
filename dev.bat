@echo off
REM Quick launch TheFool in development mode with browser

echo Starting TheFool (Development Mode)...
echo Browser will open automatically!
echo.

where pwsh >nul 2>nul
if %errorlevel% equ 0 (
    pwsh.exe -ExecutionPolicy Bypass -File "%~dp0dev.ps1"
) else (
    powershell.exe -ExecutionPolicy Bypass -File "%~dp0dev.ps1"
)

pause