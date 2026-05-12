#!/usr/bin/env pwsh
# clean.ps1 - wipe build artifacts so the next publish starts fresh.
# Run from repo root: .\clean.ps1
#
# Removes:
#   - motely-wasm/        (Bootsharp ES module output; stale files here re-fold into publishes)
#   - **/bin, **/obj      (per-project build output)
#   - dotnet clean        (NuGet/MSBuild caches for the solution)

param(
    [switch]$KeepWasm,    # skip wiping motely-wasm/
    [switch]$KeepBinObj   # skip wiping bin/ and obj/
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Remove-IfExists($path) {
    if (Test-Path -LiteralPath $path) {
        Write-Host "  rm $path"
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

Write-Host "==> dotnet clean"
& dotnet clean (Join-Path $root 'Motely.slnx') --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { Write-Warning "dotnet clean exited $LASTEXITCODE (continuing)" }

if (-not $KeepBinObj) {
    Write-Host "==> remove bin/ and obj/ recursively"
    # Materialize before removing — iterating a live recursive enumeration while
    # deleting its yielded directories races against Get-ChildItem descending into them.
    # Sort deepest-first so child bin/obj go before their parents.
    $dirs = @(
        Get-ChildItem -LiteralPath $root -Recurse -Directory -Force -ErrorAction SilentlyContinue `
        | Where-Object { $_.Name -in @('bin', 'obj') -and $_.FullName -notmatch '\\\.git\\' } `
        | Sort-Object -Property @{ Expression = { $_.FullName.Length }; Descending = $true }
    )
    foreach ($dir in $dirs) {
        Remove-IfExists $dir.FullName
    }
}

if (-not $KeepWasm) {
    Write-Host "==> remove motely-wasm/ (Bootsharp publish output)"
    Remove-IfExists (Join-Path $root 'motely-wasm')
}

Write-Host "done."
