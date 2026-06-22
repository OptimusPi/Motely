#!/usr/bin/env pwsh
# release.ps1 — build and publish motely-wasm
# Usage: .\release.ps1
#
# Steps:
#   1. Pack Bootsharp.FileSystem → push to local feed
#   2. Update FileSystem version in Directory.Packages.props
#   3. Clear stale obj + dist output
#   4. Restore NuGet packages (no cache)
#   5. dotnet publish Motely.Wasm -c Release
#   6. node motely.test.mjs  (must pass before npm)
#   7. npm publish

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Single source of truth for the published version: <MotelyVersion> in Directory.Packages.props (CPM).
$motelyVersion = ([regex]::Match((Get-Content "$root\Directory.Packages.props" -Raw), '<MotelyVersion>([^<]+)</MotelyVersion>')).Groups[1].Value
if (-not $motelyVersion) { throw "MotelyVersion not found in Directory.Packages.props" }
Write-Host "==> MotelyVersion: $motelyVersion" -ForegroundColor Yellow

# ── 1. Pack Bootsharp.FileSystem ──────────────────────────────────────────────
Write-Host ""
Write-Host "=== [1/7] Pack Bootsharp.FileSystem ===" -ForegroundColor Cyan
$fsCs = "D:\extra\bootsharp\cs"
Remove-Item "$fsCs\.nuget\*.nupkg" -ErrorAction SilentlyContinue
dotnet pack "$fsCs\Bootsharp.FileSystem\Bootsharp.FileSystem.csproj" --configuration Release --output "$fsCs\.nuget"
$pkg = Get-ChildItem "$fsCs\.nuget\Bootsharp.FileSystem.*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $pkg) { throw "No Bootsharp.FileSystem .nupkg found in $fsCs\.nuget" }
Write-Host "==> Packed: $($pkg.Name)" -ForegroundColor Yellow

# ── 2. Update Directory.Packages.props ───────────────────────────────────────
# NOTE: no `dotnet nuget push` here — step 1 packs directly INTO $fsCs\.nuget, which IS the
# `bootsharp-filesystem` local-folder feed. Pushing would copy the file onto itself and fail
# with "used by another process"; it is already in the feed by virtue of being packed there.
# We only need to point CPM at the freshly-packed version.
Write-Host ""
Write-Host "=== [2/7] Update FileSystem version in Directory.Packages.props ===" -ForegroundColor Cyan
$fsVersion = [System.IO.Path]::GetFileNameWithoutExtension($pkg.Name) -replace '^Bootsharp\.FileSystem\.', ''
Write-Host "==> FileSystem version: $fsVersion" -ForegroundColor Yellow
$propsPath = "$root\Directory.Packages.props"
$props = Get-Content $propsPath -Raw
$props = $props -replace 'Include="Bootsharp\.FileSystem" Version="[^"]*"', "Include=`"Bootsharp.FileSystem`" Version=`"$fsVersion`""
Set-Content $propsPath $props -NoNewline

# ── 3. Clear stale output ─────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== [3/7] Clear stale output ===" -ForegroundColor Cyan
Remove-Item "$root\Motely.Wasm\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$root\Motely.Wasm\dist" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "==> Cleared Motely.Wasm/obj and Motely.Wasm/dist"

# ── 4. Restore (bypass http cache so 0.9.0 is fetched fresh) ─────────────────
Write-Host ""
Write-Host "=== [4/7] NuGet restore ===" -ForegroundColor Cyan
dotnet nuget locals http-cache --clear
dotnet restore "$root\Motely.slnx"

# ── 5. Publish (WASM Release build) ──────────────────────────────────────────
Write-Host ""
Write-Host "=== [5/7] dotnet publish Motely.Wasm -c Release ===" -ForegroundColor Cyan
dotnet publish "$root\Motely.Wasm\Motely.Wasm.csproj" -c Release

# ── 6. JS tests ───────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== [6/7] JS tests ===" -ForegroundColor Cyan
node "$root\test.mjs"
if ($LASTEXITCODE -ne 0) { throw "JS tests FAILED — aborting publish" }

# ── 7. npm publish ────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== [7/7] npm publish ===" -ForegroundColor Cyan
# Sync package.json "version" to MotelyVersion (the single source of truth) before publishing.
$pkgJsonPath = "$root\Motely.Wasm\package.json"
$pkgJson = Get-Content $pkgJsonPath -Raw
$pkgJson = $pkgJson -replace '("version"\s*:\s*")[^"]*(")', "`${1}$motelyVersion`${2}"
Set-Content $pkgJsonPath $pkgJson -NoNewline
Write-Host "==> package.json version set to $motelyVersion" -ForegroundColor Yellow
Push-Location "$root\Motely.Wasm"
try { npm publish } finally { Pop-Location }

Write-Host ""
Write-Host "=== DONE — motely-wasm $motelyVersion published ===" -ForegroundColor Green
