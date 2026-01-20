# Run script for negative_tag_skipper with multiple jokers
param(
    [string]$StartBatch = "0",
    [string]$EndBatch = "-1",
    [string]$BatchChars = "4",
    [string]$MinHits = "3",
    [string]$Jokers = "Blueprint,HangingChad,OopsAll6s",
    [string]$OutputFile = ""
)

$script:topResults = @()

function Update-TopResults {
    param([string]$line)
    if ([string]::IsNullOrWhiteSpace($line)) { return }
    if (-not $line.Contains(',')) { return }
    $parts = $line.Split(',')
    if ($parts.Length -lt 2) { return }
    $seed = $parts[0].Trim()
    $hitCount = 0
    if (-not [int]::TryParse($parts[1].Trim(), [ref]$hitCount)) { return }
    $existing = $script:topResults | Where-Object { $_.Seed -eq $seed }
    if ($existing) {
        if ($hitCount -gt $existing.HitCount) { $existing.HitCount = $hitCount }
    } else {
        $script:topResults += [PSCustomObject]@{ Seed = $seed; HitCount = $hitCount }
    }
    $script:topResults = $script:topResults | Sort-Object -Property HitCount -Descending | Select-Object -First 10
}

function Show-TopResults {
    Write-Host ""
    Write-Host "=== Top 10 Results ===" -ForegroundColor Cyan
    if ($script:topResults -and $script:topResults.Count -gt 0) {
        $script:topResults | Sort-Object -Property HitCount -Descending | Select-Object -First 10 | ForEach-Object {
            Write-Host "$($_.Seed),$($_.HitCount)" -ForegroundColor Green
        }
    } else {
        Write-Host "No results found." -ForegroundColor Yellow
    }
    Write-Host ""
}

$null = Register-EngineEvent PowerShell.Exiting -Action { Show-TopResults }

if (-not (Test-Path "negative_tag_skipper.exe")) {
    Write-Host "Executable not found. Building..." -ForegroundColor Yellow
    .\build.ps1 negative_tag_skipper
    if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }
}

Write-Host "Running negative_tag_skipper..." -ForegroundColor Green
Write-Host "Start Batch: $StartBatch" -ForegroundColor Cyan
Write-Host "End Batch: $EndBatch (use -1 for all)" -ForegroundColor Cyan
Write-Host "Batch Chars: $BatchChars" -ForegroundColor Cyan
Write-Host "Antes: 2,3,4,5,6" -ForegroundColor Cyan
Write-Host "Joker Rolls: 7" -ForegroundColor Cyan
Write-Host "Jokers: $Jokers" -ForegroundColor Cyan
Write-Host "Min Hits: $MinHits" -ForegroundColor Cyan
if ($OutputFile) { Write-Host "Output: $OutputFile" -ForegroundColor Cyan }
Write-Host ""

$exeArgs = @("--start-batch", $StartBatch, "--batch-chars", $BatchChars, "--jokers", $Jokers, "--antes", "2,3,4,5,6", "--joker-rolls", "7", "--min-hits", $MinHits)
if ($EndBatch -ne "-1") { $exeArgs += "--end-batch", $EndBatch }
if ($OutputFile) { $exeArgs += "--output-file", $OutputFile }

$ErrorActionPreference = "Continue"
try {
    & ".\negative_tag_skipper.exe" @exeArgs 2>&1 | ForEach-Object {
        $line = $_.ToString()
        if ($null -eq $line) { return }
        if ($line.StartsWith("|")) {
            $csvLine = $line.Substring(1).Trim()
            Update-TopResults $csvLine
            Write-Host $csvLine -ForegroundColor Green
        } elseif ($line.StartsWith("$")) {
            $cleanLine = $line.Substring(1)
            if ($cleanLine.StartsWith("[Progress]")) {
                Write-Host $cleanLine -NoNewline
            } else {
                Write-Host $cleanLine
            }
        }
    }
} finally {
    Show-TopResults
}
