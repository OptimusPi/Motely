#!/usr/bin/env pwsh
# clean.ps1 — nuke every build output (bin / obj / dist / publish) across the repo.
# Usage: .\clean.ps1
# Safe: removes only generated output dirs, never source.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Generated/regenerable directory names to remove anywhere they appear.
# node_modules included (regenerable via `npm install`); matched at top level so we remove
# the whole dir, never pick at its insides.
$targets = @('bin', 'obj', 'dist', 'publish', 'node_modules')

$removed = 0
Get-ChildItem -Path $root -Directory -Recurse -Force |
    Where-Object { $targets -contains $_.Name -and $_.FullName -notmatch '\\node_modules\\' } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        Write-Host "rm $($_.FullName)" -ForegroundColor DarkGray
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $removed++
    }

Write-Host ""
Write-Host "=== clean done — removed $removed build-output dirs ===" -ForegroundColor Green
