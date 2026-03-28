<#
.SYNOPSIS
    Builds the motely-wasm and motely-node NPM packages.

.DESCRIPTION
    One script. Two NPM packages.
      - motely-wasm:  Bootsharp + NativeAOT-LLVM browser WASM  (Motely.BrowserWasm)
      - motely-node:  NativeAOT linux-x64 Node addon           (Motely.Node)

    Run from the repo root:
      ./Build-NpmPackages.ps1              # both
      ./Build-NpmPackages.ps1 -WasmOnly    # just motely-wasm
      ./Build-NpmPackages.ps1 -NodeOnly    # just motely-node

.PARAMETER WasmOnly
    Build only the motely-wasm package.

.PARAMETER NodeOnly
    Build only the motely-node package.
#>
param(
    [switch]$WasmOnly,
    [switch]$NodeOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Write-Host "=== Motely NPM Package Builder ===" -ForegroundColor Cyan
Write-Host "Repo root: $repoRoot"
Write-Host ""

# ── motely-wasm (Bootsharp WASM) ──────────────────────────────────────────────
if (-not $NodeOnly) {
    Write-Host "── Building motely-wasm (Bootsharp + NativeAOT-LLVM) ──" -ForegroundColor Yellow

    $wasmProject = Join-Path $repoRoot "Motely.BrowserWasm" "Motely.BrowserWasm.csproj"
    $wasmOutDir  = Join-Path $repoRoot "Motely.BrowserWasm" "motely-wasm"

    Write-Host "  Project : $wasmProject"
    Write-Host "  Output  : $wasmOutDir"
    Write-Host ""

    # Bootsharp publish produces the npm package directly into BootsharpPackageDirectory
    dotnet publish $wasmProject -c Release
    if ($LASTEXITCODE -ne 0) { throw "motely-wasm build failed!" }

    Write-Host ""
    Write-Host "  motely-wasm built -> $wasmOutDir" -ForegroundColor Green

    # Pack it
    Push-Location $wasmOutDir
    npm pack
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw "npm pack failed for motely-wasm!" }
    $wasmTarball = Get-ChildItem "motely-wasm-*.tgz" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Write-Host "  Tarball : $($wasmTarball.FullName)" -ForegroundColor Green
    Pop-Location
    Write-Host ""
}

# ── motely-node (NativeAOT Node addon) ────────────────────────────────────────
if (-not $WasmOnly) {
    Write-Host "── Building motely-node (NativeAOT linux-x64) ──" -ForegroundColor Yellow

    $nodeProject = Join-Path $repoRoot "Motely.Node" "Motely.Node.csproj"
    $nodeOutDir  = Join-Path $repoRoot "Motely.npm-staging" "motely-node"

    Write-Host "  Project : $nodeProject"
    Write-Host "  Output  : $nodeOutDir"
    Write-Host ""

    # PublishAot + PublishNodeModule copies .cjs/.mjs/.d.ts/.node into NpmPackDirectory
    dotnet publish $nodeProject -c Release -r linux-x64 /p:NodeBuild=true
    if ($LASTEXITCODE -ne 0) { throw "motely-node build failed!" }

    Write-Host ""
    Write-Host "  motely-node built -> $nodeOutDir" -ForegroundColor Green

    # Pack it
    Push-Location $nodeOutDir
    npm pack
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw "npm pack failed for motely-node!" }
    $nodeTarball = Get-ChildItem "motely-node-*.tgz" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Write-Host "  Tarball : $($nodeTarball.FullName)" -ForegroundColor Green
    Pop-Location
    Write-Host ""
}

Write-Host "=== Done ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To publish to npm:" -ForegroundColor Magenta
if (-not $NodeOnly) { Write-Host "  cd Motely.BrowserWasm/motely-wasm && npm publish" }
if (-not $WasmOnly) { Write-Host "  cd Motely.npm-staging/motely-node && npm publish" }
