# Run script for negative_joker_prefilter with multiple jokers
param(
    [Parameter(Position=0)]
    [string]$StartBatch = "0",
    
    [Parameter(Position=1)]
    [string]$EndBatch = "-1",
    
    [Parameter(Position=2)]
    [string]$BatchChars = "4",
    
    [Parameter(Position=3)]
    [string]$OutputFile = ""
)

if (-not (Test-Path "negative_joker_prefilter.exe")) {
    Write-Host "Executable not found. Building..." -ForegroundColor Yellow
    .\build.ps1 joker_prefilter
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Running negative_joker_prefilter with Blueprint, Brainstorm, OopsAll6s..." -ForegroundColor Green
Write-Host "Start Batch: $StartBatch" -ForegroundColor Cyan
Write-Host "End Batch: $EndBatch (use -1 for all)" -ForegroundColor Cyan
Write-Host "Batch Chars: $BatchChars" -ForegroundColor Cyan
Write-Host "Antes: 2,3,4,5,6" -ForegroundColor Cyan
Write-Host "Joker Rolls: 7" -ForegroundColor Cyan
Write-Host "Jokers: Blueprint,Brainstorm,OopsAll6s" -ForegroundColor Cyan
Write-Host "Require Negative Tag: YES" -ForegroundColor Cyan
if ($OutputFile) {
    Write-Host "Output: $OutputFile" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Tip: Monitor progress in another terminal with:" -ForegroundColor Yellow
    Write-Host "  Get-Content $OutputFile -Tail 10 -Wait" -ForegroundColor Gray
    Write-Host ""
}
Write-Host ""

$cmd = ".\negative_joker_prefilter.exe --start-batch $StartBatch --batch-chars $BatchChars --jokers Blueprint,Brainstorm,OopsAll6s --antes 2,3,4,5,6 --joker-rolls 7 --min-hits 1 --require-negative-tag"

if ($EndBatch -ne "-1") {
    $cmd += " --end-batch $EndBatch"
}

if ($OutputFile) {
    $cmd += " --output-file $OutputFile"
}

Write-Host "Command:" -ForegroundColor Yellow
Write-Host $cmd -ForegroundColor Gray
Write-Host ""

Invoke-Expression $cmd
