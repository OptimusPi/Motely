-- Fix corrupted DuckDB seed databases
-- Usage: duckdb SeedSources/AtLeast14Twos.db < fix_seed_db.sql
-- Or run via C#: DuckDBConnection.ExecuteNonQuery() with this script

-- Step 1: Extract seed from "SEED,score" format (strip comma and everything after)
UPDATE seeds 
SET seed = TRIM(SPLIT_PART(seed, ',', 1))
WHERE seed LIKE '%,%';

-- Step 2: Also handle any whitespace-separated values
UPDATE seeds 
SET seed = TRIM(SPLIT_PART(seed, ' ', 1))
WHERE seed LIKE '% %' AND seed NOT LIKE '%,%';

-- Step 3: Truncate to 8 characters max (Balatro seed limit)
UPDATE seeds 
SET seed = SUBSTRING(seed, 1, 8)
WHERE LENGTH(seed) > 8;

-- Step 4: Remove seeds containing '0' (invalid in Balatro)
DELETE FROM seeds WHERE seed LIKE '%0%';

-- Step 5: Remove seeds with invalid characters (must be A-Z or 1-9)
DELETE FROM seeds WHERE seed NOT GLOB '[1-9A-Z]*';

-- Step 6: Rebuild id column if missing (for provider mode performance)
-- Check if id column exists first, then add if needed
ALTER TABLE seeds ADD COLUMN IF NOT EXISTS id BIGINT;
UPDATE seeds SET id = ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 WHERE id IS NULL;
CREATE INDEX IF NOT EXISTS idx_seeds_id ON seeds(id);

-- Step 7: Verify fix
SELECT COUNT(*) as total_seeds, 
       COUNT(CASE WHEN seed LIKE '%,%' THEN 1 END) as has_comma,
       COUNT(CASE WHEN LENGTH(seed) > 8 THEN 1 END) as too_long,
       COUNT(CASE WHEN seed LIKE '%0%' THEN 1 END) as has_zero,
       COUNT(CASE WHEN seed NOT GLOB '[1-9A-Z]*' THEN 1 END) as invalid_chars
FROM seeds;
