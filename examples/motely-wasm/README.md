# motely-wasm

SIMD-vectorized [Balatro](https://www.playbalatro.com/) seed search compiled to WebAssembly via Bootsharp + NativeAOT-LLVM, driven by **JAML** (Jimbo's Ante Markup Language) filters.

This README documents the **real published surface**. The generated TypeScript declarations in `motely-wasm/generated/*.g.d.mts` are the source of truth — anything documented here can be cross-checked there.

## Honesty section

- **Browser-first.** The package is published with a `browser` field in `package.json` that stubs out `node:fs`, `node:url`, `node:path`, `node:module`, `node:crypto`, and `node:process`. Bundlers honor that field automatically. Node, Deno, or Bun usage works in principle (Bootsharp boots via `fetch()`), but you must HTTP-serve the runtime assets — do not rely on `file://` paths — and the Node-only modules listed above must remain reachable for that target.
- **Single-threaded WASM.** Bootsharp removed multi-threaded mode in upstream PR #203. No `SharedArrayBuffer`, no `Cross-Origin-Opener-Policy`/`Cross-Origin-Embedder-Policy` headers are required. For parallelism, boot one Web Worker per concurrent runtime — there is one `IMotelySearch` per WASM instance.
- **Boot cost is per runtime.** Booting Bootsharp loads ~7 MB of WASM. Pay it once per worker.

## Install

```bash
npm install motely-wasm
```

## Boot

The runtime needs the binary directory passed to `bootsharp.boot(...)`. When the package is served from `node_modules/motely-wasm/`, the binaries live at `node_modules/motely-wasm/bin/`. Pick whichever URL is reachable from your page:

```ts
import bootsharp, { Motely } from "motely-wasm";

await bootsharp.boot("/bin");
// or "/motely-wasm/bin", or "/node_modules/motely-wasm/bin" — whatever your host serves.
```

`bootsharp.getStatus()` returns `bootsharp.BootStatus.Standby | Booting | Booted`. Don't double-boot.

## Search builder

A search is constructed by calling `Motely.createSearch(jaml)`, chaining configuration methods, then calling `.start(undefined)`:

```ts
const jaml = `
name: Blueprint Copy Engine
deck: Red
stake: White
must:
  - rareJoker: Blueprint
    antes: [1, 2, 3]
should:
  - rareJoker: Brainstorm
    score: 80
`;

const validation = Motely.validateJaml(jaml);
if (validation !== "valid") {
    throw new Error(validation);
}

const settings = Motely
    .createSearch(jaml)
    .withRandomSearch(10_000)
    .withThreadCount(1);

const search = settings.start(undefined);
```

The `start(cancellationToken)` argument is a Bootsharp cancellation handle. Pass `undefined` from JS.

### Configuration methods

All chainable, all return the settings object:

| Method | Purpose |
| --- | --- |
| `withRandomSearch(count)` | Random sampling — `count` independent seeds. |
| `withListSearch(seeds, seedCount)` | Verify a known seed list against the JAML filter. |
| `withSequentialSearch()` | Deterministic walk through the seed space. Combine with `withBatchCharacterCount` / `withStartBatchIndex` / `withEndBatchIndex` to slice it. |
| `withAestheticSearch(JamlAesthetic)` | Curated themed pools: `Palindrome`, `Psychosis`, `Gross`, `Nsfw`, `Funny`, `Balatro`. |
| `withDeck(MotelyDeck)` | Override the deck declared in the JAML. |
| `withStake(MotelyStake)` | Override the stake declared in the JAML. |
| `withThreadCount(n)` | Internal thread count. WASM is single-threaded — keep this at `1`. |
| `withBatchCharacterCount(n)` | Sequential search: characters per batch. Total batch space is `35^n`. |
| `withStartBatchIndex(bigint)` / `withEndBatchIndex(bigint)` | Sequential search: slice the batch range. |
| `withProgressReportIntervalMs(bigint)` | How often the progress event fires. |
| `withCsvOutput(boolean)` / `withQuietMode(boolean)` / `withAutoScoreCutoff(boolean)` | Output toggles. |

### Lifecycle

`settings.start(undefined)` returns an `IMotelySearch` proxy. The proxy exposes live properties (read every poll cycle, do not cache them):

```ts
search.totalSeedsSearched;     // bigint
search.matchingSeeds;          // bigint
search.filteredSeeds;          // bigint
search.elapsedMs;              // bigint
search.batchIndex;             // bigint
search.completedBatchCount;    // bigint
search.isCompleted;            // boolean
search.isSequentialBatchSearch; // boolean
```

And control methods:

```ts
await search.waitForCompletionAsync(undefined);
search.cancel();
```

## Events

Real Bootsharp `EventSubscriber`s — use `.subscribe(fn)` and `.unsubscribe(fn)`. They are **not** mutable handler slots. The available events on the `Motely` namespace are:

```ts
import type { MotelyProgress, MotelyScoredSeedResult } from "motely-wasm/motely";
import type { Change } from "motely-wasm/bootsharp/file-system";

const onResult = (result: MotelyScoredSeedResult) => {
    // result.seed: string, result.score: number, result.tallies: Int32Array
};
Motely.onScoredResult.subscribe(onResult);

const onProgress = (progress: MotelyProgress) => {
    // progress.seedsSearched / matchingSeeds / percentComplete / seedsPerMillisecond / ...
};
Motely.onProgress.subscribe(onProgress);

const onSeedMatch = (seed: string) => { /* raw seed string for every match */ };
Motely.onSeedMatch.subscribe(onSeedMatch);

const onFileChanges = (changes: Change[]) => { /* Bootsharp.FileSystem watcher events */ };
Motely.onFileChanges.subscribe(onFileChanges);

// later
Motely.onScoredResult.unsubscribe(onResult);
Motely.onProgress.unsubscribe(onProgress);
Motely.onSeedMatch.unsubscribe(onSeedMatch);
Motely.onFileChanges.unsubscribe(onFileChanges);
```

## JAML validation & analysis

```ts
Motely.version();                  // string — assembly version
Motely.validateJaml(jaml);         // string — "valid" or an error message
Motely.explainJaml(jaml);          // string — human-readable plan summary
Motely.createPlan(jaml);           // JamlSearchPlan — tally column metadata
Motely.analyzeJamlSeeds(jaml, seeds); // MotelyJamlyzerResult — full per-seed analysis
```

`MotelyJamlyzerResult` (see `motely-wasm/generated/motely/analysis.g.d.mts`) contains per-seed `MotelySeedAnalysis` with `antes[]`, `boss`, `voucher`, `smallBlindTag`, `bigBlindTag`, `shopQueue[]`, `packs[]`, and optional `drawOrder` / `erraticDeckComposition` / `erraticDeckBreakdown`.

## File system (optional)

The `@rewaffle/bootsharp-file-system` package is an optional peer. When present, mount it before booting and the Motely-side library APIs become available:

```ts
import bootsharp, { Motely } from "motely-wasm";
import { IFileMounter } from "motely-wasm/bootsharp/file-system";
import * as fs from "@rewaffle/bootsharp-file-system";

fs.init(IFileMounter);
await bootsharp.boot("/bin");

const root = await Motely.pickRoot(undefined);
if (root) {
    await Motely.mountRoot(root, undefined);
    const text = await Motely.readTextFile(root, "filters/example.jaml");
    // ...
}
```

Available file APIs: `pickRoot`, `mountRoot`, `unmountRoot`, `readTextFile`, `writeTextFile`, plus the `onFileChanges` event for live watch.

## Loading without a bundler

For environments that don't bundle node modules (sandboxed iframes, `<script type="module">`, etc.), import the package from a public npm CDN. Pin the version — `@latest` defeats long-term browser caching.

```ts
const mod = await import("https://unpkg.com/motely-wasm@17.3.0/index.mjs");
await mod.default.boot("https://unpkg.com/motely-wasm@17.3.0/bin");
const { Motely } = mod;
```

Equivalent jsDelivr URL: `https://cdn.jsdelivr.net/npm/motely-wasm@17.3.0/index.mjs`.

### Content Security Policy

For sandboxed iframes that need the CDN, allow script and fetch from whichever host serves it:

```text
script-src https://unpkg.com https://cdn.jsdelivr.net
connect-src https://unpkg.com https://cdn.jsdelivr.net
```

`wasm-unsafe-eval` (or the legacy `unsafe-eval`) is required for the WASM runtime to instantiate.

## Parallelism via Web Workers

Each WASM runtime is one search at a time. To get multi-core throughput on a device, boot one runtime per Web Worker and partition the work yourself:

- **`withRandomSearch(count)`** — split `count` across workers. Each runtime's PRNG is independent.
- **`withListSearch(seeds, count)`** — partition the seed array across workers.
- **`withSequentialSearch()`** — use `withBatchCharacterCount(n)` then assign disjoint `[withStartBatchIndex, withEndBatchIndex)` ranges to each worker. Total batch space is `35^n`.
- **`withAestheticSearch(JamlAesthetic)`** — the aesthetic providers hold a single shared enumerator inside one runtime. They **cannot** be partitioned across independent WASM runtimes, because each runtime gets its own enumerator that restarts from the beginning. Run aesthetic mode on a single worker, or run multiple workers and accept full duplication.

`jaml-ui` ships a `useSearchPool` React hook that implements this partitioning. Read `src/hooks/searchPoolWorker.ts` and `src/hooks/useSearchPool.ts` for a working pattern.

## Types

```ts
import type {
    IMotelySearch,
    IMotelySearchSettingsInterop,
    MotelyProgress,
    MotelyScoredSeedResult,
    MotelyDeck,
    MotelyStake,
    MotelyBoosterPack,
    MotelyBossBlind,
    MotelyTag,
    MotelyVoucher,
    MotelyItem,
} from "motely-wasm/motely";

import type { JamlAesthetic, JamlSearchPlan } from "motely-wasm/motely/filters";

import type {
    MotelyJamlyzerResult,
    MotelySeedAnalysis,
    MotelyAnteAnalysis,
    MotelyBoosterPackAnalysis,
    MotelyAnalyzedItem,
} from "motely-wasm/motely/analysis";
```

Generated TypeScript declarations under `motely-wasm/generated/` are the source of truth — Bootsharp emits them from the C# interfaces, so they never drift from runtime behavior.

## Build details

NativeAOT-LLVM with SIMD enabled (`-msimd128`), threads explicitly disabled. The `MONO_WASM:` prefix in console logs is Bootsharp's glue log — this is **not** a Mono runtime build.

## License

MIT — © pifreak
