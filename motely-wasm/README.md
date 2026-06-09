# motely-wasm

> Balatro seed search + per-seed analysis in the browser — a vectorized SIMD engine compiled to WebAssembly.

[![npm version](https://img.shields.io/npm/v/motely-wasm.svg)](https://www.npmjs.com/package/motely-wasm)
[![license](https://img.shields.io/npm/l/motely-wasm.svg)](./package.json)
[![types](https://img.shields.io/badge/types-included-blue.svg)](#subpath-exports--where-the-types-live)
[![WASM](https://img.shields.io/badge/runtime-embedded%20WASM-purple.svg)](#boot)

The C# Motely seed engine, AOT/SIMD-compiled to WebAssembly via [Bootsharp](https://github.com/elringus/bootsharp). Author filters in **JAML** (Jimbo's Ante Markup Language), run real searches over Balatro's seed space, and analyze any single seed — all client-side, no server.

- **Embedded** — the `.wasm` is inlined into the module. `boot()` takes no args, nothing to copy or serve.
- **Typed** — full TypeScript declarations, emitted per C# namespace, served on subpath imports.
- **Streaming** — subscribe to progress/match/scored events, or filter live with a JS predicate (`jimmolate`).

## Install

```sh
npm install motely-wasm
# or
pnpm add motely-wasm
```

Requires a host with WASM + ES modules: modern browsers, Node 18+, Deno, Bun. For browser UIs, run searches in a **Web Worker** — every `run*` call blocks its thread until the search completes (WASM has no pthreads).

## Boot

The WASM binary is **embedded** — no files to copy, no path to serve, no `bin/` to wire up. `boot()` takes no arguments.

```ts
import bootsharp from "motely-wasm";

await bootsharp.boot(); // embedded: no args
```

> **`bootsharp` is the _default_ export.** `boot`, `exit`, `getStatus`, `BootStatus`, and `manifest` hang off it. Do **not** write `import * as bootsharp` — `boot` is not a named export and `bootsharp.boot` would be `undefined`.

> **`[Import]` bindings are snapshotted at `boot()`.** Assign `Program.jimmolatePredicate` (and any other `[Import]`) **before** calling `boot()`. You may reassign the predicate after boot to swap dispatch logic.

## Quick start — run a search

```ts
import bootsharp from "motely-wasm";
import { Program } from "motely-wasm/motely/wasm";
import type { MotelyScoredSeedResult, MotelyProgress } from "motely-wasm/motely";

await bootsharp.boot();

// 1. Parse JAML — throws on invalid input.
const config = Program.parseJaml(`
must:
  - joker: Blueprint
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
deck: Red
stake: White
`);

// 2. Stream results as the engine scores seeds.
Program.onProgress.subscribe((p: MotelyProgress) =>
  console.log(`${p.percentComplete.toFixed(1)}%  (${p.matchingSeeds} hits)`)
);
Program.onScoredResult.subscribe((r: MotelyScoredSeedResult) =>
  console.log(`MATCH ${r.seed}  score=${r.score}`)
);

// 3. Run. Blocks until done (call from a Web Worker for a live UI).
const search = Program.runRandomSearch(config, 100_000);
console.log(`done: ${search.matchingSeeds}/${search.totalSeedsSearched} seeds`);
```

### Search entry points

| Method | Searches |
|--------|----------|
| `runSequentialSearch(config, startBatch?, endBatch?, batchChars?, intervalMs?)` | The seed space in order, batch by batch |
| `runRandomSearch(config, count)` | `count` random seeds |
| `runSeedListSearch(config)` | The seeds listed in `config.seeds` |
| `runAestheticSearch(config, aesthetic)` | Seeds matching a `JamlAesthetic` lens |
| `runNativeListSearch(filterName, seeds)` | A list through a built-in native filter |
| `runPassthroughListSearch(seeds)` | A list with no filtering (decode/inspect) |

Each returns an `IMotelySearch` whose counters (`matchingSeeds`, `totalSeedsSearched`, `isCompleted`, …) are ready to read on return.

## Jimmolate — filter live with a JS predicate

`jimmolate` is the original Immolate `filter(seed) => keep?` model, in the browser. The C# engine does the SIMD work; your JS predicate decides which **scored** results survive. A seed reaches your handler only if the predicate keeps it.

```ts
import bootsharp from "motely-wasm";
import { Program } from "motely-wasm/motely/wasm";
import type { MotelyScoredSeedResult } from "motely-wasm/motely";

// Assign the predicate BEFORE boot (it's an [Import], snapshotted at boot).
Program.jimmolatePredicate = (r: MotelyScoredSeedResult) => r.score >= 20;

await bootsharp.boot();

Program.jimmolateEnabled = true; // gate on — without this, every scored seed is reported
Program.onScoredResult.subscribe((r) => keep(r)); // fires only for kept seeds

Program.runRandomSearch(Program.parseJaml(jaml), 1_000_000);
```

Leave `jimmolateEnabled` false to receive **every** scored seed (no filtering).

JAML ⇄ JSON conversion is available too: `Program.jamlToJson(jaml)` and `Program.jsonToJaml(json)`.

## Subpath exports — where the types live

Bootsharp emits one declaration file **per C# namespace** ([Bootsharp · Type Declarations](https://github.com/elringus/bootsharp) → "One `.g.d.mts` file is emitted per C# namespace, colocated with the matching `.g.mjs` binding"). Members in a namespace land on a matching **subpath**; the bare top-level import is intentionally empty. **Import types from the subpath, not the package root.**

| Import | Contents |
|--------|----------|
| `motely-wasm` | Default export: `boot()`, `exit()`, `getStatus()`, `BootStatus`, `manifest` |
| `motely-wasm/motely/wasm` | `Program` — the engine API (search, JAML parse, file I/O) |
| `motely-wasm/motely` | Core types: `MotelyScoredSeedResult`, `MotelyProgress`, `IMotelySearch`, `MotelyItem`, `MotelyMatchSource` |
| `motely-wasm/motely/enums` | Engine enums: `MotelyDeck`, `MotelyStake`, `MotelyVoucher`, `MotelyTag`, `MotelyBossBlind`, `MotelyBoosterPack`, joker enums, … |
| `motely-wasm/motely/filters/jaml` | `JamlConfig`, `JamlAesthetic` |
| `motely-wasm/motely/filters` | `JamlSearchPlan` |
| `motely-wasm/bootsharp/file-system` | `IFileSystem`, `IFileMounter`, `Change`, `MountOptions`, `PickOptions` |

```ts
import { Program } from "motely-wasm/motely/wasm";
import type { IMotelySearch, MotelyProgress, MotelyScoredSeedResult } from "motely-wasm/motely";
import { MotelyDeck, MotelyStake } from "motely-wasm/motely/enums";
import { JamlAesthetic } from "motely-wasm/motely/filters/jaml";
```

## Events

Subscribe before running a search.

| Event | Payload | Fires |
|-------|---------|-------|
| `Program.onProgress` | `MotelyProgress` | On the progress interval during a run |
| `Program.onSeedMatch` | `string` (seed) | When a seed matches the filter |
| `Program.onScoredResult` | `MotelyScoredSeedResult` | Per scored seed (post-`jimmolate` filter) |
| `Program.onFileChanges` | `Change[]` | When a mounted directory changes |

## File system (browser File System Access API)

The engine reads and writes JAML files from a user-picked directory via the browser File System Access API. This capability ships as a **separate peer package** — [`@rewaffle/bootsharp-file-system`](https://www.npmjs.com/package/@rewaffle/bootsharp-file-system), a sponsor-exclusive Bootsharp extension — because the WASM engine doesn't bundle the mounter itself. Install it and **wire the mounter before `boot()`**: `IFileMounter` is a Bootsharp `[Import]`, so calling `Program.pickRoot()` without `fs.init()` throws.

```sh
npm install @rewaffle/bootsharp-file-system   # peer package
```

```ts
import bootsharp from "motely-wasm";
import { Program } from "motely-wasm/motely/wasm";
import { IFileMounter, PermissionMode } from "motely-wasm/bootsharp/file-system";
import * as fs from "@rewaffle/bootsharp-file-system";

// 1. Bind the mounter BEFORE boot (every [Import] is snapshotted at boot()).
fs.init(IFileMounter);
await bootsharp.boot();

// 2. Now the file APIs on Program work.
const root = await Program.pickRoot({ mode: PermissionMode.ReadWrite }); // null if cancelled
if (root) {
  await Program.mountRoot(root, { mode: PermissionMode.ReadWrite });
  const jaml = await Program.readTextFile(root, "filters/blueprint.jaml");
  await Program.writeTextFile(root, "filters/out.jaml", updated);
  await Program.unmountRoot(root);
}
```

The search / JAML / analysis APIs don't route through this — they run whether or not the mounter is wired. The directory APIs (`pickRoot`, `mountRoot`, `readTextFile`, `writeTextFile`) are what need it; unwired, they throw.

### Mobile browsers / single file

The directory mount relies on `showDirectoryPicker`, which iOS Safari and most mobile browsers **do not support** — and `Program` exposes only directory-*root* mounting (`pickRoot` / `mountRoot`), no single-file pick. So don't mount on mobile. You don't need to: `parseJaml`, `runRandomSearch`, etc. all take a JAML **string**. Read one file with a plain `<input type="file">` and feed its text straight in — works on every browser, no FS package required:

```ts
const file = input.files[0];                 // <input type="file" accept=".jaml,.yaml">
const config = Program.parseJaml(await file.text());
const search = Program.runRandomSearch(config, 100_000);
```

## Bundler notes

The `browser` field in `package.json` stubs Node built-ins (`node:fs`, `node:url`, `node:path`, `node:module`, `node:crypto`, `node:process`) to `false`, so Vite/Webpack skip them cleanly. In library wrappers, **externalize** `motely-wasm` (and `motely-wasm/*`) so the consuming app controls WASM resolution.

## Build from source

```sh
npm run build   # dotnet publish ../Motely.Wasm/Motely.Wasm.csproj -c Release
```

Release builds use NativeAOT-LLVM (speed-optimized, trimmed). Output lands in `dist/`, which Bootsharp **regenerates wholesale on every publish** — never hand-edit anything under `dist/`.

## Maintainers

`package.json` is **hand-maintained and static** — edit it directly. Do **not** point `BootsharpPackageDirectory` at this directory: Bootsharp overwrites `package.json` on every build with a bare template (no version, types, or author), which breaks `npm publish`. The build parks that throwaway template in `obj/` (gitignored) instead. See the comment in `Motely.Wasm.csproj`.

Bump versions with `npm version patch` (never hand-edit the `version` field), then `npm publish`.

## License

MIT © Nathanial P. Howard
