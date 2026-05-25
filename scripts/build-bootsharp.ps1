#!/usr/bin/env pwsh
<#
.SYNOPSIS
    End-to-end Bootsharp + Bootsharp.FileSystem rebuild and Motely.Wasm publish.

.DESCRIPTION
    Single source of truth for the local Bootsharp pipeline. Resets D:\bootsharp
    to origin/feat/spec, applies the vendored patch series under patches/, bumps
    the alpha version, packs Bootsharp core and Bootsharp.FileSystem to their
    local NuGet feeds, bumps the pins in Directory.Packages.props, publishes
    Motely.Wasm, and runs the two Node smoke tests.

    See BOOTSHARP-BUILD.md for the prose explanation of every step.

.PARAMETER BootsharpRoot
    Path to the Bootsharp core checkout. Default D:\bootsharp.

.PARAMETER BootsharpFsRoot
    Path to the Bootsharp.FileSystem (sponsor) checkout. Default D:\extra\bootsharp.

.PARAMETER Branch
    Bootsharp upstream branch to reset to. Default feat/spec.

.PARAMETER AlphaBumpMode
    How to bump <Version> in Bootsharp's Directory.Build.props:
        auto              increment the NNN in 0.8.0-alpha.NNN (default)
        none              leave as-is
        <explicit string> use literally (e.g. 0.8.0-alpha.400)

.PARAMETER SkipSmoke
    Skip the Node smoke tests at the end (motely.test.mjs + getctx-wasm.test.mjs).

.PARAMETER WhatIf
    Print every command without executing.
#>
[CmdletBinding()]
param(
    [string] $BootsharpRoot   = 'D:\bootsharp',
    [string] $BootsharpFsRoot = 'D:\extra\bootsharp',
    [string] $Branch          = 'feat/spec',
    [string] $AlphaBumpMode   = 'auto',
    [switch] $SkipSmoke,
    [switch] $WhatIf
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Step ([string] $msg) {
    Write-Host ''
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Run ([scriptblock] $block, [string] $label) {
    if ($label) { Write-Host "    $label" -ForegroundColor DarkGray }
    Write-Host "    $block" -ForegroundColor DarkGray
    if (-not $WhatIf) {
        & $block
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            throw "Command failed (exit $LASTEXITCODE): $block"
        }
    }
}

# ── 1. Sanity-check tooling ──────────────────────────────────────────────────
Step '1/11 Sanity-check tooling'
$tools = @{
    git    = 'git --version'
    dotnet = 'dotnet --version'
    node   = 'node --version'
    npm    = 'npm --version'
    bash   = 'bash --version'   # Bootsharp pack.sh needs a POSIX shell
}
$missing = @()
foreach ($name in $tools.Keys) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { $missing += $name }
}
if ($missing.Count -gt 0) {
    $msg = @"
Missing required tool(s): $($missing -join ', ')
    Install hints:
      git     https://git-scm.com/
      dotnet  .NET 10 SDK with: dotnet workload install wasm-tools
      node    Node 22 LTS (>=20.6.0 required)
      bash    Git for Windows ships Git Bash; WSL also works
"@
    if ($WhatIf) { Write-Host $msg -ForegroundColor Yellow }
    else { throw $msg }
} else {
    Write-Host '    OK' -ForegroundColor Green
}

# ── 2. Reset Bootsharp core ──────────────────────────────────────────────────
Step "2/11 Reset $BootsharpRoot to origin/$Branch"
if (-not (Test-Path $BootsharpRoot)) {
    $msg = "BootsharpRoot does not exist: $BootsharpRoot"
    if ($WhatIf) { Write-Host "    $msg" -ForegroundColor Yellow }
    else { throw $msg }
}
Run { git -C $BootsharpRoot fetch --all --prune }
Run { git -C $BootsharpRoot reset --hard "origin/$Branch" }

if (-not $WhatIf) {
    $dirty = git -C $BootsharpRoot status --porcelain --untracked-files=no
    if ($dirty) {
        throw "Bootsharp working tree has tracked-file changes after reset:`n$dirty"
    }
}

# ── 3. Apply patch series ────────────────────────────────────────────────────
Step '3/11 Apply patches/*.patch (in lexical order)'
$patchDir = Join-Path $RepoRoot 'patches'
$patches = @()
if (Test-Path $patchDir) {
    $patches = Get-ChildItem -Path $patchDir -Filter '*.patch' -File |
        Sort-Object Name
}
if ($patches.Count -eq 0) {
    Write-Host '    (no patches to apply)' -ForegroundColor Yellow
} else {
    foreach ($p in $patches) {
        Write-Host "    -> $($p.Name)" -ForegroundColor DarkGray
        Run { git -C $BootsharpRoot apply --check $p.FullName } "check $($p.Name)"
        Run { git -C $BootsharpRoot apply       $p.FullName } "apply $($p.Name)"
    }
}

# Surface the placeholder so a fresh agent doesn't think the series is complete.
$placeholder = Join-Path $patchDir '01-projectability.patch.PLACEHOLDER'
if (Test-Path $placeholder) {
    Write-Host ''
    Write-Host '    NOTE: patches/01-projectability.patch.PLACEHOLDER exists.' -ForegroundColor Yellow
    Write-Host '    The projectability fix has NOT been vendored. Motely.Wasm publish' -ForegroundColor Yellow
    Write-Host '    will fail until the maintainer exports the stash. See the file.' -ForegroundColor Yellow
}

# ── 4. Bump <Version> in Bootsharp ───────────────────────────────────────────
Step '4/11 Bump <Version> in Bootsharp/src/cs/Directory.Build.props'
$bsProps = Join-Path $BootsharpRoot 'src/cs/Directory.Build.props'
$bsPropsMissing = -not (Test-Path $bsProps)
if ($bsPropsMissing) {
    if (-not $WhatIf) { throw "Not found: $bsProps" }
    Write-Host "    (skipped: $bsProps not found)" -ForegroundColor Yellow
}

$newAlphaVersion = $null
if ($bsPropsMissing) {
    $newAlphaVersion = '<unknown-in-WhatIf>'
} elseif ($AlphaBumpMode -eq 'none') {
    $newAlphaVersion = ([xml](Get-Content $bsProps -Raw)).Project.PropertyGroup.Version
    Write-Host "    leaving version unchanged: $newAlphaVersion" -ForegroundColor DarkGray
} elseif ($AlphaBumpMode -eq 'auto') {
    $content = Get-Content $bsProps -Raw
    if ($content -notmatch '<Version>(?<v>0\.8\.0-alpha\.(?<n>\d+))</Version>') {
        throw "Couldn't find 0.8.0-alpha.NNN <Version> in $bsProps. Pass -AlphaBumpMode explicitly."
    }
    $newN = [int]$Matches['n'] + 1
    $newAlphaVersion = "0.8.0-alpha.$newN"
    Write-Host "    $($Matches['v'])  ->  $newAlphaVersion" -ForegroundColor DarkGray
    if (-not $WhatIf) {
        ($content -replace '<Version>0\.8\.0-alpha\.\d+</Version>', "<Version>$newAlphaVersion</Version>") |
            Set-Content $bsProps -NoNewline
    }
} else {
    $newAlphaVersion = $AlphaBumpMode
    Write-Host "    setting version literally: $newAlphaVersion" -ForegroundColor DarkGray
    if (-not $WhatIf) {
        (Get-Content $bsProps -Raw) `
            -replace '<Version>[^<]+</Version>', "<Version>$newAlphaVersion</Version>" |
            Set-Content $bsProps -NoNewline
    }
}

# ── 5. Build JS, then pack C# ────────────────────────────────────────────────
Step '5/11 npm run build (Bootsharp JS) + pack.sh (Bootsharp C#)'
Run { npm --prefix (Join-Path $BootsharpRoot 'src/js') run build }
Run { bash (Join-Path $BootsharpRoot 'src/cs/.scripts/pack.sh') }

# ── 6. Pack Bootsharp.FileSystem ─────────────────────────────────────────────
Step '6/11 dotnet pack Bootsharp.FileSystem'
$fsCs     = Join-Path $BootsharpFsRoot 'cs'
$fsNuget  = Join-Path $fsCs '.nuget'
if (-not (Test-Path $fsCs)) {
    if (-not $WhatIf) { throw "Not found: $fsCs" }
    Write-Host "    (skipped: $fsCs not found)" -ForegroundColor Yellow
} else {
    Run { dotnet pack $fsCs --configuration Release --output $fsNuget }
}

$newFsVersion = $null
if (-not $WhatIf) {
    $fsPkg = Get-ChildItem -Path $fsNuget -Filter 'Bootsharp.FileSystem.*.nupkg' -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $fsPkg) { throw "No Bootsharp.FileSystem nupkg produced in $fsNuget" }
    if ($fsPkg.BaseName -notmatch '^Bootsharp\.FileSystem\.(?<v>.+)$') {
        throw "Cannot parse version from $($fsPkg.Name)"
    }
    $newFsVersion = $Matches['v']
    Write-Host "    packed: $($fsPkg.Name)  (version $newFsVersion)" -ForegroundColor DarkGray
}

# ── 7. Purge NuGet cache ─────────────────────────────────────────────────────
Step '7/11 Purge NuGet user cache for these versions'
if (-not $WhatIf) {
    $cacheRoot = Join-Path $env:USERPROFILE '.nuget/packages'
    foreach ($pkg in 'bootsharp', 'bootsharp.common', 'bootsharp.inject') {
        $p = Join-Path $cacheRoot "$pkg/$newAlphaVersion"
        if (Test-Path $p) {
            Write-Host "    rm $p" -ForegroundColor DarkGray
            Remove-Item -Recurse -Force -LiteralPath $p
        }
    }
    if ($newFsVersion) {
        $p = Join-Path $cacheRoot "bootsharp.filesystem/$newFsVersion"
        if (Test-Path $p) {
            Write-Host "    rm $p" -ForegroundColor DarkGray
            Remove-Item -Recurse -Force -LiteralPath $p
        }
    }
} else {
    Write-Host '    (would purge bootsharp / .common / .inject / .filesystem at the new versions)' -ForegroundColor DarkGray
}

# ── 8. Bump pins in Directory.Packages.props ─────────────────────────────────
Step '8/11 Bump pins in Directory.Packages.props'
$pkgProps = Join-Path $RepoRoot 'Directory.Packages.props'
if (-not (Test-Path $pkgProps)) { throw "Not found: $pkgProps" }

if (-not $WhatIf) {
    [xml]$xml = Get-Content $pkgProps -Raw
    $changed = @()
    foreach ($pv in $xml.Project.ItemGroup.PackageVersion) {
        switch ($pv.Include) {
            'Bootsharp'            { if ($pv.Version -ne $newAlphaVersion) { $changed += "$($pv.Include): $($pv.Version) -> $newAlphaVersion"; $pv.Version = $newAlphaVersion } }
            'Bootsharp.Common'     { if ($pv.Version -ne $newAlphaVersion) { $changed += "$($pv.Include): $($pv.Version) -> $newAlphaVersion"; $pv.Version = $newAlphaVersion } }
            'Bootsharp.Inject'     { if ($pv.Version -ne $newAlphaVersion) { $changed += "$($pv.Include): $($pv.Version) -> $newAlphaVersion"; $pv.Version = $newAlphaVersion } }
            'Bootsharp.FileSystem' { if ($newFsVersion -and $pv.Version -ne $newFsVersion) { $changed += "$($pv.Include): $($pv.Version) -> $newFsVersion"; $pv.Version = $newFsVersion } }
        }
    }
    $xml.Save($pkgProps)
    if ($changed.Count -eq 0) {
        Write-Host '    (no changes — already current)' -ForegroundColor DarkGray
    } else {
        foreach ($line in $changed) { Write-Host "    $line" -ForegroundColor DarkGray }
    }
} else {
    Write-Host "    (would set Bootsharp/.Common/.Inject -> $newAlphaVersion, .FileSystem -> <packed timestamp>)" -ForegroundColor DarkGray
}

# ── 9. Publish Motely.Wasm ───────────────────────────────────────────────────
Step '9/11 dotnet publish Motely.Wasm -c Release'
Run { dotnet publish (Join-Path $RepoRoot 'Motely.Wasm') -c Release }

# ── 10. Smoke ─────────────────────────────────────────────────────────────────
Step '10/11 Smoke (Node)'
if ($SkipSmoke) {
    Write-Host '    -SkipSmoke: skipped' -ForegroundColor Yellow
} else {
    Run { node (Join-Path $RepoRoot 'Motely.Wasm/motely.test.mjs') }
    Run { node (Join-Path $RepoRoot 'Motely.Wasm/getctx-wasm.test.mjs') }
}

# ── 11. Summary ──────────────────────────────────────────────────────────────
Step '11/11 Summary'
Write-Host "    Bootsharp core version : $newAlphaVersion"
if ($newFsVersion) { Write-Host "    Bootsharp.FileSystem   : $newFsVersion" }
Write-Host "    Core feed              : $(Join-Path $BootsharpRoot 'src/cs/.nuget')"
Write-Host "    FileSystem feed        : $fsNuget"
Write-Host "    Motely.Wasm publish    : $(Join-Path $RepoRoot 'motely-wasm/dist')"
if (-not $SkipSmoke -and -not $WhatIf) {
    Write-Host '    RESULT: PASS' -ForegroundColor Green
}
