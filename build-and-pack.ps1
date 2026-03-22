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
foreach ($pkg in @("motely-wasm\package.json")) {
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

# Summary
Write-Host "`n=== BUILD COMPLETE ===" -ForegroundColor Cyan
Write-Host "Version: $version" -ForegroundColor Green
Write-Host "Packages ready:" -ForegroundColor Green
Write-Host "  - motely-wasm\motely-wasm-$version.tgz" -ForegroundColor White
Write-Host "`nTo publish:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  npm login" -ForegroundColor White
Write-Host ""
Write-Host "  cd motely-wasm && npm publish motely-wasm-$version.tgz --access public && cd .." -ForegroundColor White
Write-Host "`nTo update JAMMY:" -ForegroundColor Yellow
Write-Host "  cd x:\JAMMY && pnpm add motely-wasm@$version" -ForegroundColor White
