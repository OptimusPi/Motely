@echo off
REM Start TheFool - Balatro Seed Finder
REM This is a wrapper for the PowerShell script

echo.
echo ========================================
echo   TheFool - Balatro Seed Finder
echo   Starting Web UI...
echo ========================================
echo.

REM Check if PowerShell is available
where pwsh >nul 2>nul
if %errorlevel% equ 0 (
    pwsh.exe -ExecutionPolicy Bypass -File "%~dp0start.ps1" -OpenBrowser %*
) else (
    powershell.exe -ExecutionPolicy Bypass -File "%~dp0start.ps1" -OpenBrowser %*
)

if %errorlevel% neq 0 (
    echo.
    echo Error: Failed to start TheFool
    echo Please make sure you have .NET 9 SDK installed
    echo.
    pause
)