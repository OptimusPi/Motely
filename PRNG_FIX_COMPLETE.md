# Mukundan314's PRNG Fix + Progress Reporting Cleanup

## What Was Fixed

### 1. PRNG Rounding (Mukundan314's Fix)
- **Problem**: LuaJIT uses ties-to-even rounding, but Motely used `MidpointRounding.AwayFromZero`, causing ~0.04% of seeds to mismatch
- **Test Seed**: `6L651I2V` - 6th card in ante 1 shop was incorrectly reported as Gros Michel instead of The Moon
- **Solution**: Implemented FMA (Fused Multiply-Add) magic number rounding
  - `Math.FusedMultiplyAdd(x, 1e13, 2^52)` maintains infinite precision
  - Adding 2^52 aligns the binary point for exact ties-to-even behavior
  - Matches LuaJIT's `string.format("%.13f")` exactly

**Files Changed**:
- `Motely/MotelySingleSearchContext.cs` - Scalar PRNG
- `Motely/MotelyVectorSearchContext.cs` - Vectorized SIMD PRNG
- `Motely/Motely.csproj` - **FMA disabled** in compiler (critical!)
- `Motely.Tests/Motely.Tests.csproj` - FMA disabled

### 2. Progress Reporting Cleanup
- **Problem**: Messy callback with 4 parameters, console output in wrong places, complicated threshold logic
- **Solution**: Clean `MotelyProgress` object passed to single callback

**Old Signature**:
```csharp
Action<long completedBatches, long totalBatches, long seedsSearched, double seedsPerMs>
```

**New Signature**:
```csharp
Action<MotelyProgress> // Clean object with all progress data
```

**Files Changed**:
- `Motely/MotelySearch.cs` - Core search engine
- `Motely/MotelyProgress.cs` - Progress data class
- `Motely.CLI/Program.cs` - CLI progress callback
- `Motely.API/SearchManager.cs` - API progress tracking
- `Motely/Executors/JsonSearchExecutor.cs` - Executor callbacks
- `Motely/Executors/NativeFilterExecutor.cs` - Executor callbacks

### 3. CLI `--save` Enhancement
- **New**: `--save [duckdb|csv]` with optional value
  - `--save` or `--save duckdb` → saves to both DuckDB + CSV
  - `--save csv` → CSV only (skips database)
- Uses `CommandOptionType.SingleOrNoValue` from McMaster CLI library

## Build Status
✅ **Successful Release Build** - 0 errors, 24 warnings (AOT/trim warnings only)

## Testing
Run a quick search to verify:
```powershell
dotnet run -c Release --project Motely.CLI\Motely.CLI.csproj -- --jaml Trickeoglyph --threads 16 --save
```

## Credit
- **Mukundan314**: Discovered PRNG rounding bug, provided FMA magic number solution
- **mathisfun** (TheSoul): Original complex rounding implementation
- **Tacodiva**: Scalar PRNG reference implementation
