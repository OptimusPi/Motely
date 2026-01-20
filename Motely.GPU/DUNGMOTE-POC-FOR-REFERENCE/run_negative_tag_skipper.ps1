# Interactive run script for negative_tag_skipper
param(
    [string]$JamlFile = "",
    [string]$Joker = "",
    [string]$Jokers = "",
    [int]$Ante = 0,
    [string]$Antes = "",
    [int]$MinHits = 0,
    [int]$JokerRolls = 0,
    [int]$StartBatch = -1,
    [int]$EndBatch = -1,
    [int]$BatchChars = 0,
    [string]$OutputFile = "",
    [switch]$NoProgress,
    [int]$ProgressMs = 0,
    [switch]$AutoMin,
    [int]$AutoMinPrint = 0,
    [int]$SeedsPerThread = 0,
    [switch]$Interactive
)

# Build if needed
if (-not (Test-Path "negative_tag_skipper.exe")) {
    Write-Host "Executable not found. Building..." -ForegroundColor Yellow
    .\build.ps1 negative_tag_skipper
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

# Interactive mode
if ($Interactive -or ($JamlFile -eq "" -and $Joker -eq "" -and $Jokers -eq "")) {
    Write-Host ""
    Write-Host "=== Negative Tag Skipper ===" -ForegroundColor Cyan
    Write-Host ""
    
    # JAML file or manual config
    if ($JamlFile -eq "") {
        $jamlInput = Read-Host "JAML file path (or press Enter to configure manually)"
        if ($jamlInput -and $jamlInput.Trim() -ne "") {
            $JamlFile = $jamlInput.Trim()
        }
    }
    
    if ($JamlFile -eq "") {
        # Manual configuration
        Write-Host ""
        Write-Host "Manual Configuration:" -ForegroundColor Yellow
        
        if ($Joker -eq "" -and $Jokers -eq "") {
            $jokerInput = Read-Host "Joker name(s) (comma-separated, e.g. 'OopsAll6s' or 'Blueprint,Brainstorm')"
            if ($jokerInput -and $jokerInput.Contains(',')) {
                $Jokers = $jokerInput
            } elseif ($jokerInput) {
                $Joker = $jokerInput
            }
        }
        
        if ($Ante -eq 0 -and $Antes -eq "") {
            $anteInput = Read-Host "Ante(s) (single number or comma-separated, e.g. '8' or '1,2,3')"
            if ($anteInput -and $anteInput.Contains(',')) {
                $Antes = $anteInput
            } elseif ($anteInput) {
                $Ante = [int]$anteInput
            }
        }
        
        if ($MinHits -eq 0) {
            $minHitsInput = Read-Host "Min hits (default: 4)"
            if ($minHitsInput -and $minHitsInput.Trim() -ne "") {
                $MinHits = [int]$minHitsInput
            } else {
                $MinHits = 4
            }
        }
        
        if ($JokerRolls -eq 0) {
            $rollsInput = Read-Host "Joker rolls (default: 100, use 20-30 for early antes)"
            if ($rollsInput -and $rollsInput.Trim() -ne "") {
                $JokerRolls = [int]$rollsInput
            } else {
                $JokerRolls = 100
            }
        }
        
        if ($StartBatch -eq -1) {
            $startInput = Read-Host "Start batch (default: 0)"
            if ($startInput -and $startInput.Trim() -ne "") {
                $StartBatch = [int]$startInput
            } else {
                $StartBatch = 0
            }
        }
        
        if ($EndBatch -eq -1) {
            $endInput = Read-Host "End batch (REQUIRED - limits search!)"
            if ($endInput -and $endInput.Trim() -ne "") {
                $EndBatch = [int]$endInput
            } else {
                Write-Host "Error: End batch is required!" -ForegroundColor Red
                exit 1
            }
        }
        
        if ($BatchChars -eq 0) {
            $batchCharsInput = Read-Host "Batch chars (2-4, default: 2 = 1,225 seeds/batch)"
            if ($batchCharsInput -and $batchCharsInput.Trim() -ne "") {
                $BatchChars = [int]$batchCharsInput
            }
        }
        
        if ($OutputFile -eq "") {
            $outputInput = Read-Host "Output file (optional, press Enter for stdout)"
            if ($outputInput -and $outputInput.Trim() -ne "") {
                $OutputFile = $outputInput.Trim()
            }
        }
        
        if (-not $AutoMin) {
            $autoMinInput = Read-Host "Auto-min threshold? (y/n, default: n)"
            if ($autoMinInput -and ($autoMinInput -eq 'y' -or $autoMinInput -eq 'Y')) {
                $AutoMin = $true
            }
        }
    }
}

# Build command
$args = @()

if ($JamlFile -ne "") {
    $args += "--jaml-file"
    $args += $JamlFile
} else {
    if ($Joker -ne "") {
        $args += "--joker"
        $args += $Joker
    } elseif ($Jokers -ne "") {
        $args += "--jokers"
        $args += $Jokers
    }
    
    if ($Ante -gt 0) {
        $args += "--ante"
        $args += $Ante.ToString()
    } elseif ($Antes -ne "") {
        $args += "--antes"
        $args += $Antes
    }
    
    if ($MinHits -gt 0) {
        $args += "--min-hits"
        $args += $MinHits.ToString()
    }
    
    if ($JokerRolls -gt 0) {
        $args += "--joker-rolls"
        $args += $JokerRolls.ToString()
    }
    
    if ($StartBatch -ge 0) {
        $args += "--start-batch"
        $args += $StartBatch.ToString()
    }
    
    if ($EndBatch -ge 0) {
        $args += "--end-batch"
        $args += $EndBatch.ToString()
    }
    
    if ($BatchChars -gt 0) {
        $args += "--batch-chars"
        $args += $BatchChars.ToString()
    }
}

if ($OutputFile -ne "") {
    $args += "--output-file"
    $args += $OutputFile
}

if ($NoProgress) {
    $args += "--no-progress"
}

if ($ProgressMs -gt 0) {
    $args += "--progress-ms"
    $args += $ProgressMs.ToString()
}

if ($AutoMin) {
    $args += "--auto-min"
    if ($AutoMinPrint -gt 0) {
        $args += "--auto-min-print"
        $args += $AutoMinPrint.ToString()
    }
}

if ($SeedsPerThread -gt 0) {
    $args += "--seeds-per-thread"
    $args += $SeedsPerThread.ToString()
}

# Display config
Write-Host ""
Write-Host "Running negative_tag_skipper..." -ForegroundColor Green
Write-Host ""
if ($JamlFile -ne "") {
    Write-Host "JAML file: $JamlFile" -ForegroundColor Cyan
} else {
    if ($Joker -ne "") {
        Write-Host "Joker: $Joker" -ForegroundColor Cyan
    } elseif ($Jokers -ne "") {
        Write-Host "Jokers: $Jokers" -ForegroundColor Cyan
    }
    if ($Ante -gt 0) {
        Write-Host "Ante: $Ante" -ForegroundColor Cyan
    } elseif ($Antes -ne "") {
        Write-Host "Antes: $Antes" -ForegroundColor Cyan
    }
    if ($MinHits -gt 0) {
        Write-Host "Min hits: $MinHits" -ForegroundColor Cyan
    }
    if ($JokerRolls -gt 0) {
        Write-Host "Joker rolls: $JokerRolls" -ForegroundColor Cyan
    }
    if ($StartBatch -ge 0) {
        Write-Host "Start batch: $StartBatch" -ForegroundColor Cyan
    }
    if ($EndBatch -ge 0) {
        Write-Host "End batch: $EndBatch" -ForegroundColor Cyan
    }
    if ($BatchChars -gt 0) {
        Write-Host "Batch chars: $BatchChars" -ForegroundColor Cyan
    }
}
if ($OutputFile -ne "") {
    Write-Host "Output: $OutputFile" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Tip: Monitor progress in another terminal with:" -ForegroundColor Yellow
    Write-Host "  Get-Content $OutputFile -Tail 10 -Wait" -ForegroundColor Gray
    Write-Host ""
}
Write-Host ""

# Run
& .\negative_tag_skipper.exe $args
