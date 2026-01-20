# DuckDB Setup for dungmot

## Quick Start

To enable DuckDB seed source output, you need to link the DuckDB C library.

## Option 1: Download DuckDB C API (Recommended)

1. **Download DuckDB C API:**
   - Go to https://duckdb.org/docs/api/c
   - Download `duckdb.h` and `duckdb.dll` (Windows) or `libduckdb.so` (Linux)

2. **Place files:**
   ```
   dungmot/
   ├── duckdb.h          # Header file
   ├── duckdb.dll        # Windows library (or .so for Linux)
   └── ...
   ```

3. **Build with DuckDB:**
   ```powershell
   # Windows
   nvcc -DDUCKDB_AVAILABLE -I. -L. -lduckdb negative_tag_skipper.cu -o negative_tag_skipper.exe
   
   # Linux
   nvcc -DDUCKDB_AVAILABLE -I. -L. -lduckdb negative_tag_skipper.cu -o negative_tag_skipper
   ```

## Option 2: Build DuckDB from Source

```bash
git clone https://github.com/duckdb/duckdb.git
cd duckdb
mkdir build && cd build
cmake .. -DBUILD_C_EXTENSION=ON
make
# Copy duckdb.h and libduckdb.so to dungmot directory
```

## Option 3: Use Package Manager

### Windows (vcpkg)
```powershell
vcpkg install duckdb
nvcc -DDUCKDB_AVAILABLE -I%VCPKG_ROOT%\installed\x64-windows\include -L%VCPKG_ROOT%\installed\x64-windows\lib -lduckdb ...
```

### Linux (apt)
```bash
sudo apt-get install libduckdb-dev
nvcc -DDUCKDB_AVAILABLE -lduckdb ...
```

## Verify Installation

```powershell
# Test that DuckDB is linked
.\negative_tag_skipper.exe --help
# Should show --output-db option

# Test DuckDB output
.\negative_tag_skipper.exe --ante 8 --joker Brainstorm --output-db test.db --start-batch 0 --end-batch 10
# Should create test.db without warnings
```

## Fallback Behavior

If DuckDB is not linked (no `DUCKDB_AVAILABLE` define):
- `--output-db` flag will show a warning
- Seeds will still be written to stdout (CSV format)
- You can manually convert CSV to DuckDB using Motely's conversion tools

## Integration with Motely

Once you have a DuckDB seed source file:

```powershell
# Step 1: GPU search creates seed source
.\negative_tag_skipper.exe --ante 8 --joker Brainstorm --output-db SeedSources/gpu_candidates.db

# Step 2: Motely uses it as seed source
dotnet run -c Release --project Motely.CLI -- --jaml TheDailyWee --seedsource SeedSources/gpu_candidates.db
```

The GPU searcher becomes a **seed source generator** for Motely! 🚀
