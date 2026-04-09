<#
.SYNOPSIS
  Builds motely-wasm-compat: minimal npm payload (index.mjs + types only).
  Same Bootsharp bundle as motely-wasm (WASM is base64-embedded in index.mjs); no schema, Monaco, or duplicate assets.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [Parameter(Mandatory = $true)]
    [string]$DestinationPath
)

$ErrorActionPreference = 'Stop'
$index = Join-Path $SourcePath 'index.mjs'
if (-not (Test-Path -LiteralPath $index)) {
    throw "Build-MotelyWasmCompat: missing $index (run Bootsharp publish first)."
}

if (Test-Path -LiteralPath $DestinationPath) {
    Remove-Item -LiteralPath $DestinationPath -Recurse -Force
}
$null = New-Item -ItemType Directory -Force -Path $DestinationPath

Copy-Item -LiteralPath $index -Destination (Join-Path $DestinationPath 'index.mjs') -Force

$typesSrc = Join-Path $SourcePath 'types'
if (Test-Path -LiteralPath $typesSrc) {
    Copy-Item -LiteralPath $typesSrc -Destination (Join-Path $DestinationPath 'types') -Recurse -Force
}
