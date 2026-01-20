# HIP Build Script - Convenience wrapper for AMD GPU builds
param(
    [Parameter(Position=0)]
    [string]$Target = "showman_consecutive"
)

# Set HIP environment variable and call main build script
$env:HIP = "1"
& .\build.ps1 -Target $Target -HIP
