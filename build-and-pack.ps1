#!/usr/bin/env pwsh
# Build and pack motely-node (NativeAOT) and motely-wasm (Bootsharp NativeAOT-LLVM)
# Usage: ./build-and-pack.ps1

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

# Keep npm package.json versions aligned
node (Join-Path $PSScriptRoot "sync-version.mjs")
if ($LASTEXITCODE -ne 0) { throw "sync-version.mjs failed" }

# Phase 1: Node.js NativeAOT
Write-Host "`n=== Node.js NativeAOT (linux-x64) ===" -ForegroundColor Cyan
docker run --rm -v "${PWD}:/src" -w "/src" mcr.microsoft.com/dotnet/sdk:10.0 bash -c "apt-get update -qq && apt-get install -y -qq clang zlib1g-dev npm >/dev/null 2>&1 && dotnet publish Motely.Node/Motely.Node.csproj -c Release -f net10.0 -r linux-x64 -p:PublishAot=true"
if ($LASTEXITCODE -ne 0) { throw "Node NativeAOT build failed" }

Push-Location motely-node
npm pack
if ($LASTEXITCODE -ne 0) { throw "npm pack failed for motely-node" }
Pop-Location
Write-Host "  motely-node packed" -ForegroundColor Green

# Phase 2: Browser WASM
Write-Host "`n=== Browser WASM (Bootsharp NativeAOT-LLVM) ===" -ForegroundColor Cyan
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "WASM build failed" }

node (Join-Path $PSScriptRoot "Motely/build/stage-wasm.mjs")
if ($LASTEXITCODE -ne 0) { throw "stage-wasm.mjs failed" }

Push-Location Motely.npm-staging\motely-wasm
npm pack
if ($LASTEXITCODE -ne 0) { throw "npm pack failed for motely-wasm" }
Pop-Location
Write-Host "  motely-wasm packed" -ForegroundColor Green

Write-Host "`n=== DONE ===" -ForegroundColor Cyan
Write-Host "Tarballs ready in motely-node/ and motely-wasm/" -ForegroundColor White