#!/usr/bin/env pwsh
# Clean all bin/obj folders and motely-wasm publish output.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$targets = Get-ChildItem -Path $root -Recurse -Directory -Force `
    -ErrorAction SilentlyContinue |
    Where-Object { ($_.Name -eq 'bin' -or $_.Name -eq 'obj') -and $_.FullName -notmatch '\\node_modules\\' }

foreach ($dir in $targets) {
    Write-Host "rm $($dir.FullName)"
    Remove-Item -Recurse -Force -LiteralPath $dir.FullName
}

foreach ($sub in 'dist', 'bin', 'node_modules') {
    $p = Join-Path $root "motely-wasm\$sub"
    if (Test-Path -LiteralPath $p) {
        Write-Host "rm $p"
        Remove-Item -Recurse -Force -LiteralPath $p
    }
}

# Clean stray tarballs and node_modules from Motely.Wasm
foreach ($tgz in Get-ChildItem -Path (Join-Path $root 'Motely.Wasm') -Filter '*.tgz' -ErrorAction SilentlyContinue) {
    Write-Host "rm $($tgz.FullName)"
    Remove-Item -Force -LiteralPath $tgz.FullName
}
$wasmNm = Join-Path $root 'Motely.Wasm\node_modules'
if (Test-Path -LiteralPath $wasmNm) {
    Write-Host "rm $wasmNm"
    Remove-Item -Recurse -Force -LiteralPath $wasmNm
}

Write-Host 'clean: done'
