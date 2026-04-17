# Motely.Wasm — `motely-wasm` npm package

Bootsharp + NativeAOT-LLVM build of [`Motely/`](../Motely) that publishes the **`motely-wasm`** npm package under [`../motely-wasm/`](../motely-wasm).

## Runtime

- `TargetFramework` = **`net10.0`** (no `net10.0-browser` TFM — `<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>` is what makes this build browser-targeted).
- Published with **NativeAOT-LLVM** (`<BootsharpLLVM>true</BootsharpLLVM>`), speed-optimized, AOT-trimmed (`<BootsharpAggressiveTrimming>true</BootsharpAggressiveTrimming>`), SIMD enabled (`<WasmEnableSIMD>true</WasmEnableSIMD>` + `-msimd128` emcc flag).
- Binaries are **embedded** into the single-file ES module (`<BootsharpEmbedBinaries>true</BootsharpEmbedBinaries>`) — no sideloaded resources.

## Publish

```powershell
# wasm-opt (Binaryen) must be on PATH; Bootsharp invokes `npx rollup` to bundle — the root devDependencies include `rollup`.
dotnet publish Motely.Wasm -c Release
```

Outputs:

- `../motely-wasm/index.mjs` — single-file ES module with embedded binaries.
- `../motely-wasm/types/bindings.g.d.ts` — generated TS declarations (interfaces, enums, DTOs).
- `../motely-wasm/package.json` — version stamped from `<MotelyVersion>` by the `PatchPackageJsonVersion` MSBuild target.

## Public surface

The only interop contract is the interface [`Motely/IMotelyWasm.cs`](../Motely/IMotelyWasm.cs), implemented by [`Motely/MotelyWasmHost.cs`](../Motely/MotelyWasmHost.cs) and exported via `[assembly: JSExport(typeof(Motely.IMotelyWasm))]` in [`Program.cs`](./Program.cs).

### Pure methods

| Method | Returns |
|--------|---------|
| `GetVersion()` | `string` — `MotelyVersion` from `Directory.Packages.props`. |
| `ValidateJaml(string jaml)` | `"valid"` or a human-readable error message. |
| `CompileJummy(string jummy)` | Compiled JAML string (throws on invalid Jummy). |

### Searches (return `IMotelyWasmSearch`)

All search methods are **single-threaded** in WASM (`.WithThreadCount(1)`); threading is not a parameter:

- `StartRandomSearch(jaml, randomSeedCount)`
- `StartAestheticSearch(jaml, JamlAesthetic)` — e.g. Palindrome.
- `StartSequentialSearch(jaml, batchCharCount, startBatch, endBatch)` — `batchCharCount` only applies here; provider searches use fixed vector-width batches.
- `StartSeedListSearch(jaml, string[] seeds)` — **array**, not CSV.
- `StartKeywordSearch(jaml, keywordsCsv, paddingChars)` — keywords are currently passed as comma-separated text.

Batch-helper: `RunSequentialSearchBatch(jaml, batchCharCount, startBatch, endBatch, maxResults)` — starts, awaits, collects up to `maxResults`, and disposes.

### `IMotelyWasmSearch`

| Member | Purpose |
|--------|---------|
| `GetSnapshot()` | Progress (elapsed, matching/filtered/total seeds, batch index). |
| `Cancel()` | Cancel the search. |
| `WaitForCompletion()` | `Task<MotelyWasmSearchCompletion>` — resolves with `Completed` / `Cancelled` / `Faulted`. |
| `Dispose()` | Releases the underlying engine search. |

Search hits are delivered through the imported `IMotelyWasmEvents.NotifyResult(...)` event surface rather than a drained queue.

### Single-seed inspection

`CreateSearchContext(seed, deck, stake)` returns `IMotelyWasmSearchContext` — boss / voucher / tag / booster / shop item / joker / tarot / spectral / planet / lucky-money / lucky-mult / misprint / erratic streams, with `*Chunk` variants for batched pulls.

## Bootsharp rules that matter here

1. **Do not call one `[JSExport]` interface method from another on `this`.** Shared logic lives in `private` helpers (`MotelyWasmHost.ParseJaml`). Violating this causes `Invalid Program: attempted to call a UnmanagedCallersOnly method from managed code.`
2. **Interop instances** — `IMotelyWasmSearch` and `IMotelyWasmSearchContext` cross the boundary as **instance bindings** (per Bootsharp [interop-instances](https://bootsharp.com/guide/interop-instances.html)). They cannot be arguments or return values of another instance method, and cannot be arguments of events.
3. **Enums** marshal as numbers; TS side gets name/index maps automatically.
4. **Nullability** — nullable args emit `| undefined`, nullable returns emit `| null` (Bootsharp nullability convention).

## Required toolchain for publish

- .NET 10 SDK.
- **Binaryen `wasm-opt`** on `PATH` (Bootsharp aborts publish with MSBuild error 9009 if it can't resolve it).
- Node.js + a working `npx` — Bootsharp bundles the final ESM with `npx rollup …` and resolves `rollup` from the workspace `node_modules`.

See [Bootsharp docs](https://bootsharp.com/guide/) (mirrored locally at `D:\bootsharp\docs`) for interop-interfaces, LLVM, sideloading, and serialization rules.

## Downstream consumers (don't re-implement — use these)

- **[`jaml-ui`](https://www.npmjs.com/package/jaml-ui)** — shared TS surface + React components over `motely-wasm`. All canonical Motely/JAML display labels, enum mappings, and filter UI belong here.
- **[`jimbo-ui`](https://www.npmjs.com/package/jimbo-ui)** — Balatro-themed visual components.

If an app repo (e.g. `seedfinder.app`) starts hand-rolling Motely/JAML display logic or regex-parsing JAML, that is a signal to extend `IMotelyWasm` here (export the missing piece) and consume it via `jaml-ui` — **not** to duplicate logic on the TS side.

## Distribution

- **npm:** `motely-wasm` (single "compat"-shaped package, embedded binaries).
- **CDN mirror:** `https://cdn.seedfinder.app/motely-wasm/<version>/index.mjs` and `.../latest/index.mjs` — served alongside `jammy.seedfinder.app` / `mcp.seedfinder.app`.
- **Zero-setup fallbacks:** `https://unpkg.com/motely-wasm@<version>/index.mjs`, `https://cdn.jsdelivr.net/npm/motely-wasm@<version>/index.mjs`.

## Package metadata

`motely-wasm/package.json` is rewritten end-to-end by the `WriteFullPackageJson` MSBuild target in [`Motely.Wasm.csproj`](./Motely.Wasm.csproj) **after** Bootsharp's minimal template is emitted. This is why `description`, `homepage`, `repository.directory`, `bugs`, `keywords`, and `license` survive every publish — if any of those need to change, edit the target, not the emitted file.
