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
if ($propsContent -notmatch '<MotelyVersion>([^<]+)</MotelyVersion>') {
    throw "Could not find MotelyVersion in Directory.Packages.props"
}

# Auto-bump patch version
$oldVersion = $matches[1].Trim()
$parts = $oldVersion.Split('.')
$parts[2] = [int]$parts[2] + 1
$version = $parts -join '.'

Write-Host "Version: $oldVersion -> $version" -ForegroundColor Green

# Update Directory.Packages.props
$propsContent = $propsContent -replace "<MotelyVersion>$oldVersion</MotelyVersion>", "<MotelyVersion>$version</MotelyVersion>"
Set-Content $propsPath $propsContent -NoNewline

# Update package.json files (replace whatever version is there)
foreach ($pkg in @("motely-wasm\package.json", "motely-node\package.json")) {
    $json = Get-Content $pkg -Raw
    $json = $json -replace "`"version`":\s*`"[^`"]+`"", "`"version`": `"$version`""
    Set-Content $pkg $json -NoNewline
}

Write-Host "Bumped all 3 files to $version" -ForegroundColor Green

# JAML JSON schema + JS helpers (all paths in JamlSchemaGenerator — never edit copies by hand)
Write-Host "Regenerating JAML schema from C# (Motely.CLI --write-jaml-schema)..." -ForegroundColor Yellow
dotnet run --project "Motely.CLI/Motely.CLI.csproj" -c Release -- --write-jaml-schema
if ($LASTEXITCODE -ne 0) { throw "JAML schema generation failed" }

# Phase B: motely-wasm
Write-Host "`n=== Phase B: motely-wasm ===" -ForegroundColor Cyan

# Clean old Bootsharp bundles
Write-Host "Cleaning old bootsharp folders..." -ForegroundColor Yellow
if (Test-Path "motely-wasm\bootsharp") { Remove-Item "motely-wasm\bootsharp" -Recurse -Force }
if (Test-Path "motely-wasm\bootsharp_st") { Remove-Item "motely-wasm\bootsharp_st" -Recurse -Force }

# Build single-thread WASM
Write-Host "Building single-thread WASM..." -ForegroundColor Yellow
dotnet clean Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release -p:SingleThread=true
if ($LASTEXITCODE -ne 0) { throw "Single-thread WASM build failed" }

Write-Host "Staging single-thread Bootsharp bundle..." -ForegroundColor Yellow
node stage-packages.mjs bootsharp-st
if ($LASTEXITCODE -ne 0) { throw "Stage single-thread failed" }

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
        node stage-packages.mjs bootsharp
        if ($LASTEXITCODE -ne 0) { throw "Stage multi-thread failed" }
    }
} catch {
    Write-Host "Multi-thread build failed: $_" -ForegroundColor Yellow
    $multiThreadSuccess = $false
}

if (-not $multiThreadSuccess) {
    Write-Host "Copying single-thread to bootsharp (fallback)..." -ForegroundColor Yellow
    Copy-Item "motely-wasm\bootsharp_st" "motely-wasm\bootsharp" -Recurse
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
Write-Host "`nTo publish (3 copy-paste blocks):" -ForegroundColor Yellow
Write-Host ""
Write-Host "  npm login" -ForegroundColor White
Write-Host ""
Write-Host "  cd motely-wasm && npm publish motely-wasm-$version.tgz --access public && cd .." -ForegroundColor White
Write-Host ""
Write-Host "  cd motely-node && npm publish motely-node-$version.tgz --access public && cd .." -ForegroundColor White
Write-Host "`nTo update JAMMY:" -ForegroundColor Yellow
Write-Host "  cd x:\JAMMY && pnpm add motely-wasm@$version motely-node@$version" -ForegroundColor White
