#!/usr/bin/env pwsh
# Clean all bin/obj folders and motely-wasm publish output.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$targets = Get-ChildItem -Path $root -Recurse -Directory -Force `
    -Include 'bin', 'obj' `
    -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\node_modules\\' }

foreach ($dir in $targets) {
    Write-Host "rm $($dir.FullName)"
    Remove-Item -Recurse -Force -LiteralPath $dir.FullName
}

foreach ($sub in 'dist', 'bin') {
    $p = Join-Path $root "motely-wasm\$sub"
    if (Test-Path -LiteralPath $p) {
        Write-Host "rm $p"
        Remove-Item -Recurse -Force -LiteralPath $p
    }
}

Write-Host 'clean: done'
