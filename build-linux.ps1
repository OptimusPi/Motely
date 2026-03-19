<#
.SYNOPSIS
  Build linux-x64 native AOT addon via Docker (Ubuntu 22.04 / glibc 2.35).
  Outputs to motely-node/bin/linux-x64/Motely.NodeAddon.node
.NOTES
  Requires Docker Desktop running. No WSL dotnet needed.
  Uses Dockerfile.linux-node (Ubuntu Jammy) for Vercel glibc compatibility.
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "[linux-x64] Building via Docker (Ubuntu 22.04/glibc)..." -ForegroundColor Yellow

$outDir = Join-Path $root 'motely-node\bin\linux-x64'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# Build the image (Ubuntu 22.04/glibc for Vercel compatibility)
docker build -f "$root\Dockerfile.linux-node" --target build -t motely-node-build "$root"
if ($LASTEXITCODE) { throw "Docker build failed" }

# Extract the .node file via a temporary run
docker run --rm -v "${outDir}:/dest" motely-node-build sh -c "cp /out/linux-x64/*.node /dest/"
if ($LASTEXITCODE) { throw "Docker copy failed" }

if (-not (Test-Path "$outDir\Motely.NodeAddon.node")) {
  throw "Binary not found at $outDir\Motely.NodeAddon.node"
}

$size = (Get-Item "$outDir\Motely.NodeAddon.node").Length / 1MB
Write-Host "[linux-x64] OK: $outDir\Motely.NodeAddon.node ($([math]::Round($size,1)) MB)" -ForegroundColor Green
