#!/usr/bin/env python3
"""
Combine erratic_two_rank2_part1.csv and part2.csv into erratic_two_rank2_all.db
Run this from the Motely directory
"""

import os
import sys
import subprocess
import tempfile
import tempfile as tf

csv1 = "SeedSources/erratic_two_rank2_part1.csv"
csv2 = "SeedSources/erratic_two_rank2_part2.csv"
db_path = "SeedSources/erratic_two_rank2_all.db"

# Delete existing DB if it exists
if os.path.exists(db_path):
    print(f"Deleting existing database: {db_path}")
    os.remove(db_path)

print("Combining CSV files into DuckDB...")
print(f"  Part 1: {csv1}")
print(f"  Part 2: {csv2}")
print(f"  Output: {db_path}")
print()

# Normalize paths for DuckDB (use forward slashes)
csv1_escaped = csv1.replace('\\', '/').replace("'", "''")
csv2_escaped = csv2.replace('\\', '/').replace("'", "''")
db_path_escaped = db_path.replace('\\', '/').replace("'", "''")

# Use system temp directory (has more space)
temp_dir = tf.gettempdir().replace('\\', '/').replace("'", "''")

# Two-phase import: first import unsorted, then sort in second pass
sql_phase1 = f"""
SET memory_limit='8GB';
SET temp_directory='{temp_dir}';
SET max_temp_directory_size='50GB';
SET preserve_insertion_order=true;
SET threads=4;

-- Create seeds_raw table (unsorted for now)
CREATE TABLE seeds_raw (raw_line VARCHAR);

-- Import first CSV - read with header=false to force column0 (ignores any header row)
INSERT INTO seeds_raw 
SELECT column0 as raw_line
FROM read_csv('{csv1_escaped}', header=false, auto_detect=true)
WHERE column0 IS NOT NULL AND trim(column0) != '';

-- Import second CSV - same approach
INSERT INTO seeds_raw 
SELECT column0 as raw_line
FROM read_csv('{csv2_escaped}', header=false, auto_detect=true)
WHERE column0 IS NOT NULL AND trim(column0) != '';
"""

sql_phase2 = f"""
SET memory_limit='8GB';
SET temp_directory='{temp_dir}';
SET max_temp_directory_size='50GB';
SET preserve_insertion_order=false;
SET threads=4;

-- Sanitize: Extract seed from first field (split on comma/whitespace), validate
CREATE TABLE seeds_temp AS 
SELECT 
    -- Extract first field: split on comma, then whitespace, take first 8 chars
    UPPER(TRIM(SUBSTRING(
        CASE 
            WHEN INSTR(raw_line, ',') > 0 THEN SUBSTRING(raw_line, 1, INSTR(raw_line, ',') - 1)
            WHEN INSTR(raw_line, ' ') > 0 THEN SUBSTRING(raw_line, 1, INSTR(raw_line, ' ') - 1)
            ELSE raw_line
        END,
        1, 8
    ))) as seed
FROM seeds_raw 
WHERE raw_line IS NOT NULL AND trim(raw_line) != '';

-- Validate: Remove invalid seeds (contain '0', invalid chars, empty, or >8 chars)
DELETE FROM seeds_temp 
WHERE seed = '' 
   OR seed LIKE '%0%' 
   OR seed NOT GLOB '[1-9A-Z]*'
   OR LENGTH(seed) > 8;

-- Create final table with VARCHAR(8) constraint, sorted by seed length
CREATE TABLE seeds (
    id BIGINT,
    seed VARCHAR(8)
);

INSERT INTO seeds (id, seed)
SELECT 
    ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 AS id, 
    seed 
FROM seeds_temp;

CREATE INDEX idx_seeds_id ON seeds(id);

DROP TABLE seeds_raw;
DROP TABLE seeds_temp;
"""

sql = sql_phase1

print("Running DuckDB import (this may take a while for large files)...")
print()

# Write SQL to temp file
with tempfile.NamedTemporaryFile(mode='w', suffix='.sql', delete=False, encoding='utf-8') as f:
    f.write(sql)
    temp_sql = f.name

try:
    # Phase 1: Import unsorted
    print("Phase 1: Importing CSV files (unsorted)...")
    result = subprocess.run(
        ['duckdb', db_path],
        input=sql_phase1,
        text=True,
        capture_output=True
    )
    
    if result.returncode != 0:
        print("ERROR: Phase 1 (import) failed:")
        print(result.stderr)
        if result.stdout:
            print("STDOUT:", result.stdout)
        sys.exit(1)
    else:
        if result.stdout:
            print(result.stdout)
    
    # Phase 2: Sort
    print("Phase 2: Sorting seeds by length...")
    result = subprocess.run(
        ['duckdb', db_path],
        input=sql_phase2,
        text=True,
        capture_output=True
    )
    
    if result.returncode != 0:
        print("ERROR: Phase 2 (sorting) failed:")
        print(result.stderr)
        if result.stdout:
            print("STDOUT:", result.stdout)
        sys.exit(1)
    else:
        if result.stdout:
            print(result.stdout)
finally:
    try:
        os.unlink(temp_sql)
    except:
        pass

if os.path.exists(db_path):
    db_size = os.path.getsize(db_path) / (1024**3)
    print()
    print(f"SUCCESS: Created {db_path}")
    print(f"   Database size: {db_size:.2f} GB")
else:
    print()
    print("ERROR: Failed to create database")
