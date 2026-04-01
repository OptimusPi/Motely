<#
.SYNOPSIS
    Build, pack, and optionally publish the motely-wasm npm package.

.NOTES
    npm pack / npm publish require the npm CLI (typically via Node.js). If you do not use Node locally,
    run this only in CI or a machine with npm installed.

.DESCRIPTION
    dotnet publish -> Bootsharp emits the full npm package into motely-wasm/ (ST) or motely-wasm-mt/ (MT).

    Run from the repo root:
      ./Build-NpmPackages.ps1              # ST build + pack
      ./Build-NpmPackages.ps1 -MT          # MT build + pack
      ./Build-NpmPackages.ps1 -MT -Publish # MT build + npm publish
      ./Build-NpmPackages.ps1 -BuildOnly   # dotnet publish only, skip npm entirely

.PARAMETER MT
    Build the multi-thread (pthread) variant instead of single-thread.

.PARAMETER Publish
    Skip pack, go straight to npm publish --access public.

.PARAMETER BuildOnly
    dotnet publish only. No npm steps.
#>
param([switch]$MT, [switch]$Publish, [switch]$BuildOnly)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot "Motely.BrowserWasm" "Motely.BrowserWasm.csproj"

# Read MotelyVersion from Directory.Packages.props
$propsPath = Join-Path $PSScriptRoot "Directory.Packages.props"
[xml]$props = Get-Content $propsPath
$version = $props.Project.PropertyGroup.MotelyVersion
if (-not $version) { throw "MotelyVersion not found in $propsPath" }

if ($MT) {
    $outDir = Join-Path $PSScriptRoot "Motely.BrowserWasm" "motely-wasm-mt"
    $pkgName = "motely-wasm-mt"
    Write-Host "Building $pkgName v$version (multi-thread)..." -ForegroundColor Cyan
    dotnet publish $project -c Release /p:MotelyWasmThreads=true
} else {
    $outDir = Join-Path $PSScriptRoot "Motely.BrowserWasm" "motely-wasm"
    $pkgName = "motely-wasm"
    Write-Host "Building $pkgName v$version (single-thread)..." -ForegroundColor Cyan
    dotnet publish $project -c Release
}
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Stamp MotelyVersion into package.json (template uses 0.0.0; avoid a separate Node step)
$pkgJson = Join-Path $outDir "package.json"
$raw = Get-Content -LiteralPath $pkgJson -Raw
$updated = [regex]::Replace($raw, '"version"\s*:\s*"[^"]*"', "`"version`": `"$version`"", 1, [System.Text.RegularExpressions.RegexOptions]::None)
if ($updated -eq $raw) { throw "Could not stamp version in $pkgJson" }
Set-Content -LiteralPath $pkgJson -Value $updated -NoNewline
Write-Host "Built -> $outDir (v$version)" -ForegroundColor Green

if ($BuildOnly) { exit 0 }

Push-Location $outDir
try {
    if ($Publish) {
        if ($env:NPM_TOKEN) {
            "//registry.npmjs.org/:_authToken=$($env:NPM_TOKEN)" | Set-Content ".npmrc"
        }
        npm publish --access public
        if ($LASTEXITCODE -ne 0) { throw "npm publish failed" }
        Write-Host "Published $pkgName@$version!" -ForegroundColor Green
    }
    else {
        npm pack
        if ($LASTEXITCODE -ne 0) { throw "npm pack failed" }
        $t = Get-ChildItem "$pkgName-*.tgz" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        Write-Host "Packed -> $($t.FullName)" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}