#!/usr/bin/env pwsh
# publish-all.ps1 — Build, pack, and publish motely-wasm + motely-node
# Run from: X:\JammySeedFinder\src\MotelyJAML\
set-strictmode -version latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$orch = "$root\Motely.Orchestration"
$wasmPkg = "$root\motely-wasm"
$wasmPkg = "$root\motely-wasm"
$nodePkg = "$root\motely-node"

Write-Host "`n=== Step 1: Run tests ===" -ForegroundColor Cyan
dotnet test "$root\Motely.Tests" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Host "TESTS FAILED. Fix them first." -ForegroundColor Red; exit 1 }
Write-Host "Tests passed." -ForegroundColor Green

Write-Host "`n=== Step 2: Build server (net10.0) ===" -ForegroundColor Cyan
dotnet build "$root" -c Release
if ($LASTEXITCODE -ne 0) { Write-Host "Server build failed." -ForegroundColor Red; exit 1 }
Write-Host "Server build OK." -ForegroundColor Green

Write-Host "`n=== Step 3: Publish WASM (NativeAOT-LLVM) ===" -ForegroundColor Cyan
Write-Host "Output: $wasmPkg\dist\" -ForegroundColor DarkGray
dotnet publish "$orch" -c Release -p:WasmBuild=true
if ($LASTEXITCODE -ne 0) { Write-Host "WASM publish failed." -ForegroundColor Red; exit 1 }
Write-Host "WASM publish OK." -ForegroundColor Green

Write-Host "`n=== Step 4: Publish Node (NativeAOT linux-x64) ===" -ForegroundColor Cyan
Write-Host "Output: $nodePkg\" -ForegroundColor DarkGray
dotnet publish "$orch" -c Release -p:NodeBuild=true -r linux-x64
if ($LASTEXITCODE -ne 0) { Write-Host "Node publish failed." -ForegroundColor Red; exit 1 }
Write-Host "Node publish OK." -ForegroundColor Green

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
