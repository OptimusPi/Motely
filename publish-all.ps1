#!/usr/bin/env pwsh

# ONE FUCKING SCRIPT TO PUBLISH BOTH MOTELY PACKAGES
# Run from MotelyJAML root directory

$ErrorActionPreference = "Stop"

Write-Host "=== MOTELY PACKAGE PUBLISH SCRIPT ===" -ForegroundColor Cyan

# Get version from Directory.Packages.props
$propsPath = "Directory.Packages.props"
if (-not (Test-Path $propsPath)) {
    throw "Directory.Packages.props not found"
}

$propsContent = Get-Content $propsPath -Raw
if ($propsContent -match '<MotelyVersion>([^<]+)</MotelyVersion>') {
    $version = $matches[1].Trim()
    Write-Host "Version: $version" -ForegroundColor Green
} else {
    throw "Could not find MotelyVersion in Directory.Packages.props"
}

# Sync versions first
Write-Host "`n=== Syncing package.json versions ===" -ForegroundColor Yellow
node sync-version.mjs

# Build and pack WASM package
Write-Host "`n=== Building motely-wasm ===" -ForegroundColor Yellow
Set-Location "motely-wasm"
npm run build
npm run pack

# Build and pack Node package
Write-Host "`n=== Building motely-node ===" -ForegroundColor Yellow
Set-Location ".."
.\build-linux.ps1

Set-Location "motely-node"
npm run pack

# Go back to root for publishing
Set-Location ".."

# Find the tarballs
$wasmTarball = "motely-wasm\motely-wasm-$version.tgz"
$nodeTarball = "motely-node\motely-node-$version.tgz"

Write-Host "`n=== Found tarballs ===" -ForegroundColor Yellow
Write-Host "WASM: $wasmTarball" -ForegroundColor Cyan
Write-Host "Node: $nodeTarball" -ForegroundColor Cyan

# Verify tarballs exist
if (-not (Test-Path $wasmTarball)) {
    throw "WASM tarball not found: $wasmTarball"
}
if (-not (Test-Path $nodeTarball)) {
    throw "Node tarball not found: $nodeTarball"
}

# Publish both packages
Write-Host "`n=== Publishing motely-wasm ===" -ForegroundColor Yellow
Set-Location "motely-wasm"
npm publish "motely-wasm-$version.tgz" --access public
Write-Host "✓ motely-wasm published!" -ForegroundColor Green

Set-Location ".."

Write-Host "`n=== Publishing motely-node ===" -ForegroundColor Yellow
Set-Location "motely-node"
npm publish "motely-node-$version.tgz" --access public
Write-Host "✓ motely-node published!" -ForegroundColor Green

Set-Location ".."

Write-Host "`n=== BOTH PACKAGES PUBLISHED SUCCESSFULLY ===" -ForegroundColor Green
Write-Host "Now update JAMMY dependencies:" -ForegroundColor Cyan
Write-Host "  cd x:\JAMMY"
Write-Host "  pnpm update motely-wasm@$version motely-node@$version"
Write-Host "  pnpm install"
Write-Host "  pnpm build"
