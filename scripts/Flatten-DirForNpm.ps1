<#
.SYNOPSIS
    Deep-copy a directory file-by-file so the result has no hard links (new file data per path).

.DESCRIPTION
    npm registry rejects uploads (415 / "Hard link is not allowed") when the tarball
    contains hard-link entries. MSBuild Copy duplicates and some Windows dedup layouts
    can produce hard-linked duplicates. This script copies with [System.IO.File]::Copy only.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [Parameter(Mandatory = $true)]
    [string]$DestinationPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "SourcePath does not exist: $SourcePath"
}
$srcRoot = (Resolve-Path -LiteralPath $SourcePath).Path.TrimEnd('\')
if (-not (Test-Path -LiteralPath $srcRoot -PathType Container)) {
    throw "SourcePath is not a directory: $SourcePath"
}

if (Test-Path -LiteralPath $DestinationPath) {
    Remove-Item -LiteralPath $DestinationPath -Recurse -Force
}
$null = New-Item -ItemType Directory -Path $DestinationPath -Force
$destRoot = (Resolve-Path -LiteralPath $DestinationPath).Path.TrimEnd('\')

$prefixLen = $srcRoot.Length + 1
foreach ($file in Get-ChildItem -Path $srcRoot -Recurse -File) {
    $relative = $file.FullName.Substring($prefixLen)
    $targetFile = Join-Path $destRoot $relative
    $parent = Split-Path $targetFile -Parent
    if (-not (Test-Path -LiteralPath $parent)) {
        $null = New-Item -ItemType Directory -Path $parent -Force
    }
    [System.IO.File]::Copy($file.FullName, $targetFile, $false)
}
