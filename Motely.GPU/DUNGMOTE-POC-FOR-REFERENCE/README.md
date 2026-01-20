# Balatro CUDA Seed Searcher Prototype

A GPU-accelerated seed searcher for Balatro, implemented in CUDA from scratch based solely on:
- The Balatro Lua source code (`external/Balatro/*.lua`)
- The JAML filter schema (`jaml.schema.json`)

## What This Implements

### Core RNG System (`balatro_rng.cuh`)

1. **LuaJIT Tausworthe PRNG** - The exact 4x64-bit combined Tausworthe generator that LuaJIT (and therefore LOVE2D/Balatro) uses
2. **pseudohash()** - Balatro's string-to-double hash function
3. **pseudoseed()** - Balatro's per-key RNG state system
4. **pseudorandom()** - The full random generation pipeline

### Game Logic (`balatro_game.cuh`)

- Joker rarity determination
- Joker pool selection
- Edition polling (Foil/Holographic/Polychrome/Negative)
- Voucher selection
- Boss blind selection
- Tag selection
- Soul card spawn checking

### Search Kernel (`seed_search.cu`)

- Parallel seed evaluation
- Result collection with atomic operations
- Benchmark mode for performance testing
- Verification mode for RNG accuracy testing

## Building

### Requirements

- **NVIDIA GPU**: CUDA Toolkit 11.0+ (tested with 12.x)
- **AMD GPU**: ROCm/HIP (see `HIPIFY_GUIDE.md`)
- Visual Studio 2022 (for Windows) or GCC (for Linux)
- C++ compiler (cl.exe on Windows, g++ on Linux)

**Note**: This codebase supports both NVIDIA (CUDA) and AMD (HIP) GPUs via unified abstraction layer (`gpu_common.h`)

### Windows Setup

**Add these to your PATH:**

1. CUDA Toolkit:
   ```
   C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.8\bin
   ```

2. Visual Studio C++ compiler:
   ```
   C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207\bin\Hostx64\x64
   ```
   (Version number may vary - check your VS installation)

3. Windows SDK tools:
   ```
   C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64
   ```
   (Version number may vary - check your Windows SDK installation)

**To add to PATH:**
1. Press Windows key, type "Environment Variables"
2. Click "Edit the system environment variables"
3. Click "Environment Variables" → Select "Path" → "Edit"
4. Click "New" and add each path above
5. Restart PowerShell/terminal

### Compile

**Windows (PowerShell):**
```powershell
# Build soul joker searcher
nvcc -O3 -arch=sm_89 --use_fast_math -o soul_joker_filter_search.exe soul_joker_filter_search.cu

# Build erratic deck searcher
nvcc -O3 -arch=sm_89 --use_fast_math -o erratic_search.exe erratic_search.cu
```

**Or use the build script:**
```powershell
# Build all executables
.\build.ps1 all

# Fast prefilters (recommended for quick searches)
.\build.ps1 joker_prefilter      # negative_joker_prefilter.exe
.\build.ps1 legendary_prefilter  # negative_legendary_prefilter.exe
.\build.ps1 rare_prefilter       # negative_rare_prefilter.exe
.\build.ps1 uncommon_prefilter    # negative_uncommon_prefilter.exe

# Other searches
.\build.ps1 soul_search          # soul_joker_filter_search.exe
.\build.ps1 soul_edition          # soul_edition_search.exe
.\build.ps1 raw_soul             # raw_soul_edition_check.exe
.\build.ps1 erratic               # erratic_search.exe
.\build.ps1 negative_tag_skipper  # negative_tag_skipper.exe
.\build.ps1 economy_rush          # economy_rush_search.exe
.\build.ps1 ultimate_filter      # ultimate_filter.exe
.\build.ps1 consecutive_negative # consecutive_negative_checker.exe
.\build.ps1 showman_consecutive  # showman_consecutive_filter.exe

# Utilities
.\build.ps1 verify  # verify_rng.exe - RNG verification test
.\build.ps1 clean   # Clean build artifacts
```

**For AMD GPUs (HIP/ROCm):**
```powershell
$env:HIP=1; .\build.ps1 all
# OR
.\build_hip.ps1 all
```

**Linux/Mac:**
```bash
nvcc -O3 -arch=sm_75 -o soul_joker_filter_search soul_joker_filter_search.cu
```

**Adjust `-arch=sm_XX` for your GPU:**
- RTX 20xx (Turing): `sm_75`
- RTX 30xx (Ampere): `sm_86`
- RTX 40xx (Ada Lovelace): `sm_89`
- RTX 50xx (Blackwell): `sm_100`

### GPU Architecture Flags

| GPU Series | Architecture Flag |
|------------|-------------------|
| RTX 20xx (Turing) | `-arch=sm_75` |
| RTX 30xx (Ampere) | `-arch=sm_86` |
| RTX 40xx (Ada Lovelace) | `-arch=sm_89` |
| RTX 50xx (Blackwell) | `-arch=sm_100` |

## Usage

### Soul Joker Filter Search

```powershell
# Search 1 million seeds for negative legendary soul jokers
.\soul_joker_filter_search.exe 1000000

# Search 10 million seeds
.\soul_joker_filter_search.exe 10000000

# Search with starting seed
.\soul_joker_filter_search.exe 1000000 AAAAAAAA
```

### Erratic Deck Search

```powershell
# Verify a seed
.\erratic_search.exe --verify TESTTEST

# Benchmark
.\erratic_search.exe --benchmark

# Find seeds with 10+ twos (for Wee Joker)
.\erratic_search.exe --rank-name 2 --min-count 10 --count 100000000

# Find seeds with 18+ Hearts (for Bloodstone)
.\erratic_search.exe --suit 1 --min-count 18 --count 100000000
```

## How It Works

### The RNG Pipeline

```
Seed String (e.g., "TESTTEST")
    │
    ▼
pseudohash() → hashed_seed (double in [0,1))
    │
    ▼
pseudoseed(key) → combines key + seed via pseudohash
    │              applies transformation: (2.134... + x * 1.724...) % 1
    │              averages with hashed_seed
    │
    ▼
math.randomseed() → seeds LuaJIT's Tausworthe PRNG
    │                (4 x 64-bit state, warmed up 10 iterations)
    │
    ▼
math.random() → generates [0,1) double
```

### The Tausworthe Generator

LuaJIT uses a combined Tausworthe generator with 4 components, each with different shift parameters:

```
state[0]: shifts (31, 45, 1, 18)
state[1]: shifts (19, 30, 6, 28)
state[2]: shifts (24, 48, 9, 7)
state[3]: shifts (21, 39, 17, 8)

output = state[0] XOR state[1] XOR state[2] XOR state[3]
```

This gives a period of 2^223 and excellent statistical properties.

## Customizing the Filter

Edit the `evaluate_seed()` function in `seed_search.cu` to implement your own search criteria. The current example searches for:
- Soul card spawns in ante 1 (legendary joker!)
- Negative edition jokers

Example modifications:
```cuda
// Check for specific voucher in ante 1
if (get_voucher(state) == VOUCHER_TELESCOPE) {
    score += 100;
}

// Check for rare joker rarity roll
if (get_joker_rarity(state, "", 0) == RARITY_RARE) {
    score += 25;
}
```

## Performance

Expected throughput depends on:
- Filter complexity
- GPU model
- How many antes you simulate

Rough estimates:
| GPU | Simple Filter | Complex Filter |
|-----|---------------|----------------|
| RTX 3080 | ~50-100M seeds/sec | ~10-30M seeds/sec |
| RTX 4090 | ~100-200M seeds/sec | ~30-60M seeds/sec |

The full seed space is 35^8 ≈ 2.25 trillion seeds, which at 100M seeds/sec would take ~6 hours.

## Accuracy Notes

This implementation is **verified accurate** with Balatro's actual RNG. Key considerations:

1. **Precision rounding**: Matches Balatro's `string.format("%.13f", ...)` precision requirement (critical for accuracy!)
2. **Tausworthe state**: Uses the exact same bit manipulation as LuaJIT's `lj_prng.c`
3. **Seeding**: Replicates LuaJIT's seeding with pi/e constants and minimum bit enforcement
4. **Warmup iterations**: Uses 11 iterations (5x2 + 1) per state, matching LuaJIT behavior

To verify accuracy, run:
```powershell
.\build.ps1 verify
.\verify_rng.exe
```

See `RNG_VERIFICATION_RESULTS.md` for detailed verification results.

## Fast Pre-filters

The fastest way to search for specific joker combinations:

- **`negative_legendary_prefilter.cu`** - Negative edition legendary jokers from Soul cards
- **`negative_rare_prefilter.cu`** - Negative edition rare jokers from shop slots
- **`negative_uncommon_prefilter.cu`** - Negative edition uncommon jokers from shop slots

Usage:
```powershell
# Build
.\build.ps1 rare_prefilter

# Run
.\run_rare_prefilter.ps1 1000000 "1,2,3" "11111111" output.csv

# Monitor progress (in another terminal)
Get-Content output.csv -Tail 10 -Wait
```

All prefilters include progress reporting (prints every 0.1% completion) and can be resumed by using the last seed from a sorted output file.

## Files

- `balatro_rng.cuh` - Core RNG implementation (LuaJIT Tausworthe + Balatro's pseudohash/pseudoseed) - **Verified accurate**
- `balatro_streams.cuh` - Item stream generation (jokers, tarots, vouchers, etc.)
- `balatro_evaluator.cuh` - Seed evaluation engine
- `balatro_filters.cuh` - Filter system for search criteria
- `soul_joker_filter_search.cu` - Negative legendary soul joker searcher
- `erratic_search.cu` - Erratic deck searcher
- `negative_legendary_prefilter.cu` - Fast legendary prefilter
- `negative_rare_prefilter.cu` - Fast rare prefilter
- `negative_uncommon_prefilter.cu` - Fast uncommon prefilter
- `verify_rng.cu` - RNG verification test
- `build.ps1` - PowerShell build script (Windows)
- `INTEGRATION.md` - Guide for integrating into graphical tools

## Credits

- RNG reverse engineering based on LuaJIT source and Balatro's `misc_functions.lua`
- Built for the BalatroSeedOracle project

## License

MIT - Do whatever you want with it!
