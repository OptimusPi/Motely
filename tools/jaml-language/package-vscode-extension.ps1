param(
  [switch]$SkipInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rootScript = Join-Path (Split-Path $PSScriptRoot -Parent -Parent) 'package-vscode-extension.ps1'

if (-not (Test-Path $rootScript)) {
  throw "Could not find root packaging script at $rootScript"
}

& $rootScript @args
