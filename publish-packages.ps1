<#
.SYNOPSIS
  Build + pack motely-wasm and motely-node for npm publish.
.PARAMETER Publish
  Also npm publish both packages after packing.
.PARAMETER SkipNode
  Skip the motely-node native AOT build.
.PARAMETER SkipWasm
  Skip the motely-wasm WASM AOT build.
.PARAMETER SkipBump
  Skip auto-bumping the patch version.
.PARAMETER SkipClean
  Skip cleaning bin/obj directories.
#>
param(
  [switch]$Publish,
  [switch]$SkipNode,
  [switch]$SkipWasm,
  [switch]$SkipBump,
  [switch]$SkipClean
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# ── 1. Bump patch version ────────────────────────────────────────────────────
$propsPath = Join-Path $root 'Directory.Packages.props'
$xml = [xml](Get-Content $propsPath -Raw)
$vnode = $xml.SelectSingleNode('//*[local-name()="MotelyVersion"]')
if (-not $vnode) { throw "MotelyVersion not found in Directory.Packages.props" }

if (-not $SkipBump) {
  $parts = $vnode.InnerText.Trim().Split('.')
  $old = $parts -join '.'
  $parts[2] = [string]([int]$parts[2] + 1)
  $version = $parts -join '.'
  $vnode.InnerText = $version
  $xml.Save($propsPath)
  Write-Host "$old -> $version" -ForegroundColor Cyan
} else {
  $version = $vnode.InnerText.Trim()
  Write-Host "version: $version (no bump)" -ForegroundColor Cyan
}

# Sync version into both package.json files
foreach ($pkgRel in @('motely-wasm\package.json', 'motely-node\package.json')) {
  $pkgPath = Join-Path $root $pkgRel
  $pkg = Get-Content $pkgPath -Raw | ConvertFrom-Json
  $pkg.version = $version
  $pkg | ConvertTo-Json -Depth 10 | Set-Content $pkgPath -Encoding UTF8
  Write-Host "  $($pkg.name): $version" -ForegroundColor DarkGray
}

# ── 2. Clean ─────────────────────────────────────────────────────────────────
if (-not $SkipClean) {
  Write-Host "`n[clean]" -ForegroundColor Yellow
  @(
    'Motely\bin', 'Motely\obj',
    'Motely.Orchestration\bin', 'Motely.Orchestration\obj',
    'Motely.BrowserWasm\bin', 'Motely.BrowserWasm\obj',
    'Motely.NodeAddon\bin', 'Motely.NodeAddon\obj',
    'motely-wasm\_framework', 'motely-wasm\_framework_st',
    'motely-node\bin'
  ) | ForEach-Object {
    $d = Join-Path $root $_
    if (Test-Path $d) { Remove-Item -Recurse -Force $d; Write-Host "  rm $_" -ForegroundColor DarkGray }
  }
}

# ── 3. motely-wasm ───────────────────────────────────────────────────────────
$wasmTgz = $null
if (-not $SkipWasm) {
  # 3a. Threaded build
  Write-Host "`n[wasm] dotnet publish (threaded)" -ForegroundColor Yellow
  dotnet publish (Join-Path $root 'Motely.BrowserWasm\Motely.BrowserWasm.csproj') -c Release
  if ($LASTEXITCODE) { throw "BrowserWasm threaded publish failed" }

  # Stage _framework
  $fwSrc = Join-Path $root 'Motely.BrowserWasm\bin\Release\net10.0-browser\publish\wwwroot\_framework'
  $fwDst = Join-Path $root 'motely-wasm\_framework'
  if (-not (Test-Path $fwSrc)) { throw "Publish output missing: $fwSrc" }
  if (Test-Path $fwDst) { Remove-Item -Recurse -Force $fwDst }
  Copy-Item $fwSrc $fwDst -Recurse -Force
  Write-Host "  staged _framework" -ForegroundColor DarkGray

  # 3b. Single-thread build
  # Must nuke obj/ between threaded and single-thread builds (assembly attribute conflict)
  $objDir = Join-Path $root 'Motely.BrowserWasm\obj'
  if (Test-Path $objDir) { Remove-Item -Recurse -Force $objDir }

  Write-Host "`n[wasm] dotnet publish (single-thread)" -ForegroundColor Yellow
  $stPub = Join-Path $root 'Motely.BrowserWasm\bin\Release\net10.0-browser-st\publish'
  dotnet publish (Join-Path $root 'Motely.BrowserWasm\Motely.BrowserWasm.csproj') -c Release `
    -p:SingleThread=true "-p:PublishDir=$stPub"
  if ($LASTEXITCODE) { throw "BrowserWasm single-thread publish failed" }

  # Stage _framework_st (same logic as stage-packages.mjs but for ST)
  $stSrc = Join-Path $stPub 'wwwroot\_framework'
  $stDst = Join-Path $root 'motely-wasm\_framework_st'
  if (Test-Path $stDst) { Remove-Item -Recurse -Force $stDst }
  Copy-Item $stSrc $stDst -Recurse -Force
  Write-Host "  staged _framework_st" -ForegroundColor DarkGray

  # 3c. TypeScript compile + pack
  Write-Host "`n[wasm] npm build + pack" -ForegroundColor Yellow
  Push-Location (Join-Path $root 'motely-wasm')
  try {
    npm install --ignore-scripts
    npm run build
    npm pack
  }
  finally { Pop-Location }

  $wasmTgz = (Get-ChildItem (Join-Path $root 'motely-wasm\motely-wasm-*.tgz') |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

# ── 4. motely-node ───────────────────────────────────────────────────────────
$nodeTgz = $null
if (-not $SkipNode) {
  # Copy jaml-schema from motely-wasm so both packages ship it
  $wasmDir = Join-Path $root 'motely-wasm'
  $nodeDir = Join-Path $root 'motely-node'
  foreach ($f in @('jaml-schema.js', 'jaml-schema.d.ts', 'jaml.schema.json')) {
    Copy-Item (Join-Path $wasmDir $f) (Join-Path $nodeDir $f) -Force
  }

  # Build linux-x64 via WSL (Ubuntu with .NET SDK)
  Write-Host "`n[node] dotnet publish linux-x64 via WSL" -ForegroundColor Yellow
  $wslRoot = wsl wslpath -u ($root -replace '\\', '\\')
  wsl bash -c "cd '$wslRoot' && dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r linux-x64"
  if ($LASTEXITCODE) { throw "linux-x64 publish failed" }

  $nodeAddon = Join-Path $nodeDir 'bin\linux-x64\Motely.NodeAddon.node'
  if (-not (Test-Path $nodeAddon)) { throw "linux-x64 binary missing: $nodeAddon" }
  Write-Host "  ok: $nodeAddon" -ForegroundColor DarkGray

  # Pack
  Write-Host "`n[node] npm pack" -ForegroundColor Yellow
  Push-Location $nodeDir
  try { npm pack }
  finally { Pop-Location }

  $nodeTgz = (Get-ChildItem (Join-Path $nodeDir 'motely-node-*.tgz') |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

# ── 5. Summary ───────────────────────────────────────────────────────────────
Write-Host "`n========================================" -ForegroundColor Cyan
if ($wasmTgz)  { Write-Host "  motely-wasm  $version  $wasmTgz" -ForegroundColor Green }
if ($nodeTgz)  { Write-Host "  motely-node  $version  $nodeTgz" -ForegroundColor Green }
Write-Host "========================================" -ForegroundColor Cyan

# ── 6. Publish ───────────────────────────────────────────────────────────────
if ($Publish) {
  if ($wasmTgz)  { npm publish $wasmTgz --access public }
  if ($nodeTgz)  { npm publish $nodeTgz --access public }
  Write-Host "`nPublished v$version" -ForegroundColor Green
} else {
  Write-Host "`nDry run. To publish:" -ForegroundColor Yellow
  if ($wasmTgz)  { Write-Host "  npm publish `"$wasmTgz`" --access public" }
  if ($nodeTgz)  { Write-Host "  npm publish `"$nodeTgz`" --access public" }
}
