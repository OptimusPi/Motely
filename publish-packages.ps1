<#
.SYNOPSIS
  Build + pack motely-wasm and motely-node. Bumps patch automatically.
  Pass -Publish to also npm publish both.
  Pass -SkipClean to skip the clean step.
  Pass -SkipNode to skip the motely-node build.
#>
param([switch]$Publish, [switch]$SkipClean, [switch]$SkipNode)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# ── 1. Bump patch version ─────────────────────────────────────────────────────
$propsPath = Join-Path $root 'Directory.Packages.props'
$xml = [xml](Get-Content $propsPath -Raw)
$vnode = $xml.SelectSingleNode('//*[local-name()="MotelyVersion"]')
if (-not $vnode) { throw "MotelyVersion not found in Directory.Packages.props" }
$parts = $vnode.InnerText.Trim().Split('.')
$old = $parts -join '.'
$parts[2] = [string]([int]$parts[2] + 1)
$version = $parts -join '.'
$vnode.InnerText = $version
$xml.Save($propsPath)
Write-Host "$old → $version" -ForegroundColor Cyan

# ── 2. Clean ──────────────────────────────────────────────────────────────────
if (-not $SkipClean) {
  @(
    'Motely\bin','Motely\obj',
    'Motely.Orchestration\bin','Motely.Orchestration\obj',
    'Motely.BrowserWasm\bin','Motely.BrowserWasm\obj',
    'Motely.NodeAddon\bin','Motely.NodeAddon\obj',
    'motely-wasm\_framework','motely-wasm\_framework_st',
    'motely-node\bin','motely-node\pkg'
  ) | ForEach-Object {
    $d = Join-Path $root $_
    if (Test-Path $d) { Remove-Item -Recurse -Force $d; Write-Host "  rm $_" -ForegroundColor DarkGray }
  }
}

# ── Helper: stage _framework from publish output ─────────────────────────────
# Nukes destination first to prevent stale fingerprinted files piling up.
# Backs up worker.js and restores it after (it's a committed source file).
function Stage-Framework([string]$publishRoot, [string]$dst) {
  $src = Join-Path $publishRoot 'wwwroot\_framework'
  if (-not (Test-Path $src)) { throw "Publish output missing: $src" }

  # Back up worker.js if it exists
  $workerBackup = $null
  $workerPath = Join-Path $dst 'worker.js'
  if (Test-Path $workerPath) { $workerBackup = Get-Content $workerPath -Raw }

  # Nuke and recreate
  if (Test-Path $dst) { Remove-Item -Recurse -Force $dst }
  New-Item -ItemType Directory -Force $dst | Out-Null

  $count = 0
  Get-ChildItem $src -Recurse -File |
    Where-Object { $_.Extension -notin '.br','.gz' } |
    ForEach-Object {
      $rel = $_.FullName.Substring($src.Length).TrimStart('\','/')
      $target = Join-Path $dst $rel
      $dir = Split-Path $target
      if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
      Copy-Item $_.FullName $target -Force
      $count++
    }

  # Restore worker.js
  if ($workerBackup) { Set-Content $workerPath $workerBackup -NoNewline }

  Write-Host "  staged $count files → $dst" -ForegroundColor DarkGray
}

# ── 3. BrowserWasm — threaded ─────────────────────────────────────────────────
Write-Host "`n[wasm/threaded] dotnet publish" -ForegroundColor Yellow
dotnet publish (Join-Path $root 'Motely.BrowserWasm\Motely.BrowserWasm.csproj') -c Release
if ($LASTEXITCODE) { throw "BrowserWasm threaded publish failed" }
Stage-Framework (Join-Path $root 'Motely.BrowserWasm\bin\Release\net10.0-browser\publish') `
                (Join-Path $root 'motely-wasm\_framework')

# ── 4. BrowserWasm — single-thread ───────────────────────────────────────────
Write-Host "`n[wasm/single-thread] dotnet publish" -ForegroundColor Yellow
$stPub = Join-Path $root 'Motely.BrowserWasm\bin\Release\net10.0-browser-st\publish'
dotnet publish (Join-Path $root 'Motely.BrowserWasm\Motely.BrowserWasm.csproj') -c Release `
  -p:SingleThread=true "-p:PublishDir=$stPub"
if ($LASTEXITCODE) { throw "BrowserWasm single-thread publish failed" }
Stage-Framework $stPub (Join-Path $root 'motely-wasm\_framework_st')

# ── 5. motely-wasm: install → build → pack ───────────────────────────────────
Write-Host "`n[motely-wasm] npm install + build + pack" -ForegroundColor Yellow
Push-Location (Join-Path $root 'motely-wasm')
try {
  npm install --ignore-scripts; if ($LASTEXITCODE) { throw "npm install failed" }
  npm run build;               if ($LASTEXITCODE) { throw "npm run build failed" }
  npm pack;                    if ($LASTEXITCODE) { throw "npm pack failed" }
} finally { Pop-Location }

$wasmTgz = (Get-ChildItem (Join-Path $root 'motely-wasm\motely-wasm-*.tgz') |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

# ── 6. motely-node: win-x64 + linux-x64 ──────────────────────────────────────
if (-not $SkipNode) {
  Write-Host "`n[motely-node] dotnet publish win-x64" -ForegroundColor Yellow
  dotnet publish (Join-Path $root 'Motely.NodeAddon\Motely.NodeAddon.csproj') -c Release -r win-x64
  if ($LASTEXITCODE) { throw "NodeAddon win-x64 failed" }
  # linux-x64 is triggered automatically by PublishLinuxX64 MSBuild target via Docker

  $nodeTgz = (Get-ChildItem (Join-Path $root 'motely-node\pkg\motely-node-*.tgz') |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

# ── 7. Summary ────────────────────────────────────────────────────────────────
Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  motely-wasm  $version  →  $wasmTgz" -ForegroundColor Green
if (-not $SkipNode) { Write-Host "  motely-node  $version  →  $nodeTgz" -ForegroundColor Green }
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

if ($Publish) {
  npm publish $wasmTgz --access public
  if (-not $SkipNode) { npm publish $nodeTgz --access public }
  Write-Host "`nPublished v$version" -ForegroundColor Green
} else {
  Write-Host "`nTo publish:" -ForegroundColor Yellow
  Write-Host "  npm publish `"$wasmTgz`" --access public"
  if (-not $SkipNode) { Write-Host "  npm publish `"$nodeTgz`" --access public" }
}
