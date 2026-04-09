<#
.SYNOPSIS
  Deletes generated / output-only trees under the repo (not source).

.DESCRIPTION
  - bin / obj: .NET build output (never under node_modules).
  - dist: bundler/tsc output (never under node_modules).
  - Motely.BrowserWasm/motely-wasm, motely-wasm-compat: Bootsharp npm publish folders.
  - tools/jaml-language/vscode-extension: *.vsix and copied jaml.schema.json (recreated by build).

  Run .\clean.ps1 before a from-scratch build if you want zero stale artifacts.
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Test-UnderNodeModulesOrGit([string]$FullPath) {
    return $FullPath -match '\\node_modules\\' -or $FullPath -match '\\.git\\'
}

# Longest paths first so nested dirs disappear cleanly.
$dirPatterns = @(
    @{ Name = 'bin' },
    @{ Name = 'obj' },
    @{ Name = 'dist' }
)

$removedDirs = 0
foreach ($pat in $dirPatterns) {
    $targets = Get-ChildItem -LiteralPath $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -eq $pat.Name -and -not (Test-UnderNodeModulesOrGit $_.FullName)
        } |
        Sort-Object { $_.FullName.Length } -Descending

    foreach ($t in $targets) {
        Remove-Item -LiteralPath $t.FullName -Recurse -Force
        Write-Host "Removed $($t.FullName)"
        $removedDirs++
    }
}

$wasmPublish = @(
    'Motely.BrowserWasm\motely-wasm',
    'Motely.BrowserWasm\motely-wasm-compat'
)
foreach ($rel in $wasmPublish) {
    $p = Join-Path $root $rel
    if (Test-Path -LiteralPath $p) {
        Remove-Item -LiteralPath $p -Recurse -Force
        Write-Host "Removed $p"
        $removedDirs++
    }
}

$vsixDir = Join-Path $root 'tools\jaml-language\vscode-extension'
if (Test-Path -LiteralPath $vsixDir) {
    Get-ChildItem -LiteralPath $vsixDir -Filter '*.vsix' -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
        Write-Host "Removed $($_.FullName)"
    }
}

$stagedSchema = Join-Path $root 'tools\jaml-language\vscode-extension\jaml.schema.json'
if (Test-Path -LiteralPath $stagedSchema) {
    Remove-Item -LiteralPath $stagedSchema -Force
    Write-Host "Removed $stagedSchema"
}

Write-Host "Done. Removed $removedDirs output directory tree(s); VSIX/schema cleanup as applicable."
