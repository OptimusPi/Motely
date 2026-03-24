#!/usr/bin/env pwsh
# publish-all.ps1 — Build, pack, and publish motely-wasm + motely-node
# Run from: X:\JammySeedFinder\src\MotelyJAML\
set-strictmode -version latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$buildScript = "$root\build.mjs"
$wasmPkg = "$root\motely-wasm"
$nodePkg = "$root\motely-node"

# Write-Host "`n=== Step 1: Run tests ===" -ForegroundColor Cyan
# dotnet test "$root\Motely.Tests" -c Release --no-restore
# if ($LASTEXITCODE -ne 0) { Write-Host "TESTS FAILED. Fix them first." -ForegroundColor Red; exit 1 }
# Write-Host "Tests passed." -ForegroundColor Green

if (-not (Test-Path $buildScript)) { Write-Host "build.mjs not found." -ForegroundColor Red; exit 1 }

Write-Host "`n=== Step 2: Build WASM package (NativeAOT-LLVM / Bootsharp) ===" -ForegroundColor Cyan
Write-Host "Output: $wasmPkg\dist\" -ForegroundColor DarkGray
node $buildScript wasm
if ($LASTEXITCODE -ne 0) { Write-Host "WASM package build failed." -ForegroundColor Red; exit 1 }
Write-Host "WASM package build OK." -ForegroundColor Green

Write-Host "`n=== Step 3: Build Node package (NativeAOT) ===" -ForegroundColor Cyan
Write-Host "Output: $nodePkg\" -ForegroundColor DarkGray
node $buildScript node
if ($LASTEXITCODE -ne 0) { Write-Host "Node package build failed." -ForegroundColor Red; exit 1 }
Write-Host "Node package build OK." -ForegroundColor Green

# Show versions
$wasmVer = (Get-Content "$wasmPkg\package.json" | ConvertFrom-Json).version
$nodeVer = (Get-Content "$nodePkg\package.json" | ConvertFrom-Json).version
Write-Host "`n=== Ready to publish ===" -ForegroundColor Green
Write-Host "  motely-wasm@$wasmVer  ($wasmPkg)" -ForegroundColor Yellow
Write-Host "  motely-node@$nodeVer  ($nodePkg)" -ForegroundColor Yellow

Write-Host ""
$confirm = Read-Host "Publish both to npm? (y/N)"
if ($confirm -ne 'y') { Write-Host "Aborted." -ForegroundColor DarkGray; exit 0 }

Write-Host "`nPublishing motely-wasm@$wasmVer..." -ForegroundColor Cyan
Push-Location $wasmPkg
npm publish --access public
Pop-Location
if ($LASTEXITCODE -ne 0) { Write-Host "motely-wasm publish failed." -ForegroundColor Red; exit 1 }

Write-Host "Publishing motely-node@$nodeVer..." -ForegroundColor Cyan
Push-Location $nodePkg
npm publish --access public
Pop-Location
if ($LASTEXITCODE -ne 0) { Write-Host "motely-node publish failed." -ForegroundColor Red; exit 1 }

Write-Host "`n=== ALL DONE ===" -ForegroundColor Green
Write-Host "  motely-wasm@$wasmVer  published" -ForegroundColor Green
Write-Host "  motely-node@$nodeVer  published" -ForegroundColor Green
Write-Host "`nNext: cd X:\JAMMY && pnpm add motely-wasm@$wasmVer motely-node@$nodeVer" -ForegroundColor DarkGray
