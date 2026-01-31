---
name: Backend Caching and Organization
overview: Add IMemoryCache for seed sources with Cloudflare CDN-friendly HTTP headers, implement category-based organization using _CATEGORY__filename.ext convention, add CSV support with seed validation, sequential search continue options, and multi-source hydrate search.
todos:
  - id: add_memory_cache
    content: Register IMemoryCache and add cache with Cloudflare-friendly HTTP headers
    status: completed
  - id: category_parsing
    content: Parse _CATEGORY__filename.ext format and extract category/displayName
    status: completed
  - id: organize_by_category
    content: Group seed sources by category, always include categories even if empty
    status: completed
  - id: csv_support
    content: Add CSV file support with seed validation (0-8 chars, dictionary only, uppercase conversion)
    status: completed
  - id: seed_validation
    content: Validate seeds - convert 0→O, lowercase→uppercase, reject invalid chars
    status: completed
  - id: icons_metadata
    content: Add icon metadata (🦆 for .db, 📊 for .csv, 📄 for .txt) to seed source objects
    status: completed
  - id: sequential_continue
    content: Add sequential search continue options with progress percentage
    status: completed
  - id: multi_source_hydrate
    content: Add API route for hydrate search with collection of sources
    status: completed
  - id: order_filters
    content: Order filters - unsaved first, then alphabetical by name
    status: completed
isProject: false
---

**File:** `Motely.API/Program.cs`

- Register `IMemoryCache` in service collection (if not already registered)
- Create cache key constant: `"seed_sources_cache"`
- Modify `/seed-sources` endpoint:
- Check cache first
- If cache miss, scan directories and build list
- Store in cache with **absolute expiration** (30 minutes or 1 hour) - sources rarely change
- **Important:** Add HTTP headers for Cloudflare CDN:
  - `Cache-Control: public, max-age=1800` (30 minutes) or `max-age=3600` (1 hour)
  - `ETag` header for cache validation
- Return cached or fresh data
- **No automatic invalidation** - cache expires naturally after long period, respects Cloudflare's caching

### 2. Category-Based Organization System

**File:** `Motely.API/Program.cs`

- **Naming Convention:** `_CATEGORY__filename.ext`
- Files starting with `_` have a category
- Double underscore `__` separates category from filename
- Example: `_Erratic_Deck__2s.db` → category: "Erratic Deck", filename: "2s.db"
- Underscores in category name render as spaces in display (e.g., `_Erratic_Deck__` → "Erratic Deck")
- **Helper Methods:**
- `ParseCategoryFromFileName(string fileName)` → returns `(category: string?, displayName: string)`
  - If starts with `_`, extract category (everything before `__`)
  - Replace underscores in category with spaces for display
  - Extract filename (everything after `__`)
  - If no category, `category = null`, `displayName = fileName`
- **Organization:**
- Always include categories (even if empty)
- Group by category, then sort alphabetically within category
- Uncategorized files go in "Uncategorized" category
- Built-in sources (always first): "All Seeds (default)", "Random 1M"
- Actions (always last): "New word list…"

### 3. CSV Support with Seed Validation

**File:** `Motely.API/Program.cs`**File:** `Motely/Executors/JsonSearchExecutor.cs` (if needed)

- Add `.csv` file support in `/seed-sources` endpoint
- Parse CSV files (simple: one seed per line, or comma-separated)
- **Seed Validation Rules:**
- Seeds must be 0-8 characters
- Valid dictionary: `[ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789]` (35 chars)
- **UX Conversions:**
  - Convert `0` → `O` (zero to letter O)
  - Convert lowercase → uppercase
  - Reject any character not in dictionary
- Skip invalid seeds (log warning, continue processing)
- **Helper Method:**
- `ValidateAndNormalizeSeed(string seed)` → returns normalized seed or null if invalid
  - Trim whitespace
  - Replace `0` with `O`
  - Convert to uppercase
  - Check length (0-8 chars)
  - Check all chars in dictionary
  - Return normalized seed or null

### 4. Icons and Metadata

**File:** `Motely.API/Program.cs`

- Add `icon` field to seed source objects:
- `.db` files → `"🦆"` (duck icon)
- `.csv` files → `"📊"` (spreadsheet icon)
- `.txt` files → `"📄"` (document icon)
- Built-in → `"⭐"` (star icon)
- Actions → `"➕"` (plus icon)
- Add `category` field (string or null)
- Add `displayName` field (clean version without category prefix)
- Add `fileName` field (original filename)

### 5. Sequential Search Continue Options

**File:** `Motely.API/Program.cs`**File:** `Motely.API/wwwroot/JAML/jaml.js`

- Modify `/seed-sources` endpoint to include sequential search options:
- `"all"` → "All Seeds (Start from beginning)"
- `"all:continue"` → "All Seeds (Continue Saved Search - {progress}%)"
  - Calculate progress from `SearchManager` if there's a paused/resumable search
  - Format: `"All Seeds (Continue Saved Search - 2.3456%)"` (4 decimal places)
- **Backend Logic:**
- Check if there's a resumable sequential search (check DuckDB for saved batch position)
- If resumable, include continue option with calculated progress
- Progress calculation: `(currentBatch / totalBatches) * 100` with 4 decimal precision
- **Frontend Logic (jaml.js):**
- When progress updates come through SignalR, check if search is resumable
- Auto-select continue option if available
- Update dropdown options dynamically

### 6. Multi-Source Hydrate Search

**File:** `Motely.API/Program.cs`**File:** `Motely.API/SearchManager.cs`

- Add new endpoint: `POST /search/hydrate` or modify existing `/search` to accept array of sources
- Request body:
  ```csharp
                {
                  filterJaml: string,
                  seedSources: string[], // Array of source keys: ["db:file1.db", "txt:file2.txt", "csv:file3.csv"]
                  seedCount: long?,
                  startBatch: long?,
                  cutoff: int?
                }
  ```
- **Backend Implementation:**
- Parse each source key (format: `{type}:{filename}`)
- Load seeds from all sources (combine into single list)
- Validate and normalize all seeds
- Remove duplicates
- Sort seeds (if needed)
- Pass combined seed list to search executor

### 7. Filter Ordering

**File:** `Motely.API/Program.cs`

- Order filters:

1. **Unsaved/running searches first** (already implemented with `Insert(0)`)
2. **Then by name** (alphabetical, case-insensitive)

- Use `OrderBy(f => f.name, StringComparer.OrdinalIgnoreCase)` after inserting unsaved ones

### 8. Seed Source Response Structure

**File:** `Motely.API/Program.cs`

- Response structure:
  ```csharp
                {
                  sources: [
                    {
                      key: "all",
                      label: "All Seeds (default)",
                      kind: "builtin",
                      icon: "⭐",
                      category: null,
                      displayName: "All Seeds (default)"
                    },
                    {
                      key: "db:_Erratic_Deck__2s.db",
                      label: "2s", // or full name for display
                      kind: "db",
                      icon: "🦆",
                      category: "Erratic Deck",
                      displayName: "2s",
                      fileName: "_Erratic_Deck__2s.db"
                    },
                    // ... grouped by category
                  ],
                  categories: [
                    {
                      name: "Built-in",
                      sources: [...]
                    },
                    {
                      name: "Erratic Deck",
                      sources: [...]
                    },
                    {
                      name: "Uncategorized",
                      sources: [...]
                    }
                  ]
                }
  ```

## Files to Modify

1. `Motely.API/Program.cs` - Main implementation
2. `Motely.API/wwwroot/JAML/jaml.js` - Frontend updates for continue options
3. `Motely/Executors/JsonSearchExecutor.cs` - CSV parsing (if needed)
4. Potentially create `Motely.API/SeedSourceManager.cs` service class (optional)

## Testing Checklist

- Seed sources are cached and don't hit disk on every request
- HTTP headers respect Cloudflare CDN caching
- Category parsing works correctly (`_CATEGORY__filename.ext`)
- Underscores in category names render as spaces
- Seed sources are grouped by category

