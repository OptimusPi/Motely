# Build linux-x64 in Docker (Ubuntu 22.04 = glibc 2.35 = Vercel-safe).
# Run from MotelyJAML repo root.
# Requires: Docker
$ErrorActionPreference = "Stop"
$root = (Get-Location).Path
@(
    'Motely\obj',
    'Motely.Orchestration\obj',
    'Motely.NodeAddon\obj'
) | ForEach-Object {
    $path = Join-Path $root $_
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path
        Write-Host "Removed stale $_ before linux Docker publish" -ForegroundColor DarkGray
    }
}
$src = (Get-Location).Path -replace '\\', '/'
$src = $src -replace '\\', '/'
docker build -f Dockerfile.linux-node -t motely-linux-node .
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
docker run --rm -v "${src}:/src" -w /src motely-linux-node
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Done. Check motely-node/bin/linux-x64/Motely.NodeAddon.node"
