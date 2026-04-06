<#
.SYNOPSIS
    THE one script: build Motely, publish WASM, optionally npm pack/publish (flattened — no 415 hard-link errors).

.DESCRIPTION
  Same entry point as Build-And-Publish.ps1 (forwarder).

  motely-wasm/ (full npm: wasm bundle, schema, Monaco, types) and motely-wasm-compat/ (minimal: index.mjs + types only) under Motely.BrowserWasm/ after dotnet publish.

  .\publish.ps1                    # full build + wasm; no npm
  .\publish.ps1 -Publish           # + npm publish both packages (NPM_TOKEN or npm login)
  .\publish.ps1 -DryRun            # + npm pack --dry-run both
  .\publish.ps1 -WasmOnly         # only dotnet publish WASM (fast)

  Do not run npm publish from inside motely-wasm/ or motely-wasm-compat/ folders.
#>
param(
    [switch]$WasmOnly,
    [switch]$SkipSolutionBuild,
    [switch]$SkipJamlTooling,
    [ValidateSet('None', 'DryRun', 'Publish')]
    [string]$Npm = 'None',
    [switch]$Publish,
    [switch]$DryRun
)

if ($Publish) { $Npm = 'Publish' }
elseif ($DryRun) { $Npm = 'DryRun' }

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$wasmDir = Join-Path $root 'Motely.BrowserWasm'
$wasmProj = Join-Path $wasmDir 'Motely.BrowserWasm.csproj'
$wasmOut = Join-Path $wasmDir 'motely-wasm'
$wasmCompatOut = Join-Path $wasmDir 'motely-wasm-compat'

function Write-Banner([string]$msg) {
    Write-Host "`n$msg" -ForegroundColor Cyan
}

if (-not $WasmOnly -and -not $SkipSolutionBuild) {
    Write-Banner '==> dotnet build (Motely.sln, Release)'
    dotnet build (Join-Path $root 'Motely.sln') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    Write-Host 'Solution build OK.' -ForegroundColor Green
}

if (-not $WasmOnly -and -not $SkipJamlTooling) {
    Write-Banner '==> Regenerate JAML schema'
    dotnet run --project (Join-Path $root 'Motely.CLI\Motely.CLI.csproj') -- --write-jaml-schema
    if ($LASTEXITCODE -ne 0) { throw 'Schema generation failed' }

    Write-Banner '==> Rebuild jaml-language (grammar, monaco, pnpm build)'
    node (Join-Path $root 'tools\jaml-language\gen-grammar.mjs')
    node (Join-Path $root 'tools\jaml-language\gen-monaco.mjs')
    Push-Location (Join-Path $root 'tools\jaml-language')
    try {
        pnpm build
        if ($LASTEXITCODE -ne 0) { throw 'pnpm build (jaml-language) failed' }
    }
    finally {
        Pop-Location
    }
}

Write-Banner '==> dotnet publish Motely.BrowserWasm (Release)'
dotnet publish $wasmProj -c Release
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (BrowserWasm) failed' }

$flattenScript = Join-Path $root 'scripts\Flatten-DirForNpm.ps1'
if ($Npm -ne 'None' -and -not (Test-Path -LiteralPath $flattenScript)) {
    throw "Missing flatten script: $flattenScript"
}
function Invoke-NpmForPackage {
    param([string]$PackageDir, [string]$Label, [string]$Mode)
    if ($Mode -eq 'None') { return }
    $flatDir = Join-Path $env:TEMP "motely-npm-$Label-$([Guid]::NewGuid().ToString('n'))"
    Write-Host "Flattening $Label for npm (avoids 415 hard links) -> $flatDir" -ForegroundColor DarkGray
    & $flattenScript -SourcePath $PackageDir -DestinationPath $flatDir
    if (-not $?) { throw "Flatten-DirForNpm failed ($Label)" }
    Push-Location $flatDir
    try {
        if ($Mode -eq 'DryRun') {
            Write-Host "`nnpm pack --dry-run ($Label)" -ForegroundColor Cyan
            npm pack --dry-run
            if ($LASTEXITCODE -ne 0) { throw "npm pack failed ($Label)" }
        }
        else {
            if ($env:NPM_TOKEN) {
                "//registry.npmjs.org/:_authToken=$($env:NPM_TOKEN)" | Set-Content '.npmrc'
            }
            # Run npm directly — no 2>&1 / pipes here (muddles exit codes on Windows).
            npm publish --access public
            if ($LASTEXITCODE -ne 0) {
                throw @"
npm publish failed ($Label).
If npm said the version already exists: bump <MotelyVersion> in Directory.Packages.props, run .\publish.ps1 (build only), then .\publish.ps1 -Publish.
"@
            }
            Write-Host "Published $Label" -ForegroundColor Green
        }
    }
    finally {
        Pop-Location
        Remove-Item -LiteralPath $flatDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Npm -ne 'None') {
    $jamlSchemaDir = Join-Path $root 'tools\jaml-language\jaml-schema'
    Write-Banner "==> pnpm install (jaml-language — sync jaml-schema)"
    Push-Location (Join-Path $root 'tools\jaml-language')
    try {
        pnpm install
        if ($LASTEXITCODE -ne 0) { throw 'pnpm install (jaml-language) failed' }
    }
    finally { Pop-Location }

    Write-Banner "==> npm ($Npm) — jaml-schema + motely-wasm + motely-wasm-compat"
    Invoke-NpmForPackage -PackageDir $jamlSchemaDir -Label 'jaml-schema' -Mode $Npm
    Invoke-NpmForPackage -PackageDir $wasmOut -Label 'motely-wasm' -Mode $Npm
    Invoke-NpmForPackage -PackageDir $wasmCompatOut -Label 'motely-wasm-compat' -Mode $Npm
}

$wasmAbs = (Resolve-Path $wasmOut).Path
$compatAbs = (Resolve-Path $wasmCompatOut).Path
Write-Host @"

================================================================================
 WASM outputs:
   motely-wasm          $wasmAbs
   motely-wasm-compat   $compatAbs

 Test:  cd Motely.TestWebsite && npm install && npm run dev
 npm:   .\publish.ps1 -Publish   (or -DryRun)
================================================================================
"@ -ForegroundColor Green
