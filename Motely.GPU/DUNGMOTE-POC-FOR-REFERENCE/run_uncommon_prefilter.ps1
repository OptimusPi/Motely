# Run script for uncommon negative prefilter
param(
    [Parameter(Position=0)]
    [string]$SeedCount = "1000000",
    
    [Parameter(Position=1)]
    [string]$Antes = "1,2,3,4,5,6,7,8",
    
    [Parameter(Position=2)]
    [string]$StartSeed = "11111111",
    
    [Parameter(Position=3)]
    [string]$OutputFile = ""
)

$exe = ".\negative_uncommon_prefilter.exe"

if (-not (Test-Path $exe)) {
    Write-Host "Error: $exe not found. Build it first with: .\build.ps1 uncommon_prefilter" -ForegroundColor Red
    exit 1
}

$args = @($SeedCount, $Antes, $StartSeed)
if ($OutputFile) {
    $args += $OutputFile
}

Write-Host "Running uncommon negative prefilter..." -ForegroundColor Green
Write-Host "Seed Count: $SeedCount" -ForegroundColor Cyan
Write-Host "Antes: $Antes" -ForegroundColor Cyan
Write-Host "Start Seed: $StartSeed" -ForegroundColor Cyan
if ($OutputFile) {
    Write-Host "Output: $OutputFile" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Tip: Monitor progress in another terminal with:" -ForegroundColor Yellow
    Write-Host "  Get-Content $OutputFile -Tail 10 -Wait" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Tip: To resume from last seed:" -ForegroundColor Yellow
    Write-Host "  `$lastSeed = (Get-Content $OutputFile | Sort-Object | Select-Object -Last 1).Split(',')[0]" -ForegroundColor Gray
    Write-Host "  .\run_uncommon_prefilter.ps1 $SeedCount `"$Antes`" `$lastSeed $OutputFile" -ForegroundColor Gray
    Write-Host ""
}
Write-Host ""

& $exe $args

