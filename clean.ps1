$ErrorActionPreference = 'Stop'

$targets = Get-ChildItem -Path $PSScriptRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Sort-Object { $_.FullName.Length } -Descending

foreach ($target in $targets) {
    Remove-Item -LiteralPath $target.FullName -Recurse -Force
    Write-Host "Removed $($target.FullName)"
}

Write-Host "Done. Removed $($targets.Count) directories."
