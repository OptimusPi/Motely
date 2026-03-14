# Build linux-x64 in Docker (Ubuntu 22.04 = glibc 2.35 = Vercel-safe).
# Run from MotelyJAML repo root.
# Requires: Docker
$ErrorActionPreference = "Stop"
$src = (Get-Location).Path -replace '\\', '/'
$src = $src -replace '\\', '/'
docker build -f Dockerfile.linux-node -t motely-linux-node .
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
docker run --rm -v "${src}:/src" -w /src motely-linux-node
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Done. Check Motely.node/bin/linux-x64/Motely.NodeAddon.node"
