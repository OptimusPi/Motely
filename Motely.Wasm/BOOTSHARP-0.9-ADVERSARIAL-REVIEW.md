# Motely.Wasm Bootsharp 0.9 Adversarial Review

**Date:** 2026-08-10  
**Scope:** `Motely.Wasm/`, Bootsharp 0.9 interop, published `motely-wasm@25.0.3` compatibility

## Verdict

The Motely.Wasm C# surface is written in the intended Bootsharp style, but the repository host and smoke test do not match the published `motely-wasm@25.0.3` package that consumers install.

## Findings

### Critical: published API mismatch

The repository host expects a generated `MotelyWasmApi` namespace and these members:

- `runScoreSeeds`
- `takeRun`
- `runFindSeeds`

Those assumptions appear in `Motely.Wasm/host/main.mjs` and `Motely.Wasm/host/index.html`.

The live `motely-wasm@25.0.3` package instead exposes generated namespaces including:

- `MotelyJaml`
- `MotelyLsp`
- `MotelySearch`
- `MotelyJamlyzer`
- `MotelyUtilities`
- `MotelyWasm`

Its search API returns promises directly:

- `MotelySearch.searchList(config)`
- `MotelySearch.searchRandom(config, count)`
- `MotelySearch.searchSequential(...)`
- `MotelySearch.collect(config, stopAfter)`
- `MotelySearch.collectSequential(...)`
- `MotelySearch.findOne(config)`

The generated declarations expose `Promise<Array<MotelyScoredSeedResult>>`; they do not expose the repository's `run/take` API.

**Impact:** the checked-in browser host cannot be assumed to run against the published package. The local smoke path can pass while the npm artifact has a different contract.

### High: stale global async result slot

`Motely.Wasm/MotelyWasmApi.cs` stores the last result in a static `_completedRun` slot and exposes `TakeRun()` after an async `Task` completes.

The official Bootsharp serialization guide documents promises/tasks and immutable record serialization, and the 25.0.3 generated declarations expose direct promise-returning search methods. The global slot is therefore not representative of the published 25.0.3 surface and is unsafe under overlapping calls: one run can overwrite another run's result before it is taken.

### High: smoke test validates only local generated output

`Motely.Wasm/host/index.html` imports `Motely.Wasm/bin/motely-wasm` and exercises that local generated module. It does not install or import the published npm package.

**Impact:** API drift between the checked-in source output and `motely-wasm@25.0.3` is not detected by the current smoke test.

### Medium: version identity is split

The repository stamps the engine as `25.1.0` in `Directory.Build.props`, while the requested/latest published npm package is `25.0.3`.

**Impact:** the browser head, VS Code extension, and native engine can silently run different versions unless the release process pins and verifies the artifact identity.

### Medium: WASM is excluded from the normal solution build

`Motely.slnx` explicitly excludes `Motely.Wasm` and requires a separate publish command.

The documented publish could not be reproduced in the review environment because the .NET `wasm-tools` workload was missing. Workload installation was canceled by the environment and rolled back.

## Bootsharp facts verified from the official guide

Sources:

- <https://bootsharp.com/guide/declarations>
- <https://bootsharp.com/guide/serialization>
- <https://bootsharp.com/guide/build-config>
- <https://bootsharp.com/guide/specialization>
- <https://bootsharp.com/guide/interop-modules>
- <https://bootsharp.com/guide/llvm>

Confirmed behavior:

- Bootsharp generates TypeScript declarations from C# interop APIs.
- Immutable records, structs, and read-only collections are serialized automatically.
- Enums cross as numeric values with generated name/index mappings.
- C# dictionaries cross as JavaScript `Map` values.
- Bootsharp 0.9 Release publishing enables NativeAOT-LLVM automatically.
- The intended integration is generated Bootsharp interop, not a manually invented JSON or DTO marshaling layer.

## Review boundary

No source code was changed as part of this review. This file records the findings and verification state only.
