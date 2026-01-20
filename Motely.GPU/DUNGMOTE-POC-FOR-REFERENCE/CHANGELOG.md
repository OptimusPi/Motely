# Changelog

## [1.0.0] - 2025-01-06

### Added
- Progress reporting to all prefilters (prints every 0.1% completion)
- RNG verification test (`verify_rng.cu`)
- Host version of seed conversion functions (`seed_string_to_index_host`)
- Resume capability documentation
- **HIP/ROCm support** - Unified GPU abstraction layer (`gpu_common.h`) for NVIDIA CUDA and AMD HIP compatibility
- **DuckDB seed source output** - GPU searchers can now create Motely-compatible seed source databases via `--output-db` flag
- HIP build script (`build_hip.ps1`) for AMD GPU builds
- Cross-platform GPU support documentation (`HIPIFY_GUIDE.md`, `HIP_CU_COMPATIBILITY.md`)

### Changed
- **BREAKING**: Consolidated RNG implementation - `balatro_rng_v2.cuh` merged into `balatro_rng.cuh`
- Renamed prefilters to remove "test_" prefix:
  - `test_negative_prefilter.cu` → `negative_legendary_prefilter.cu`
  - `test_rare_negative_prefilter.cu` → `negative_rare_prefilter.cu`
  - `test_uncommon_negative_prefilter.cu` → `negative_uncommon_prefilter.cu`
- Updated all includes to use consolidated `balatro_rng.cuh`
- Updated build targets and run scripts to match new file names
- **All executables now support both NVIDIA (nvcc) and AMD (hipcc)** - build script automatically detects platform
- Removed hand-rolled YAML parser from `negative_tag_skipper.cu` - now uses command-line arguments only
- Build script now correctly shows 13/13 executables

### Fixed
- RNG precision issue: v1 was missing precision rounding that matches Balatro's `string.format("%.13f", ...)`
- Verified v2 implementation matches Balatro game output exactly
- Fixed duplicate function definitions in prefilter files
- Fixed build script executable count (was showing 10/10, 10/11, 11/12, 12/12 - now correctly 13/13)
- Fixed HIP support - all targets now respect `--HIP` flag and use `hipcc` when enabled
- Fixed missing `GPU_FREE` for result count buffer

### Removed
- `balatro_rng_v2.cuh` (consolidated into `balatro_rng.cuh`)
- Hand-rolled YAML parser from `negative_tag_skipper.cu` (replaced with command-line args)

### Documentation
- Created `RNG_VERIFICATION_RESULTS.md` documenting RNG accuracy verification
- Created `INTEGRATION.md` for graphical tool integration guidance
- Created `HIPIFY_GUIDE.md` for AMD GPU build instructions
- Created `HIP_CU_COMPATIBILITY.md` explaining how HIP compiles `.cu` files
- Created `DUCKDB_INTEGRATION.md` for seed source output usage
- Created `DUCKDB_SETUP.md` for DuckDB library setup
- Created `GOATELY_FINAL_FORM_PLAN.md` for future unified GPU searcher architecture
- Updated build script help text

