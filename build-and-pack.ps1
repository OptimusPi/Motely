#!/usr/bin/env pwsh
# Build and pack motely-wasm and motely-node packages
# Version is controlled by Directory.Packages.props

$ErrorActionPreference = "Stop"

Write-Host "=== MOTELY BUILD AND PACK ===" -ForegroundColor Cyan

# Get version
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

# Phase B: motely-wasm
Write-Host "`n=== Phase B: motely-wasm ===" -ForegroundColor Cyan

# Clean old framework folders
Write-Host "Cleaning old _framework folders..." -ForegroundColor Yellow
if (Test-Path "motely-wasm\_framework") { Remove-Item "motely-wasm\_framework" -Recurse -Force }
if (Test-Path "motely-wasm\_framework_st") { Remove-Item "motely-wasm\_framework_st" -Recurse -Force }

# Build single-thread WASM
Write-Host "Building single-thread WASM..." -ForegroundColor Yellow
dotnet clean Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release -p:SingleThread=true
if ($LASTEXITCODE -ne 0) { throw "Single-thread WASM build failed" }

# Stage single-thread
Write-Host "Staging single-thread framework..." -ForegroundColor Yellow
node stage-packages.mjs browser
if ($LASTEXITCODE -ne 0) { throw "Stage single-thread failed" }

# Rename to _framework_st
if (Test-Path "motely-wasm\_framework") {
    Move-Item "motely-wasm\_framework" "motely-wasm\_framework_st"
}

# Build multi-thread WASM (skip if YamlDotNet atomics error)
Write-Host "Building multi-thread WASM..." -ForegroundColor Yellow
dotnet clean Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
$multiThreadSuccess = $true
try {
    dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Multi-thread build failed (YamlDotNet atomics issue), using single-thread only" -ForegroundColor Yellow
        $multiThreadSuccess = $false
    } else {
        node stage-packages.mjs browser
        if ($LASTEXITCODE -ne 0) { throw "Stage multi-thread failed" }
    }
} catch {
    Write-Host "Multi-thread build failed: $_" -ForegroundColor Yellow
    $multiThreadSuccess = $false
}

# If multi-thread failed, copy single-thread to _framework
if (-not $multiThreadSuccess) {
    Write-Host "Copying single-thread to _framework (fallback)..." -ForegroundColor Yellow
    Copy-Item "motely-wasm\_framework_st" "motely-wasm\_framework" -Recurse
}

# Build motely-wasm package
Write-Host "Building motely-wasm package..." -ForegroundColor Yellow
Push-Location motely-wasm
npm install
npm run build
if ($LASTEXITCODE -ne 0) { throw "motely-wasm build failed" }

# Pack motely-wasm
Write-Host "Packing motely-wasm..." -ForegroundColor Yellow
npm pack
if ($LASTEXITCODE -ne 0) { throw "motely-wasm pack failed" }
Pop-Location

Write-Host "✓ motely-wasm-$version.tgz ready" -ForegroundColor Green

# Phase C: motely-node
Write-Host "`n=== Phase C: motely-node ===" -ForegroundColor Cyan

# Build linux-x64 addon
Write-Host "Building linux-x64 addon via Docker..." -ForegroundColor Yellow
.\build-linux.ps1
if ($LASTEXITCODE -ne 0) { throw "Linux build failed" }

# Verify binary exists
$nodeBinary = "motely-node\bin\linux-x64\Motely.NodeAddon.node"
if (-not (Test-Path $nodeBinary)) {
    throw "Linux binary not found: $nodeBinary"
}
Write-Host "✓ Linux binary exists: $nodeBinary" -ForegroundColor Green

# Copy jaml-schema files
Write-Host "Copying jaml-schema files..." -ForegroundColor Yellow
Copy-Item "motely-wasm\jaml-schema.js" "motely-node\" -Force
Copy-Item "motely-wasm\jaml-schema.d.ts" "motely-node\" -Force
Copy-Item "motely-wasm\jaml.schema.json" "motely-node\" -Force

# Pack motely-node
Write-Host "Packing motely-node..." -ForegroundColor Yellow
Push-Location motely-node
npm pack
if ($LASTEXITCODE -ne 0) { throw "motely-node pack failed" }
Pop-Location

Write-Host "✓ motely-node-$version.tgz ready" -ForegroundColor Green

# Summary
Write-Host "`n=== BUILD COMPLETE ===" -ForegroundColor Cyan
Write-Host "Version: $version" -ForegroundColor Green
Write-Host "Packages ready:" -ForegroundColor Green
Write-Host "  - motely-wasm\motely-wasm-$version.tgz" -ForegroundColor White
Write-Host "  - motely-node\motely-node-$version.tgz" -ForegroundColor White
Write-Host "`nTo publish:" -ForegroundColor Yellow
Write-Host "  cd motely-wasm && npm publish motely-wasm-$version.tgz --access public" -ForegroundColor White
Write-Host "  cd motely-node && npm publish motely-node-$version.tgz --access public" -ForegroundColor White
Write-Host "`nTo update JAMMY:" -ForegroundColor Yellow
Write-Host "  cd x:\JAMMY && pnpm add motely-wasm@$version motely-node@$version" -ForegroundColor White
