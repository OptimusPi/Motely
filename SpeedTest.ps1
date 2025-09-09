# Motely Filter Speed Test Script
# Compares performance between original and optimized filters

param(
    [int]$SeedCount = 1000000,
    [int]$Iterations = 5,
    [string]$FilterFile = "",
    [switch]$UseOptimized = $false
)

Write-Host "Motely Filter Speed Test" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# Build the project first
Write-Host "Building Motely..." -ForegroundColor Yellow
dotnet build -c Release --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Create test filter if not provided
if (-not $FilterFile) {
    $FilterFile = "speed-test.json"
    $testFilter = @{
        must = @(
            @{
                type = "Joker"
                value = "Blueprint"
                antes = @(1, 2, 3)
                sources = @{
                    shopSlots = 0..9
                }
            },
            @{
                type = "Voucher"
                value = "MagicTrick"
                antes = @(1)
            }
        )
    } | ConvertTo-Json -Depth 10
    
    $testFilter | Out-File -FilePath $FilterFile -Encoding UTF8
    Write-Host "Created test filter: $FilterFile" -ForegroundColor Green
}

# Generate random seeds for testing
function Generate-RandomSeeds {
    param([int]$Count)
    
    $seeds = @()
    $chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    
    for ($i = 0; $i -lt $Count; $i++) {
        $seed = ""
        for ($j = 0; $j -lt 8; $j++) {
            $seed += $chars[(Get-Random -Maximum $chars.Length)]
        }
        $seeds += $seed
    }
    
    return $seeds
}

Write-Host "Generating $SeedCount random seeds..." -ForegroundColor Yellow
$testSeeds = Generate-RandomSeeds -Count $SeedCount
$seedFile = "speed-test-seeds.txt"
$testSeeds | Out-File -FilePath $seedFile -Encoding UTF8
Write-Host "Seeds generated!" -ForegroundColor Green
Write-Host ""

# Test function
function Test-FilterPerformance {
    param(
        [string]$FilterType,
        [string]$FilterPath,
        [string]$SeedPath,
        [int]$Iterations
    )
    
    Write-Host "Testing $FilterType filter..." -ForegroundColor Cyan
    
    $times = @()
    $results = @()
    
    for ($i = 1; $i -le $Iterations; $i++) {
        Write-Host "  Iteration $i/$Iterations..." -NoNewline
        
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        
        # Run Motely with the filter
        if ($FilterType -eq "Optimized") {
            $output = & dotnet run --no-build -c Release -- search -f $FilterPath -s $SeedPath --optimized 2>&1
        } else {
            $output = & dotnet run --no-build -c Release -- search -f $FilterPath -s $SeedPath 2>&1
        }
        
        $sw.Stop()
        $elapsed = $sw.ElapsedMilliseconds
        
        $times += $elapsed
        
        # Extract match count from output
        $matchLine = $output | Select-String -Pattern "Found (\d+) matching seeds"
        if ($matchLine) {
            $matches = [int]($matchLine.Matches[0].Groups[1].Value)
            $results += $matches
            Write-Host " $elapsed ms (found $matches seeds)" -ForegroundColor Green
        } else {
            Write-Host " $elapsed ms" -ForegroundColor Yellow
        }
    }
    
    # Calculate statistics
    $avgTime = ($times | Measure-Object -Average).Average
    $minTime = ($times | Measure-Object -Minimum).Minimum
    $maxTime = ($times | Measure-Object -Maximum).Maximum
    $stdDev = [Math]::Sqrt((($times | ForEach-Object { [Math]::Pow($_ - $avgTime, 2) }) | Measure-Object -Sum).Sum / $times.Count)
    
    return @{
        FilterType = $FilterType
        AverageMs = [Math]::Round($avgTime, 2)
        MinMs = $minTime
        MaxMs = $maxTime
        StdDev = [Math]::Round($stdDev, 2)
        SeedsPerSecond = [Math]::Round($SeedCount / ($avgTime / 1000), 0)
        MatchCount = if ($results.Count -gt 0) { $results[0] } else { 0 }
    }
}

# Run tests
Write-Host "Starting performance tests..." -ForegroundColor Yellow
Write-Host "Testing with $SeedCount seeds, $Iterations iterations each" -ForegroundColor Gray
Write-Host ""

$results = @()

# Test original filter
$originalResult = Test-FilterPerformance -FilterType "Original" -FilterPath $FilterFile -SeedPath $seedFile -Iterations $Iterations
$results += $originalResult

Write-Host ""

# Test optimized filter
$optimizedResult = Test-FilterPerformance -FilterType "Optimized" -FilterPath $FilterFile -SeedPath $seedFile -Iterations $Iterations
$results += $optimizedResult

Write-Host ""
Write-Host "Performance Summary" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host ""

# Display results in a table
$results | Format-Table -Property @(
    @{Label="Filter Type"; Expression={$_.FilterType}; Width=12},
    @{Label="Avg (ms)"; Expression={$_.AverageMs}; Width=10},
    @{Label="Min (ms)"; Expression={$_.MinMs}; Width=10},
    @{Label="Max (ms)"; Expression={$_.MaxMs}; Width=10},
    @{Label="Std Dev"; Expression={$_.StdDev}; Width=10},
    @{Label="Seeds/sec"; Expression={$_.SeedsPerSecond}; Width=12},
    @{Label="Matches"; Expression={$_.MatchCount}; Width=10}
) -AutoSize

# Calculate improvement
if ($results.Count -eq 2) {
    $improvement = [Math]::Round((($originalResult.AverageMs - $optimizedResult.AverageMs) / $originalResult.AverageMs) * 100, 2)
    $speedup = [Math]::Round($originalResult.AverageMs / $optimizedResult.AverageMs, 2)
    
    Write-Host ""
    if ($improvement -gt 0) {
        Write-Host "Performance Improvement: $improvement% faster" -ForegroundColor Green
        Write-Host "Speedup: ${speedup}x" -ForegroundColor Green
    } elseif ($improvement -lt 0) {
        Write-Host "Performance Regression: $([Math]::Abs($improvement))% slower" -ForegroundColor Red
    } else {
        Write-Host "Performance: No significant difference" -ForegroundColor Yellow
    }
    
    # Verify correctness
    if ($originalResult.MatchCount -eq $optimizedResult.MatchCount) {
        Write-Host "Correctness: ✓ Both filters found the same number of matches" -ForegroundColor Green
    } else {
        Write-Host "Correctness: ✗ Filters found different number of matches!" -ForegroundColor Red
        Write-Host "  Original: $($originalResult.MatchCount) matches" -ForegroundColor Yellow
        Write-Host "  Optimized: $($optimizedResult.MatchCount) matches" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Cyan

# Clean up temporary files
if (Test-Path "speed-test.json") {
    Remove-Item "speed-test.json" -Force
}
if (Test-Path "speed-test-seeds.txt") {
    Remove-Item "speed-test-seeds.txt" -Force
}