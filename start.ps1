#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the TheFool Blazor application for Balatro seed finding
.DESCRIPTION
    This script launches the TheFool web UI with configurable parameters
.PARAMETER Port
    The port to run the web server on (default: 5000)
.PARAMETER HttpsPort
    The HTTPS port to run the web server on (default: 5001)
.PARAMETER DatabasePath
    Path to the DuckDB database directory (default: ouija_databases)
.PARAMETER MotelyPath
    Path to the Motely executable (default: Motely\bin\Debug\net9.0\Motely.exe)
.PARAMETER Environment
    The hosting environment (Development, Production)
.PARAMETER OpenBrowser
    Automatically open the browser when the app starts
.PARAMETER Urls
    Override the URLs to listen on (e.g., "http://localhost:5000;https://localhost:5001")
.EXAMPLE
    .\start.ps1
    # Starts with default settings
.EXAMPLE
    .\start.ps1 -Port 8080 -OpenBrowser
    # Starts on port 8080 and opens browser
.EXAMPLE
    .\start.ps1 -DatabasePath "C:\my_databases" -Environment "Production"
    # Starts with custom database path in production mode
#>
[CmdletBinding()]
param(
    [Parameter()]
    [int]$Port = 5000,
    
    [Parameter()]
    [int]$HttpsPort = 5001,
    
    [Parameter()]
    [string]$DatabasePath = "ouija_databases",
    
    [Parameter()]
    [string]$MotelyPath = "Motely\bin\Debug\net9.0\Motely.exe",
    
    [Parameter()]
    [ValidateSet("Development", "Production")]
    [string]$Environment = "Development",
    
    [Parameter()]
    [switch]$OpenBrowser,
    
    [Parameter()]
    [string]$Urls = "",
    
    [Parameter()]
    [switch]$NoBuild,
    
    [Parameter()]
    [switch]$Watch
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Display startup banner
Write-Host @"
🃏 ========================================= 🃏
   TheFool - Balatro Seed Finder
   Version: 0.4.0
🃏 ========================================= 🃏
"@ -ForegroundColor Cyan

# Check if we're in the right directory
if (-not (Test-Path "TheFool\TheFool.csproj")) {
    Write-Error "This script must be run from the Motely solution root directory"
    exit 1
}

# Check if Motely executable exists
$motelyFullPath = Join-Path $PSScriptRoot $MotelyPath
if (-not (Test-Path $motelyFullPath)) {
    Write-Warning "Motely executable not found at: $motelyFullPath"
    Write-Host "You may need to build Motely first:" -ForegroundColor Yellow
    Write-Host "  dotnet build Motely\Motely.csproj" -ForegroundColor Yellow
}

# Create database directory if it doesn't exist
$dbFullPath = Join-Path $PSScriptRoot $DatabasePath
if (-not (Test-Path $dbFullPath)) {
    Write-Host "Creating database directory: $dbFullPath" -ForegroundColor Green
    New-Item -ItemType Directory -Path $dbFullPath -Force | Out-Null
}

# Build the project unless -NoBuild is specified
if (-not $NoBuild) {
    Write-Host "`nBuilding TheFool project..." -ForegroundColor Yellow
    dotnet build TheFool\TheFool.csproj --configuration $Environment
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        exit 1
    }
    Write-Host "Build completed successfully!" -ForegroundColor Green
}

# Check launch settings for port if in Development mode
if ($Environment -eq "Development" -and (Test-Path "TheFool\Properties\launchSettings.json")) {
    try {
        $launchSettings = Get-Content "TheFool\Properties\launchSettings.json" | ConvertFrom-Json
        $profile = $launchSettings.profiles."http"
        if ($profile -and $profile.applicationUrl) {
            $detectedUrl = $profile.applicationUrl
            Write-Host "Detected launch settings URL: $detectedUrl" -ForegroundColor Yellow
            # Extract port from URL
            if ($detectedUrl -match ':([0-9]+)') {
                $Port = $matches[1]
            }
        }
    } catch {
        Write-Warning "Could not parse launch settings"
    }
}

# Prepare environment variables
$env:ASPNETCORE_ENVIRONMENT = $Environment
$env:ASPNETCORE_URLS = if ($Urls) { $Urls } else { "http://localhost:$Port;https://localhost:$HttpsPort" }

# Prepare configuration as environment variables
$env:DuckDbPath = $DatabasePath
$env:MotelySearch__ExecutablePath = $motelyFullPath
$env:MotelySearch__WorkingDirectory = Split-Path $motelyFullPath -Parent

# Display configuration
Write-Host "`nConfiguration:" -ForegroundColor Cyan
Write-Host "  Environment: $Environment" -ForegroundColor White
Write-Host "  URLs: $($env:ASPNETCORE_URLS)" -ForegroundColor White
Write-Host "  Database Path: $DatabasePath" -ForegroundColor White
Write-Host "  Motely Path: $MotelyPath" -ForegroundColor White

# Create appsettings override for runtime configuration
$appSettingsOverride = @{
    DuckDbPath = $DatabasePath
    MotelySearch = @{
        ExecutablePath = $motelyFullPath
        WorkingDirectory = Split-Path $motelyFullPath -Parent
    }
} | ConvertTo-Json -Depth 10

$appSettingsPath = "TheFool\appsettings.runtime.json"
$appSettingsOverride | Out-File -FilePath $appSettingsPath -Encoding utf8

Write-Host "`nStarting TheFool..." -ForegroundColor Green

# Function to open browser
function Open-Browser {
    param($url)
    Start-Sleep -Seconds 2
    if ($IsMacOS) {
        open $url
    } elseif ($IsLinux) {
        xdg-open $url
    } else {
        Start-Process $url
    }
}

# Open browser if requested
if ($OpenBrowser) {
    # In Development mode, the actual port might be different due to launch settings
    $actualPort = if ($Environment -eq "Development" -and (Test-Path "TheFool\Properties\launchSettings.json")) {
        try {
            $ls = Get-Content "TheFool\Properties\launchSettings.json" | ConvertFrom-Json
            if ($ls.profiles."http".applicationUrl -match ':([0-9]+)') {
                $matches[1]
            } else {
                $Port
            }
        } catch {
            $Port
        }
    } else {
        $Port
    }
    
    $browserUrl = "http://localhost:$actualPort"
    Write-Host "Browser will open to $browserUrl in 3 seconds..." -ForegroundColor Green
    
    Start-Job -ScriptBlock { 
        param($url)
        Start-Sleep -Seconds 3
        if ($IsMacOS) {
            open $url
        } elseif ($IsLinux) {
            xdg-open $url
        } else {
            Start-Process $url
        }
    } -ArgumentList $browserUrl | Out-Null
}

try {
    # Change to TheFool directory
    Push-Location TheFool
    
    # Run the application
    if ($Watch) {
        Write-Host "Starting in watch mode (auto-reload on file changes)..." -ForegroundColor Magenta
        dotnet watch run --no-build
    } else {
        dotnet run --no-build --configuration $Environment
    }
} finally {
    Pop-Location
    
    # Cleanup
    if (Test-Path $appSettingsPath) {
        Remove-Item $appSettingsPath -Force
    }
}

Write-Host "`nTheFool has been shut down." -ForegroundColor Yellow