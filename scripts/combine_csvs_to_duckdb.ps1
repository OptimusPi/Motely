# Combine erratic_two_rank2_part1.csv and part2.csv into erratic_two_rank2_all.db
# Run this from the Motely directory

$csv1 = "SeedSources\erratic_two_rank2_part1.csv"
$csv2 = "SeedSources\erratic_two_rank2_part2.csv"
$dbPath = "SeedSources\erratic_two_rank2_all.db"

# Delete existing DB if it exists
if (Test-Path $dbPath) {
    Write-Host "Deleting existing database: $dbPath"
    Remove-Item $dbPath -Force
}

Write-Host "Combining CSV files into DuckDB..."
Write-Host "  Part 1: $csv1"
Write-Host "  Part 2: $csv2"
Write-Host "  Output: $dbPath"
Write-Host ""

# Escape paths for DuckDB (use forward slashes)
$csv1Escaped = $csv1.Replace('\', '/')
$csv2Escaped = $csv2.Replace('\', '/')
$dbPathEscaped = $dbPath.Replace('\', '/')

$duckdbCommand = @"
SET memory_limit='4GB';
SET temp_directory='.duckdb_temp';
SET max_temp_directory_size='20GB';
SET preserve_insertion_order=false;
SET threads=8;

-- Read both CSVs into temp tables to detect schema
CREATE TEMP TABLE csv1_temp AS SELECT * FROM read_csv_auto('$csv1Escaped');
CREATE TEMP TABLE csv2_temp AS SELECT * FROM read_csv_auto('$csv2Escaped');

-- Get first column name from each
CREATE TEMP TABLE col_info AS 
SELECT 'csv1' as source, column_name 
FROM information_schema.columns 
WHERE table_name='csv1_temp' 
ORDER BY ordinal_position 
LIMIT 1
UNION ALL
SELECT 'csv2' as source, column_name 
FROM information_schema.columns 
WHERE table_name='csv2_temp' 
ORDER BY ordinal_position 
LIMIT 1;

-- Create seeds_raw table
CREATE TABLE seeds_raw (seed VARCHAR);

-- Import from csv1 using dynamic column name
INSERT INTO seeds_raw
SELECT * FROM (
    SELECT CASE 
        WHEN (SELECT column_name FROM col_info WHERE source='csv1') = 'seed' THEN (SELECT seed FROM csv1_temp)
        WHEN (SELECT column_name FROM col_info WHERE source='csv1') = 'Score' THEN (SELECT Score FROM csv1_temp)
        ELSE (SELECT column0 FROM csv1_temp)
    END as seed
    FROM csv1_temp
) WHERE seed IS NOT NULL AND trim(seed) != '';

-- Import from csv2 using dynamic column name
INSERT INTO seeds_raw
SELECT * FROM (
    SELECT CASE 
        WHEN (SELECT column_name FROM col_info WHERE source='csv2') = 'seed' THEN (SELECT seed FROM csv2_temp)
        WHEN (SELECT column_name FROM col_info WHERE source='csv2') = 'Score' THEN (SELECT Score FROM csv2_temp)
        ELSE (SELECT column0 FROM csv2_temp)
    END as seed
    FROM csv2_temp
) WHERE seed IS NOT NULL AND trim(seed) != '';

DROP TABLE csv1_temp;
DROP TABLE csv2_temp;
DROP TABLE col_info;

-- Create final sorted table (this is the slow part, but now has 20GB temp space!)
CREATE TABLE seeds AS 
SELECT ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 AS id, seed 
FROM seeds_raw 
WHERE seed IS NOT NULL AND trim(seed) != '';

DROP TABLE seeds_raw;
CREATE INDEX idx_seeds_id ON seeds(id);
"@

# Write command to temp file
$tempScript = [System.IO.Path]::GetTempFileName() + ".sql"
$duckdbCommand | Out-File -FilePath $tempScript -Encoding UTF8

Write-Host "Running DuckDB import (this may take a while for large files)..."
Write-Host ""

# Run DuckDB using cmd.exe for proper redirection (PowerShell doesn't support < redirection)
$tempScriptEscaped = $tempScript.Replace('\', '/')
cmd /c "duckdb `"$dbPath`" < `"$tempScript`""

# Clean up
Remove-Item $tempScript -ErrorAction SilentlyContinue

if (Test-Path $dbPath) {
    Write-Host ""
    Write-Host "✅ Successfully created: $dbPath"
    $dbSize = (Get-Item $dbPath).Length / 1GB
    Write-Host "   Database size: $([math]::Round($dbSize, 2)) GB"
} else {
    Write-Host ""
    Write-Host "❌ Failed to create database"
}
