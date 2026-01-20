# GOATELY Final Form - Architecture Plan

**GOATELY** = **G**PU + **O**f-**A**ll-**T**ime + **L**Y (from Motely) = Greatest GPU Balatro Seed Searcher

## Vision

Combine the best of:
- **Balatro Seed Oracle/Motely** (CPU SIMD C#, JAML, precise PRNG)
- **Ouija/Immolate** (OpenCL/C, GPU search patterns)
- **dungmot** (Current CUDA/HIP unified codebase)

Into the **ULTIMATE GPU SEARCHER** that:
- ✅ Runs on **both NVIDIA and AMD** (HIP unified)
- ✅ Uses **JAML** for configuration (like Motely)
- ✅ Has **precise, accurate PRNG** (matches Motely CPU exactly)
- ✅ **Fast as FUCK** on both GPU platforms
- ✅ Will integrate with **Balatro Seed Oracle** (CPU + GPU hybrid)

## Requirements

### Core Functionality
- [ ] **JAML Compatibility**: Parse JAML configs (use proper YAML library, not hand-rolled)
- [ ] **Precise PRNG**: Match Motely CPU's exact RNG implementation
- [ ] **Same Scoring**: Identical search logic and scoring as Motely
- [ ] **GPU Performance**: SIMT optimizations, warp-level primitives, launch bounds
- [ ] **Cross-Platform**: NVIDIA (CUDA) and AMD (HIP) support

### Architecture

```
GOATELY/
├── core/
│   ├── gpu_common.h          # Unified CUDA/HIP abstraction (DONE)
│   ├── balatro_rng.cuh       # Precise PRNG (needs Motely verification)
│   ├── balatro_game.cuh      # Game state simulation
│   └── jaml_parser.h         # Proper YAML parser (yaml-cpp or similar)
├── search/
│   ├── search_kernel.cuh     # Main GPU search kernel
│   ├── scoring.cuh           # Scoring logic (matches Motely)
│   └── filters.cuh           # JAML filter evaluation
├── host/
│   ├── jaml_loader.cpp       # Load and parse JAML configs
│   ├── gpu_manager.cpp       # GPU initialization, memory management
│   └── result_processor.cpp  # Process and output results
└── executables/
    └── goately_search.cu     # Main entry point
```

### Key Design Decisions

1. **JAML Parser**: Use `yaml-cpp` or similar library (C++), NOT hand-rolled
2. **PRNG Verification**: Compare against Motely CPU output, ensure bit-perfect match
3. **Unified API**: Keep `gpu_common.h` abstraction for CUDA/HIP compatibility
4. **Modular Design**: Separate kernels, scoring, filters for maintainability
5. **Performance**: SIMT optimizations, warp-level aggregation, proper launch bounds

### Integration with Balatro Seed Oracle

- GPU searcher finds candidate seeds (fast, broad search)
- CPU (Motely) verifies and refines (precise, narrow search)
- Results combined and presented in Seed Oracle UI

## Implementation Phases

### Phase 1: Foundation (Current Branch)
- ✅ Unified GPU abstraction (`gpu_common.h`)
- ✅ HIP support for all targets
- ✅ Remove hand-rolled YAML parser
- [ ] Add proper YAML library integration
- [ ] Verify PRNG matches Motely exactly

### Phase 2: JAML Integration
- [ ] Integrate yaml-cpp or similar
- [ ] Parse JAML configs to GPU-friendly structures
- [ ] Support all Motely JAML features (sources, filters, etc.)

### Phase 3: Search Kernel
- [ ] Implement unified search kernel
- [ ] Add scoring logic (match Motely)
- [ ] Add filter evaluation (JAML-based)

### Phase 4: Performance
- [ ] SIMT optimizations
- [ ] Warp-level result aggregation
- [ ] Optimal launch bounds per GPU
- [ ] Memory access patterns

### Phase 5: Integration
- [ ] API for Balatro Seed Oracle
- [ ] Result format compatibility
- [ ] CPU/GPU hybrid search coordination

## Naming Options

- **GOATELY** = GPU + Greatest Of All Time + LY (from Motely) ✅
- **GOTELY** = GPU + Greatest Of All Time + LY
- **GAML** = GPU + JAML (but less descriptive)
- **BALATRO-GPU** = Descriptive but boring

**Recommendation: GOATELY** 🐐

## Next Steps

1. ✅ Commit current work
2. ✅ Create feature branch
3. [ ] Research yaml-cpp integration
4. [ ] Compare PRNG with Motely CPU output
5. [ ] Design unified search kernel architecture
6. [ ] Implement JAML parser integration
7. [ ] Build prototype search kernel
