param(
    # Defaults to ../SeedSources relative to this scripts/ folder
    [string]$SeedSourcesDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "SeedSources"),

    # Optionally pass one or more .db paths/names to fix (e.g. NSFW-MAX.db)
    [string[]]$DbPaths = @(),

    [switch]$Recurse
)

$ErrorActionPreference = "Stop"

function Invoke-DuckDbScalar([string]$DbPath, [string]$Sql) {
    $out = & duckdb -csv -noheader $DbPath -c $Sql 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ($out | Select-Object -First 1).Trim()
}

function Fix-SeedSourceDb([string]$DbPath) {
    Write-Host "Fixing id column: $DbPath"

    $hasSeedsTable = Invoke-DuckDbScalar $DbPath "SELECT COUNT(*) FROM information_schema.tables WHERE table_name='seeds';"
    if ($hasSeedsTable -ne "1") {
        Write-Warning "  Skipping (no 'seeds' table found)"
        return
    }

    $hasId = Invoke-DuckDbScalar $DbPath "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='seeds' AND column_name='id';"
    if ($hasId -eq "1") {
        $nullIds = Invoke-DuckDbScalar $DbPath "SELECT COUNT(*) FROM seeds WHERE id IS NULL;"
        if ($nullIds -eq "0") {
            Write-Host "  OK: already has id (no NULLs)"
            return
        }
    }

    $sql = @"
BEGIN TRANSACTION;
DROP TABLE IF EXISTS seeds_old_id;
ALTER TABLE seeds RENAME TO seeds_old_id;

CREATE TABLE seeds AS
SELECT
    CAST(ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 AS BIGINT) AS id,
    seed
FROM seeds_old_id;

CREATE INDEX IF NOT EXISTS idx_seeds_id ON seeds(id);
DROP TABLE seeds_old_id;
COMMIT;
"@

    $sql | & duckdb $DbPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "DuckDB failed while fixing: $DbPath"
    }

    $count = Invoke-DuckDbScalar $DbPath "SELECT COUNT(*) FROM seeds;"
    Write-Host "  OK (seeds: $count)"
}

# Ensure duckdb is available
if (-not (Get-Command duckdb -ErrorAction SilentlyContinue)) {
    throw "duckdb CLI not found on PATH. Install DuckDB or add it to PATH."
}

$targets = @()

if ($DbPaths.Count -gt 0) {
    foreach ($p in $DbPaths) {
        $candidate = $p
        if (-not (Test-Path $candidate)) {
            $candidate = Join-Path $SeedSourcesDir $p
        }
        if (-not (Test-Path $candidate)) {
            throw "DB not found: $p (also checked: $candidate)"
        }
        $targets += (Get-Item $candidate)
    }
} else {
    if (-not (Test-Path $SeedSourcesDir)) {
        throw "SeedSources directory not found: $SeedSourcesDir"
    }

    $targets = Get-ChildItem -Path $SeedSourcesDir -Filter "*.db" -File -Recurse:$Recurse
}

if ($targets.Count -eq 0) {
    Write-Host "No .db files found under: $SeedSourcesDir"
    exit 0
}

foreach ($db in $targets) {
    Fix-SeedSourceDb $db.FullName
}

