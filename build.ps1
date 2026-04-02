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

# Read MotelyVersion from Directory.Packages.props and stamp package.json after publish.
$propsPath = Join-Path $root "Directory.Packages.props"
[xml]$props = Get-Content $propsPath
$version = $props.Project.PropertyGroup.MotelyVersion
if (-not $version) { throw "MotelyVersion not found in $propsPath" }

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

# Stamp npm package version so npm publish matches MotelyVersion.
$pkgJson = Join-Path $wasmOut "package.json"
$raw = Get-Content -LiteralPath $pkgJson -Raw
$updated = [regex]::Replace($raw, '"version"\s*:\s*"[^"]*"', "`"version`": `"$version`"")
if ($updated -ne $raw) {
    Set-Content -LiteralPath $pkgJson -Value $updated -NoNewline
}
Write-Host "Stamped npm package version -> $version" -ForegroundColor Green

# ── 3. Copy Monaco subpath export into the npm package ─────────────────────
$monacoSrc = Join-Path $root "tools\jaml-language\monaco\dist"
$monacoDst = Join-Path $wasmOut "monaco"
if (Test-Path $monacoSrc) {
    New-Item -ItemType Directory -Force $monacoDst | Out-Null
    Copy-Item "$monacoSrc\*" $monacoDst -Recurse -Force
    Write-Host "Monaco subpath export copied -> $monacoDst" -ForegroundColor Green
} else {
    Write-Warning "Monaco dist not found. Run pnpm build in tools/jaml-language first."
}

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
