#!/usr/bin/env pwsh
# publish.ps1 — bump, build, publish motely-wasm + motely-node
# motely-wasm: builds natively (browser-wasm, no platform dependency)
# motely-node: builds inside Docker (linux-x64 NativeAOT)
#
# Sources:
#   https://bootsharp.com/guide/getting-started
#   https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csproj = 'Motely.Orchestration/Motely.Orchestration.csproj'

# ── Bump version ──────────────────────────────────────────────
$propsPath = Join-Path $root 'Directory.Packages.props'
$props = Get-Content $propsPath -Raw
if ($props -notmatch '<MotelyVersion>(\d+)\.(\d+)\.(\d+)</MotelyVersion>') {
    Write-Error 'MotelyVersion not found in Directory.Packages.props'
}
$maj, $min, $pat = [int]$Matches[1], [int]$Matches[2], [int]$Matches[3]
$old = "$maj.$min.$pat"
$pat++
$new = "$maj.$min.$pat"
$props = $props -replace '<MotelyVersion>[^<]+<', "<MotelyVersion>$new<"
Set-Content $propsPath $props -NoNewline
Write-Host "`n  version: $old -> $new`n"

# ── Helper: write version into package.json right before npm publish ──
function Sync-Version($pkg) {
    $p = Join-Path $root $pkg 'package.json'
    if (!(Test-Path $p)) { Write-Error "$p not found" }
    $j = Get-Content $p -Raw | ConvertFrom-Json
    $j.version = $new
    $j | ConvertTo-Json -Depth 10 | Set-Content $p
    Write-Host "  $pkg/package.json -> $new"
}

function Ensure-NpmPublishAccess($pkg) {
    $who = & npm whoami 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($who | Out-String))) {
        Write-Error "npm authentication required before publishing $pkg. Run 'npm login' and verify the current account has publish access to '$pkg'."
    }

    $user = ($who | Out-String).Trim()
    Write-Host "  npm authenticated as $user"
}

function Write-JamlSchema() {
    & dotnet run --project 'Motely.CLI/Motely.CLI.csproj' -c Release -- --write-jaml-schema
    if ($LASTEXITCODE -ne 0) { Write-Error 'JAML schema generation failed' }
}

function Stage-WasmSchema() {
    $schemaSource = Join-Path $root 'jaml.schema.json'
    $schemaTarget = Join-Path $root 'motely-wasm/dist/jaml.schema.json'
    if (!(Test-Path $schemaSource)) { Write-Error "Authoritative schema not found at $schemaSource" }
    $schemaTargetDir = Split-Path $schemaTarget -Parent
    if (!(Test-Path $schemaTargetDir)) { New-Item -ItemType Directory -Path $schemaTargetDir | Out-Null }
    Copy-Item $schemaSource $schemaTarget -Force
    $schema = Get-Content $schemaTarget -Raw | ConvertFrom-Json
    if (-not $schema.properties.id -or -not $schema.properties.hashtags) {
        Write-Error 'Staged motely-wasm schema is missing id or hashtags'
    }
    Write-Host "  staged JAML schema -> motely-wasm/dist/jaml.schema.json"
}

# ── motely-wasm ───────────────────────────────────────────────
Write-Host "`n── motely-wasm ──"
dotnet publish $csproj -c Release -p:WasmBuild=true
if ($LASTEXITCODE -ne 0) { Write-Error 'motely-wasm build failed' }
Write-JamlSchema
Sync-Version 'motely-wasm'
Stage-WasmSchema
Ensure-NpmPublishAccess 'motely-wasm'
npm publish ./motely-wasm

# ── motely-node (Docker → linux-x64) ─────────────────────────
# NativeAOT needs clang + zlib for linking. The dotnet/sdk image doesn't include them.
# Ubuntu 24.04 (dotnet/sdk:10.0 base) also needs libstdc++-12-dev for NativeAOT linking.
# Source: https://aka.ms/nativeaot-prerequisites
# Source: https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html
Write-Host "`n── motely-node ──"
docker run --rm -v "${root}:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 `
    bash -c "set -e && apt-get update -qq && apt-get install -y clang zlib1g-dev libstdc++-12-dev && dotnet publish $csproj -c Release -p:NodeBuild=true -p:PackNpmPackage=false --verbosity normal"
if ($LASTEXITCODE -ne 0) { Write-Error 'motely-node Docker build failed — see output above for the actual error' }

# Copy the linux .node binary from Docker build output into motely-node/
$pubDir = Join-Path $root 'Motely.Orchestration/bin/Release/net10.0/linux-x64/publish'
$nodeFile = Join-Path $pubDir 'Motely.Orchestration.node'
if (!(Test-Path $nodeFile)) { Write-Error "Linux .node binary not found at $nodeFile" }
Copy-Item $nodeFile (Join-Path $root 'motely-node/Motely.Orchestration.node') -Force
Write-Host "  copied linux-x64 .node binary"

# Copy generated .d.ts from build output
$genDir = Join-Path $root 'Motely.Orchestration/bin/Release/net10.0/linux-x64'
$dtsFile = Join-Path $genDir 'Motely.Orchestration.d.ts'
if (Test-Path $dtsFile) {
    Copy-Item $dtsFile (Join-Path $root 'motely-node/Motely.Orchestration.d.ts') -Force
    Write-Host "  copied .d.ts"
}

Sync-Version 'motely-node'
Ensure-NpmPublishAccess 'motely-node'
npm publish ./motely-node

Write-Host "`n  done: motely-wasm@$new + motely-node@$new`n"
