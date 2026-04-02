<#
.SYNOPSIS
    One-shot: regen schema, build language tooling, dotnet publish WASM, npm publish.

.DESCRIPTION
    ./publish-npm.ps1           # full build + publish
    ./publish-npm.ps1 -DryRun   # build but npm pack --dry-run only
#>
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$wasmDir = Join-Path $root "Motely.BrowserWasm"
$wasmProj = Join-Path $wasmDir "Motely.BrowserWasm.csproj"
$wasmOut  = Join-Path $wasmDir "motely-wasm"

# ── 1. Regenerate JAML schema from C# source ───────────────────────────────
Write-Host "`n==> Regenerate JAML schema" -ForegroundColor Cyan
dotnet run --project (Join-Path $root "Motely.CLI\Motely.CLI.csproj") -- --write-jaml-schema
if ($LASTEXITCODE -ne 0) { throw "Schema generation failed" }

# ── 2. Regenerate grammar + Monaco tokenizer, rebuild language tooling ──────
Write-Host "`n==> Rebuild language tooling" -ForegroundColor Cyan
node (Join-Path $root "tools\jaml-language\gen-grammar.mjs")
node (Join-Path $root "tools\jaml-language\gen-monaco.mjs")
Push-Location (Join-Path $root "tools\jaml-language")
try { pnpm build } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw "Language tooling build failed" }

# ── 3. dotnet publish — Bootsharp emits the full npm package ───────────────
Write-Host "`n==> dotnet publish" -ForegroundColor Cyan
dotnet publish $wasmProj -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Write-Host "Package ready -> $wasmOut" -ForegroundColor Green

# ── 4. npm publish or dry-run ───────────────────────────────────────────────
Push-Location $wasmOut
try {
    if ($DryRun) {
        npm pack --dry-run
    } else {
        if ($env:NPM_TOKEN) {
            "//registry.npmjs.org/:_authToken=$($env:NPM_TOKEN)" | Set-Content ".npmrc"
        }
        npm publish --access public
        if ($LASTEXITCODE -ne 0) { throw "npm publish failed" }
        Write-Host "Published motely-wasm" -ForegroundColor Green
    }
} finally {
    Pop-Location
}
