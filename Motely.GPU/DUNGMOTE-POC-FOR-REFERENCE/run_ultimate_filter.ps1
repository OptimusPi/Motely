# Run script for ultimate_filter
param(
    [string]$StartBatch = "0",
    [string]$EndBatch = "-1",
    [string]$BatchChars = "4",
    [string]$StartAnte = "2",
    [string]$EndAnte = "8",
    [string]$JokerRolls = "7",
    [string]$MinScore = "3",
    [string]$Jokers = "Blueprint,HangingChad,OopsAll6s"
)

$script:topResults = @()

function Update-TopResults {
    param([string]$line)
    if ([string]::IsNullOrWhiteSpace($line)) { return }
    if (-not $line.Contains(',')) { return }
    $parts = $line.Split(',')
    if ($parts.Length -lt 2) { return }
    $seed = $parts[0].Trim()
    $score = 0
    if (-not [int]::TryParse($parts[1].Trim(), [ref]$score)) { return }
    $existing = $script:topResults | Where-Object { $_.Seed -eq $seed }
    if ($existing) {
        if ($score -gt $existing.Score) { $existing.Score = $score }
    } else {
        $script:topResults += [PSCustomObject]@{ Seed = $seed; Score = $score }
    }
    $script:topResults = $script:topResults | Sort-Object -Property Score -Descending | Select-Object -First 10
}

function Show-TopResults {
    Write-Host ""
    Write-Host "=== Top 10 Results ===" -ForegroundColor Cyan
    if ($script:topResults -and $script:topResults.Count -gt 0) {
        $script:topResults | Sort-Object -Property Score -Descending | Select-Object -First 10 | ForEach-Object {
            Write-Host "$($_.Seed),$($_.Score)" -ForegroundColor Green
        }
    } else {
        Write-Host "No results found." -ForegroundColor Yellow
    }
    Write-Host ""
}

$null = Register-EngineEvent PowerShell.Exiting -Action { Show-TopResults }

if (-not (Test-Path "ultimate_filter.exe")) {
    Write-Host "Executable not found. Building..." -ForegroundColor Yellow
    .\build.ps1 ultimate_filter
    if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }
}

Write-Host "Running ultimate_filter..." -ForegroundColor Green
Write-Host "Start Batch: $StartBatch" -ForegroundColor Cyan
Write-Host "End Batch: $EndBatch (use -1 for all)" -ForegroundColor Cyan
Write-Host "Antes: $StartAnte-$EndAnte" -ForegroundColor Cyan
Write-Host "Joker Rolls: $JokerRolls" -ForegroundColor Cyan
Write-Host "Jokers: $Jokers" -ForegroundColor Cyan
Write-Host "Min Score: $MinScore" -ForegroundColor Cyan
Write-Host ""

$exeArgs = @("--start-batch", $StartBatch, "--batch-chars", $BatchChars, "--start-ante", $StartAnte, "--end-ante", $EndAnte, "--jokers", $Jokers, "--joker-rolls", $JokerRolls, "--min-score", $MinScore)
if ($EndBatch -ne "-1") { $exeArgs += "--end-batch", $EndBatch }

$ErrorActionPreference = "Continue"
try {
    & ".\ultimate_filter.exe" @exeArgs 2>&1 | ForEach-Object {
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
} catch {
    # Ignore pipeline errors  
} finally {
    Show-TopResults
}
