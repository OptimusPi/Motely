# Motely Filter Speed Test Script using built-in batch processing
# Compares performance between original and optimized filters

param(
    [int]$BatchSize = 8,  # How many character positions to test (8 = full seed space)
    [int]$Threads = 16,   # Number of threads
    [string]$FilterFile = "JsonItemFilters/test-aleeb-blueprint.json",
    [int]$StartBatch = 0,
    [int]$EndBatch = 100  # Test first 100 batches
)

Write-Host "Motely Filter Batch Speed Test" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
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

# Test function using Motely's native batch processing
function Test-FilterPerformance {
    param(
        [string]$FilterPath,
        [int]$Threads,
        [int]$BatchSize,
        [int]$StartBatch,
        [int]$EndBatch
    )
    
    Write-Host "Testing filter with batch processing..." -ForegroundColor Cyan
    Write-Host "  Filter: $FilterPath" -ForegroundColor Gray
    Write-Host "  Threads: $Threads" -ForegroundColor Gray
    Write-Host "  Batch Size: $BatchSize chars" -ForegroundColor Gray
    Write-Host "  Batch Range: $StartBatch to $EndBatch" -ForegroundColor Gray
    Write-Host ""
    
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    
    # Run Motely with native batch processing
    # This uses the built-in seed provider to generate seeds on-the-fly
    $output = & dotnet run --no-build -c Release -- `
        -j (Split-Path -Leaf $FilterPath).Replace('.json', '') `
        --threads $Threads `
        --batchSize $BatchSize `
        --startBatch $StartBatch `
        --endBatch $EndBatch `
        2>&1
    
    $sw.Stop()
    $elapsed = $sw.ElapsedMilliseconds
    
    # Calculate total seeds processed
    $totalSeeds = ($EndBatch - $StartBatch + 1) * [Math]::Pow(36, $BatchSize)
    $seedsPerSecond = [Math]::Round($totalSeeds / ($elapsed / 1000), 0)
    
    Write-Host "Results:" -ForegroundColor Cyan
    Write-Host "  Time: $elapsed ms" -ForegroundColor Green
    Write-Host "  Seeds Processed: $totalSeeds" -ForegroundColor Green
    Write-Host "  Seeds/Second: $seedsPerSecond" -ForegroundColor Green
    
    # Extract matches from output
    $matches = $output | Where-Object { $_ -match '^[A-Z0-9]{8}$' }
    if ($matches) {
        Write-Host "  Matches Found: $($matches.Count)" -ForegroundColor Green
        Write-Host "  Sample Matches:" -ForegroundColor Gray
        $matches | Select-Object -First 5 | ForEach-Object {
            Write-Host "    $_" -ForegroundColor Gray
        }
    }
    
    return @{
        TimeMs = $elapsed
        TotalSeeds = $totalSeeds
        SeedsPerSecond = $seedsPerSecond
        Matches = if ($matches) { $matches.Count } else { 0 }
    }
}

# Run test
$result = Test-FilterPerformance `
    -FilterPath $FilterFile `
    -Threads $Threads `
    -BatchSize $BatchSize `
    -StartBatch $StartBatch `
    -EndBatch $EndBatch

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "========" -ForegroundColor Cyan
$result | Format-Table -AutoSize

# Performance metrics
$millionSeedsTime = [Math]::Round(1000000 / $result.SeedsPerSecond, 2)
Write-Host ""
Write-Host "Performance Metrics:" -ForegroundColor Cyan
Write-Host "  Time to process 1M seeds: $millionSeedsTime seconds" -ForegroundColor Yellow
Write-Host "  Throughput: $($result.SeedsPerSecond) seeds/second" -ForegroundColor Yellow