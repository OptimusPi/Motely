#!/usr/bin/env pwsh
# Quick development start with auto-browser launch

Write-Host "🔥 Starting TheFool in Development Mode 🔥" -ForegroundColor Magenta
Write-Host "Browser will open automatically..." -ForegroundColor Green
Write-Host ""

# Just call the main script with dev defaults
& "$PSScriptRoot\start.ps1" -Environment Development -OpenBrowser -Watch