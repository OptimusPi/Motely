#!/usr/bin/env pwsh
# Build and pack motely-wasm (Bootsharp LLVM) and motely-node (NodeApi)
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

# Update motely-wasm/package.json
foreach ($pkg in @("motely-wasm\package.json", "motely-node\package.json")) {
    if (Test-Path $pkg) {
        $json = Get-Content $pkg -Raw
        $json = $json -replace "`"version`":\s*`"[^`"]+`"", "`"version`": `"$version`""
        Set-Content $pkg $json -NoNewline
    }
}

Write-Host "Bumped to $version" -ForegroundColor Green

# Phase 1: Generate JAML schema
Write-Host "`n=== Phase 1: JAML Schema ===" -ForegroundColor Cyan
dotnet run --project "Motely.CLI/Motely.CLI.csproj" -c Release -- --write-jaml-schema
if ($LASTEXITCODE -ne 0) { throw "JAML schema generation failed" }

# Phase 2: Compile net10.0 (Node.js with NodeApi)
Write-Host "`n=== Phase 2: Node.js (net10.0) ===" -ForegroundColor Cyan
dotnet publish "Motely/Motely.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "net10.0 publish failed" }
Write-Host "  ✓ Node.js bindings (Motely.js + Motely.dll)"

# Phase 3: Publish Motely.BrowserWasm (WASM via Bootsharp LLVM)
# BootsharpBinariesDirectory writes artifacts directly to motely-wasm/dist/bootsharp/
Write-Host "`n=== Phase 3: WASM (Bootsharp NativeAOT-LLVM) ===" -ForegroundColor Cyan
dotnet publish "Motely.BrowserWasm/Motely.BrowserWasm.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "WASM publish failed" }
Write-Host "  ✓ WASM + Bootsharp runtime → motely-wasm/dist/bootsharp/"

# Phase 4: Pack motely-wasm
Write-Host "`n=== Phase 4: Pack motely-wasm ===" -ForegroundColor Cyan
Push-Location motely-wasm
npm pack
if ($LASTEXITCODE -ne 0) { throw "motely-wasm pack failed" }
Pop-Location
Write-Host "✓ motely-wasm-$version.tgz ready" -ForegroundColor Green

# Summary
Write-Host "`n=== BUILD COMPLETE ===" -ForegroundColor Cyan
Write-Host "Version: $version" -ForegroundColor Green
Write-Host "Packages:" -ForegroundColor Green
Write-Host "  - motely-wasm\motely-wasm-$version.tgz (Browser WASM + Bootsharp)" -ForegroundColor White
Write-Host "`nPublish:" -ForegroundColor Yellow
Write-Host "  cd motely-wasm && npm publish motely-wasm-$version.tgz --access public" -ForegroundColor White
