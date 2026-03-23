#!/usr/bin/env pwsh
# Build and pack motely-node (NativeAOT) and motely-wasm (Bootsharp NativeAOT-LLVM)
# Usage: ./build-and-pack.ps1 [-Node] [-Wasm] [-All]
# Default: -All

param(
    [switch]$Node,
    [switch]$Wasm,
    [switch]$All
)

if (-not $Node -and -not $Wasm) { $All = $true }
if ($All) { $Node = $true; $Wasm = $true }

$ErrorActionPreference = "Stop"

Write-Host "=== MOTELY BUILD ===" -ForegroundColor Cyan

# Bump patch version
$propsPath = "Directory.Packages.props"
$propsContent = Get-Content $propsPath -Raw
if ($propsContent -notmatch '<MotelyVersion>([^<]+)</MotelyVersion>') {
    throw "Could not find MotelyVersion in $propsPath"
}
$oldVersion = $matches[1].Trim()
$parts = $oldVersion.Split('.')
$parts[2] = [int]$parts[2] + 1
$version = $parts -join '.'
$propsContent = $propsContent -replace "<MotelyVersion>$oldVersion</MotelyVersion>", "<MotelyVersion>$version</MotelyVersion>"
Set-Content $propsPath $propsContent -NoNewline
Write-Host "Version: $oldVersion -> $version" -ForegroundColor Green

# Keep npm package.json versions aligned with <MotelyVersion>
node (Join-Path $PSScriptRoot "sync-version.mjs")
if ($LASTEXITCODE -ne 0) { throw "sync-version.mjs failed" }

# Phase 1: Node.js NativeAOT (.node addon via Docker)
if ($Node) {
    Write-Host "`n=== Node.js NativeAOT (linux-x64) ===" -ForegroundColor Cyan
    Write-Host "Building in Docker..." -ForegroundColor Yellow
    docker run --rm -v "${PWD}:/src" -w "//src" mcr.microsoft.com/dotnet/sdk:10.0 bash -c "apt-get update -qq && apt-get install -y -qq clang zlib1g-dev npm >/dev/null 2>&1 && dotnet publish Motely.Node/Motely.Node.csproj -c Release -f net10.0 -r linux-x64 -p:PublishAot=true"
    if ($LASTEXITCODE -ne 0) { throw "Node NativeAOT build failed" }
    node (Join-Path $PSScriptRoot "Motely/build/stage-node.mjs")
    if ($LASTEXITCODE -ne 0) { throw "stage-node.mjs failed" }
    Write-Host "  motely-node/ staged (MotelyNode.cjs + linux-x64/MotelyNode.node + generator .d.ts)" -ForegroundColor Green
}

# Phase 2: Browser WASM (Bootsharp NativeAOT-LLVM)
if ($Wasm) {
    Write-Host "`n=== Browser WASM (Bootsharp NativeAOT-LLVM) ===" -ForegroundColor Cyan
    dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "WASM build failed" }
    node (Join-Path $PSScriptRoot "Motely/build/stage-wasm.mjs")
    if ($LASTEXITCODE -ne 0) { throw "stage-wasm.mjs failed" }
    Write-Host "  motely-wasm/dist ready (dist/index.mjs + full dist/bootsharp/)" -ForegroundColor Green
}

Write-Host "`n=== DONE ===" -ForegroundColor Cyan
if ($Node) { Write-Host "  Node: cd motely-node && npm pack" -ForegroundColor White }
if ($Wasm) { Write-Host "  WASM: cd Motely.npm-staging/motely-wasm && npm pack" -ForegroundColor White }
