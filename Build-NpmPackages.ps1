<#
.SYNOPSIS
    Build, pack, and optionally publish the motely-wasm npm package.

.DESCRIPTION
    dotnet publish -> Bootsharp emits the full npm package into motely-wasm/ automatically.
    BootsharpPublishDirectory and BootsharpPackageDirectory both point to motely-wasm/.

    Run from the repo root:
      ./Build-NpmPackages.ps1            # build + pack
      ./Build-NpmPackages.ps1 -Publish   # build + npm publish (uses NPM_TOKEN env or existing npm login)
      ./Build-NpmPackages.ps1 -BuildOnly # dotnet publish only, skip npm entirely

.PARAMETER Publish
    Skip pack, go straight to npm publish --access public.

.PARAMETER BuildOnly
    dotnet publish only. No npm steps.
#>
param([switch]$Publish, [switch]$BuildOnly)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot "Motely.BrowserWasm" "Motely.BrowserWasm.csproj"
# Bootsharp emits the npm package next to the WASM project (see BootsharpPublishDirectory in .csproj).
$outDir = Join-Path $PSScriptRoot "Motely.BrowserWasm" "motely-wasm"

Write-Host "Building motely-wasm..." -ForegroundColor Cyan
dotnet publish $project -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Write-Host "Built -> $outDir" -ForegroundColor Green

if ($BuildOnly) { exit 0 }

Push-Location $outDir
try {
    if ($Publish) {
        if ($env:NPM_TOKEN) {
            "//registry.npmjs.org/:_authToken=$($env:NPM_TOKEN)" | Set-Content ".npmrc"
        }
        npm publish --access public
        if ($LASTEXITCODE -ne 0) { throw "npm publish failed" }
        Write-Host "Published!" -ForegroundColor Green
    }
    else {
        npm pack
        if ($LASTEXITCODE -ne 0) { throw "npm pack failed" }
        $t = Get-ChildItem "motely-wasm-*.tgz" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        Write-Host "Packed -> $($t.FullName)" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}