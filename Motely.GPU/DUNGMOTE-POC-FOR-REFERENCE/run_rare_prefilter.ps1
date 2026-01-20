# Run script for negative_rare_prefilter
param(
    [Parameter(Position=0)]
    [string]$Count = "1000000",
    
    [Parameter(Position=1)]
    [string]$Antes = "1,2,3,4,5,6,7,8",
    
    [Parameter(Position=2)]
    [string]$StartSeed = "11111111",
    
    [Parameter(Position=3)]
    [string]$OutputFile = ""
)

if (-not (Test-Path "negative_rare_prefilter.exe")) {
    Write-Host "Executable not found. Building..." -ForegroundColor Yellow
    .\build.ps1 rare_prefilter
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Running negative_rare_prefilter..." -ForegroundColor Green
Write-Host "Count: $Count" -ForegroundColor Cyan
Write-Host "Antes: $Antes" -ForegroundColor Cyan
Write-Host "Start: $StartSeed" -ForegroundColor Cyan
if ($OutputFile) {
    Write-Host "Output: $OutputFile" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Tip: Monitor progress in another terminal with:" -ForegroundColor Yellow
    Write-Host "  Get-Content $OutputFile -Tail 10 -Wait" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Tip: To resume from last seed:" -ForegroundColor Yellow
    Write-Host "  `$lastSeed = (Get-Content $OutputFile | Sort-Object | Select-Object -Last 1).Split(',')[0]" -ForegroundColor Gray
    Write-Host "  .\run_rare_prefilter.ps1 $Count `"$Antes`" `$lastSeed $OutputFile" -ForegroundColor Gray
    Write-Host ""
}
Write-Host ""

if ($OutputFile) {
    .\negative_rare_prefilter.exe $Count $Antes $StartSeed $OutputFile
} else {
    .\negative_rare_prefilter.exe $Count $Antes $StartSeed
}

