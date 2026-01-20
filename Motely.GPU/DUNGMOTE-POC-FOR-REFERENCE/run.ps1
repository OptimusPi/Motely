# Run script for soul_joker_filter_search
param(
    [Parameter(Position=0)]
    [string]$Count = "1000000",
    
    [Parameter(Position=1)]
    [string]$StartSeed = ""
)

if (-not (Test-Path "soul_joker_filter_search.exe")) {
    Write-Host "Executable not found. Building..."
    .\build.ps1 soul_search
}

if ($StartSeed) {
    .\soul_joker_filter_search.exe $Count $StartSeed
} else {
    .\soul_joker_filter_search.exe $Count
}

