<#
.SYNOPSIS
Test Motely.WASM locally: build + serve AppBundle at http://localhost:3333 (no npm).
Use this when you're in this folder and just want to try the WASM. For using in an app, use npm (see README).
#>
param(
    [switch]$NoBuild,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$AppBundleDir = Join-Path $PSScriptRoot "bin/$Configuration/net10.0-browser/browser-wasm/AppBundle"

if (-not $NoBuild) {
    Write-Host "Building Motely.WASM ($Configuration)..." -ForegroundColor Cyan
    dotnet build -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}

if (-not (Test-Path $AppBundleDir)) {
    throw "AppBundle not found at: $AppBundleDir. Run without -NoBuild first."
}

Write-Host ""
Write-Host "Serving AppBundle at http://localhost:3333 (with COOP/COEP for WASM threads)" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host ""

$ServerScript = Join-Path $PSScriptRoot "scripts\serve-with-headers.cjs"
node $ServerScript 3333 $AppBundleDir
