#Requires -Version 5.1
<#
.SYNOPSIS
  Copies Bootsharp outputs + static site into dist/ (Windows). No Node required.
  Prerequisite: dotnet publish Motely.BrowserWasm -c Release (from repo root).
#>
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Jaml = Split-Path $Root -Parent
$WasmSt = Join-Path $Jaml "Motely.BrowserWasm\motely-wasm"
$WasmMt = Join-Path $Jaml "Motely.BrowserWasm\motely-wasm-mt"
$Dist = Join-Path $Root "dist"

if (-not (Test-Path (Join-Path $WasmSt "index.mjs"))) {
    throw "Missing $WasmSt\index.mjs — run: dotnet publish (Motely.BrowserWasm) -c Release"
}
if (-not (Test-Path (Join-Path $WasmMt "index.mjs"))) {
    throw "Missing $WasmMt\index.mjs — publish MT with /p:MotelyWasmThreads=true"
}

if (Test-Path $Dist) { Remove-Item -Recurse -Force $Dist }
New-Item -ItemType Directory -Path (Join-Path $Dist "coep") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Dist "src") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $Dist "shims") -Force | Out-Null

Copy-Item -Recurse -Force $WasmSt (Join-Path $Dist "motely-wasm")
Copy-Item -Recurse -Force $WasmMt (Join-Path $Dist "motely-wasm-mt")
Copy-Item -Force (Join-Path $Root "src\*.js") (Join-Path $Dist "src")
Copy-Item -Force (Join-Path $Root "shims\*.mjs") (Join-Path $Dist "shims")
Copy-Item -Force (Join-Path $Root "index.html") $Dist
Copy-Item -Force (Join-Path $Root "coep\index.html") (Join-Path $Dist "coep")

Write-Host "build-website: -> $Dist" -ForegroundColor Green
