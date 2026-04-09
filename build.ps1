<#
.SYNOPSIS
    Build (delegates to publish.ps1).

.DESCRIPTION
    .\build.ps1              # full build + wasm, no npm
    .\build.ps1 -WasmOnly    # wasm publish only
    .\build.ps1 -NpmPublish  # same as .\publish.ps1 -Publish
#>
param([switch]$WasmOnly, [switch]$NpmPublish)

if ($NpmPublish) {
    & "$PSScriptRoot\publish.ps1" -WasmOnly:$WasmOnly -Npm Publish
}
else {
    & "$PSScriptRoot\publish.ps1" -WasmOnly:$WasmOnly -Npm None
}
