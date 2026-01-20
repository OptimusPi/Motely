# DuckDB Seed Source Integration

## Overview

dungmot GPU searchers can now output **seed source DuckDB files** that Motely can use directly as `--seedsource` input!

## Flow

```
GPU Searcher (dungmot)
    ↓ finds candidate seeds
    ↓ writes to DuckDB seeds table
    ↓
DuckDB File (seed_source.db)
    ↓
Motely CPU Searcher
    ↓ reads --seedsource seed_source.db
    ↓ does precise verification/scoring
    ↓ outputs final results
```

## Usage

### 1. Run GPU Searcher with `--output-db`

```powershell
.\negative_tag_skipper.exe --ante 8 --joker Brainstorm --min-hits 4 --output-db SeedSources/gpu_candidates.db
```

This creates `SeedSources/gpu_candidates.db` with the `seeds` table schema.

### 2. Use as Seed Source in Motely

```powershell
dotnet run -c Release --project Motely.CLI -- --jaml TheDailyWee --seedsource SeedSources/gpu_candidates.db --threads 16
```

Motely will read the GPU-found seeds and do precise CPU verification/scoring!

## DuckDB Schema

Matches Motely's `DuckDBSchema.SeedSourcesTableSchema()`:

```sql
CREATE TABLE seeds (
    id BIGINT,
    seed VARCHAR(8)
);
CREATE INDEX idx_seeds_id ON seeds(id);
```

- `id`: Auto-incrementing BIGINT (0, 1, 2, ...)
- `seed`: Seed string (1-8 chars, uppercase, no '0')
- Index on `id` for fast range queries

## Benefits

1. **GPU Fast Search**: GPU does broad, fast filtering
2. **CPU Precise Verification**: Motely does exact PRNG verification
3. **Reusable Seed Sources**: DuckDB files can be reused for multiple searches
4. **Hybrid Approach**: Best of both worlds!

## Implementation Notes

- Uses DuckDB C API for efficient bulk inserts
- Appender pattern for performance (batched writes)
- Auto-flushes every 1000 rows
- Validates seeds (1-8 chars, uppercase, no '0')
- Creates index for fast queries

## Building with DuckDB

### Windows

1. Download DuckDB C API:
   ```powershell
   # Download duckdb.h and duckdb.dll from https://duckdb.org/docs/api/c
   ```

2. Link in build:
   ```powershell
   nvcc ... -I/path/to/duckdb -L/path/to/duckdb -lduckdb ...
   ```

### Linux

```bash
# Install DuckDB development package
sudo apt-get install libduckdb-dev  # or equivalent

# Link in build
nvcc ... -lduckdb ...
```

## Example Workflow

```powershell
# Step 1: GPU fast search (finds ~1000 candidates from 1 billion seeds)
.\negative_tag_skipper.exe --ante 8 --joker Brainstorm --min-hits 4 --output-db gpu_candidates.db

# Step 2: CPU precise verification (verifies those 1000 candidates)
dotnet run -c Release --project Motely.CLI -- --jaml TheDailyWee --seedsource gpu_candidates.db --threads 16

# Result: Fast GPU search + Precise CPU verification = Best of both worlds!
```
