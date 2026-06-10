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

# ── 1. Pack Bootsharp.FileSystem ──────────────────────────────────────────────
Write-Host ""
Write-Host "=== [1/7] Pack Bootsharp.FileSystem ===" -ForegroundColor Cyan
$fsCs = "D:\extra\bootsharp\cs"
Remove-Item "$fsCs\.nuget\*.nupkg" -ErrorAction SilentlyContinue
dotnet pack "$fsCs\Bootsharp.FileSystem\Bootsharp.FileSystem.csproj" --configuration Release --output "$fsCs\.nuget"
$pkg = Get-ChildItem "$fsCs\.nuget\Bootsharp.FileSystem.*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $pkg) { throw "No Bootsharp.FileSystem .nupkg found in $fsCs\.nuget" }
Write-Host "==> Packed: $($pkg.Name)" -ForegroundColor Yellow

# ── 2. Push to local feed + update Directory.Packages.props ──────────────────
Write-Host ""
Write-Host "=== [2/7] Push FileSystem to local feed ===" -ForegroundColor Cyan
dotnet nuget push $pkg.FullName --source bootsharp-filesystem --skip-duplicate
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
node "$root\Motely.Wasm\motely.test.mjs"
if ($LASTEXITCODE -ne 0) { throw "JS tests FAILED — aborting publish" }

# ── 7. npm publish ────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== [7/7] npm publish ===" -ForegroundColor Cyan
Push-Location "$root\Motely.Wasm"
try { npm publish } finally { Pop-Location }

Write-Host ""
Write-Host "=== DONE — motely-wasm 20.2.0 published ===" -ForegroundColor Green
