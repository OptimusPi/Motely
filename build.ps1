<#
.SYNOPSIS
    Build all C# projects, then publish the WASM package.

.PARAMETER WasmOnly
    Skip the solution build, only publish WASM.

.PARAMETER NpmPublish
    After publishing WASM, run npm publish --access public (uses NPM_TOKEN env or existing npm login).
#>
param([switch]$WasmOnly, [switch]$NpmPublish)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$wasmDir = Join-Path $root "Motely.BrowserWasm"
$wasmProj = Join-Path $wasmDir "Motely.BrowserWasm.csproj"
# Bootsharp writes the npm package beside the WASM project (BootsharpPublishDirectory).
$wasmOut = Join-Path $wasmDir "motely-wasm"

# ── 1. Build solution (all non-WASM projects) ──────────────────────────────
if (-not $WasmOnly) {
    Write-Host "`n==> dotnet build (solution)" -ForegroundColor Cyan
    dotnet build (Join-Path $root "Motely.sln") -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
    Write-Host "Build OK" -ForegroundColor Green
}

# ── 2. Publish WASM ────────────────────────────────────────────────────────
Write-Host "`n==> dotnet publish (WASM)" -ForegroundColor Cyan
dotnet publish $wasmProj -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (WASM) failed" }
Write-Host "WASM published -> $wasmOut" -ForegroundColor Green

# ── 3. Optional npm publish ────────────────────────────────────────────────
if ($NpmPublish) {
    Push-Location $wasmOut
    try {
        if ($env:NPM_TOKEN) {
            "//registry.npmjs.org/:_authToken=$($env:NPM_TOKEN)" | Set-Content ".npmrc"
        }
        Write-Host "`n==> npm publish" -ForegroundColor Cyan
        npm publish --access public
        if ($LASTEXITCODE -ne 0) { throw "npm publish failed" }
        Write-Host "npm published!" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
