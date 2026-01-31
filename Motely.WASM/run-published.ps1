<#
.SYNOPSIS
Publish Motely.WASM (Release) then serve the publish output at http://localhost:3333.
Use this to test the exact bundle that would be shipped (trimmed, same as npm/weejoker).
#>
param(
    [switch]$NoPublish,
    [int]$Port = 3333
)

$ErrorActionPreference = "Stop"
$PublishDir = Join-Path $PSScriptRoot "bin/Release/net10.0-browser/browser-wasm/publish"

if (-not $NoPublish) {
    Write-Host "Publishing Motely.WASM (Release)..." -ForegroundColor Cyan
    dotnet publish -c Release
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish output not found at: $PublishDir. Run without -NoPublish first."
}

Write-Host ""
Write-Host "Serving published bundle at http://localhost:$Port (COOP/COEP for WASM threads)" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host ""

$ServerScript = Join-Path $PSScriptRoot "scripts\serve-with-headers.cjs"
node $ServerScript $Port $PublishDir
