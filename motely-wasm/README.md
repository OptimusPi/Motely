# motely-wasm

WebAssembly package for [Motely](https://github.com/OptimusPi/MotelyJAML) — the Balatro seed search engine, with filters written in JAML.

The package ships `jaml.schema.json` (JSON Schema for the JAML filter format) at the package root, so editors can wire it up for autocomplete / validation without an extra fetch.

## Install

```sh
npm install motely-wasm
```

## Quick start

```js
import bootsharp, { Motely } from "motely-wasm";

// Boot the .NET WASM runtime. In a browser, pass the URL path under which your
// host serves the package's `bin/` directory (where dotnet.native.wasm lives).
// In Node, pass the `.wasm` bytes directly — see the "Booting" section below.
await bootsharp.boot("/motely-wasm/bin");

// A JAML filter — see https://github.com/OptimusPi/MotelyJAML for the language.
const jaml = `
name: WeeMonday
deck: Erratic
stake: Black
must:
  - joker: WeeJoker
    antes: [1]
`;

// Validate before searching — returns "valid" or an error message.
const status = Motely.validateJaml(jaml);
if (status !== "valid") throw new Error(status);

// Subscribe to results before starting.
Motely.onScoredResult.subscribe(r => console.log("match:", r.seed, r.score));
Motely.onProgress.subscribe(p => console.log(`${p.percentComplete.toFixed(1)}%`));

// Build, start, and await a search.
const search = Motely.createSearch(jaml)
  .withSequentialSearch()
  .start();

await search.waitForCompletionAsync();
console.log("done:", search.totalSeedsSearched, "searched,", search.matchingSeeds, "matched");
```

## Booting

`bootsharp.boot(...)` initializes the .NET WASM runtime. Call it once before any
`Motely.*` API. The argument shape depends on the host.

### Browser

Pass the URL path your host serves the package's `bin/` directory at (where
`dotnet.native.wasm` lives). It's host-chosen — typical mountings:

| Host | Mount `bin/` at | Boot call |
| --- | --- | --- |
| Vite / Storybook `staticDirs` | `/motely-wasm/bin` | `boot("/motely-wasm/bin")` |
| Next.js route handler | `/motely-wasm/bin/[...path]` | `boot("/motely-wasm/bin")` |
| Static page in `node_modules/motely-wasm/` | `/bin` | `boot("/bin")` |
| Raw node_modules served as static | `/node_modules/motely-wasm/bin` | `boot("/node_modules/motely-wasm/bin")` |
| Public CDN | `https://unpkg.com/motely-wasm/bin` | `boot("https://unpkg.com/motely-wasm/bin")` |

Web Workers must boot from the **same** URL as the main thread — absolute paths
in a worker resolve against the page origin, so whichever URL serves the WASM
to the main thread is the same one the worker must pass.

```js
import bootsharp from "motely-wasm";

if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
  await bootsharp.boot("/motely-wasm/bin");
}

console.log(bootsharp.getStatus()); // BootStatus.Booted
```

### Node / Bun / Deno

There's no URL to fetch from — read `dotnet.native.wasm` off disk and hand its
bytes to `boot`:

```js
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import bootsharp from "motely-wasm";

const wasmUrl = new URL("../bin/dotnet.native.wasm", import.meta.resolve("motely-wasm"));
const wasmBytes = await readFile(fileURLToPath(wasmUrl));

await bootsharp.boot({
  wasm: wasmBytes.buffer.slice(
    wasmBytes.byteOffset,
    wasmBytes.byteOffset + wasmBytes.byteLength
  ),
});
```

Requires Node ≥ 20.6 (sync `import.meta.resolve`). Node 22 LTS is the recommended floor.

`Motely.Wasm/motely.test.mjs` in the source repo is the executable reference for
this path — if Node boot breaks, that suite catches it first.

## JAML API

```js
import { Motely } from "motely-wasm";

// Validate a JAML filter string — returns "valid" on success, an error message on failure.
const status = Motely.validateJaml(jaml);

// Human-readable explanation of what a JAML filter does.
const explanation = Motely.explainJaml(jaml);

// Inspect the search plan (tally column count, CSV header, labels).
const plan = Motely.createPlan(jaml);

// Analyze specific seeds against a JAML filter.
const result = Motely.analyzeJamlSeeds(jaml, ["ABCD1234", "XYZ99"]);

// Engine version string.
console.log(Motely.version());
```

## JAML schema

`motely-wasm` ships `jaml.schema.json` at the package root — point your editor at it for
autocomplete and inline validation while writing filters:

```json
{
  "$schema": "node_modules/motely-wasm/jaml.schema.json"
}
```

Or with a `# yaml-language-server` comment at the top of any `.jaml` / `.yml` filter file:

```yaml
# yaml-language-server: $schema=node_modules/motely-wasm/jaml.schema.json
name: WeeMonday
deck: Erratic
stake: Black
must:
  - joker: WeeJoker
    antes: [1]
```

JAML is a YAML dialect (JSON-compatible). A filter is a flat document:

| Key | Purpose | Example |
|---|---|---|
| `name` | Human label | `WeeMonday` |
| `deck` | Starting deck | `Erratic`, `Red`, `Ghost`, … |
| `stake` | Difficulty floor | `White`, `Black`, `Gold`, … |
| `must` | All clauses must match | list of clause objects |
| `mustNot` | All clauses must NOT match | list of clause objects |
| `should` | Scored clauses (use with `score:`) | list of clause objects |

Each clause object names a target type (`joker`, `tarot`, `planet`, `voucher`, `tag`, `boss`) plus
optional filters (`antes`, `sources`, `min`, `score`):

```yaml
must:
  - joker: WeeJoker          # specific item name, or "Any"
    antes: [1, 2]            # which antes to check (omit = all)
    sources:
      shopItems: [0, 1]      # shop slot indices
      boosterPacks: [0]      # pack slot indices
  - tag: NegativeTag
    antes: [1]
  - voucher: Telescope
    antes: [1, 2]
    min: 2                   # appear at least N times across all listed antes
```

Use `Motely.validateJaml(jaml)` to check a filter string at runtime; `Motely.explainJaml(jaml)`
returns a human-readable plan; `Motely.createPlan(jaml)` returns the scoring structure.

## Running a search

`Motely.createSearch(jaml)` returns a settings builder. Chain a search-mode method,
then call `.start()` to get a running `IMotelySearch`.

```js
import { Motely } from "motely-wasm";

Motely.onSeedMatch.subscribe(seed => { /* matching seed string */ });
Motely.onScoredResult.subscribe(r => { /* r.seed, r.score, r.tallies */ });
Motely.onProgress.subscribe(p => { /* p.percentComplete, p.seedsSearched, p.seedsPerMillisecond, … */ });

const settings = Motely.createSearch(jaml)
  .withSequentialSearch()          // enumerate all seeds in order
  // .withRandomSearch(10_000)     // or pick N random seeds
  // .withListSearch(seeds, seeds.length) // or supply a seed list
  // .withAestheticSearch(0)       // or a JamlAesthetic mode
  .withProgressReportIntervalMs(500n);

const search = settings.start();

// Async (yields between batches — good on the main thread or in a Worker)
await search.waitForCompletionAsync();

// or synchronous (blocks until done — only inside a Worker)
// search.runSearchUntilCompletion();

console.log(search.isCompleted, search.totalSeedsSearched, search.matchingSeeds);
search.cancel(); // stop early
```

## Stream pagers

Pagers expose Balatro's raw PRNG streams — one packed int per item, no JAML filter needed.
**Always prefer `getNextChunk(n)` over repeated `getNext()` calls** — each call is a WASM
interop crossing, so batching is the right default:

```js
import { Motely, MotelyDeck, MotelyStake } from "motely-wasm";

const pager = Motely.createShopPager("AAAAAAAA", MotelyDeck.Red, MotelyStake.White, 1);
const items = pager.getNextChunk(6); // 6 packed ints, one crossing
```

Each factory targets a distinct PRNG stream — these are not the same stream filtered, so there
is one factory per stream type:

| Factory | Stream |
|---|---|
| `createShopPager(seed, deck, stake, ante)` | Mixed shop (jokers, tarots, planets, spectrals on Ghost, standard cards with MagicTrick) |
| `createJokerPager(seed, deck, stake, ante)` | Shop jokers — includes edition + sticker bits |
| `createTarotPager(seed, deck, stake, ante)` | Shop tarots |
| `createPlanetPager(seed, deck, stake, ante)` | Shop planets |
| `createSpectralPager(seed, deck, stake, ante)` | Shop spectrals (non-Ghost returns `SpectralExcludedByStream`) |
| `createLegendaryJokerPager(seed, deck, stake, ante)` | Legendary fixed-rarity joker stream |
| `createRareTagJokerPager(seed, deck, stake, ante)` | Rare Tag hand-out joker stream |
| `createTagPager(seed, deck, stake, ante)` | Tags — value is `MotelyTag` cast to int |
| `createVoucherPager(seed, deck, stake, ante)` | Vouchers — value is `MotelyVoucher` cast to int |

## Packed-int decoders

All pager streams return packed integers. Bit fields are extracted with the decode helpers
exported from the entry point:

```js
import {
    Motely,
    MotelyItemType, MotelyItemTypeCategory, MotelyJokerRarity,
    MotelyItemEdition, MotelyItemSeal, MotelyItemEnhancement,
} from "motely-wasm";

const pager = Motely.createJokerPager("AAAAAAAA", 0, 0, 1);
const v = pager.getNext();

const type      = Motely.decodeItemType(v);        // → MotelyItemType value
const category  = Motely.decodeItemCategory(v);    // → MotelyItemTypeCategory value
const rarity    = Motely.decodeJokerRarity(v);     // → MotelyJokerRarity (Common/Uncommon/Rare/Legendary)
const edition   = Motely.decodeItemEdition(v);     // → MotelyItemEdition (None/Foil/Holo/Poly/Negative)
const seal      = Motely.decodeItemSeal(v);        // → MotelyItemSeal
const enh       = Motely.decodeItemEnhancement(v); // → MotelyItemEnhancement

const perishable = Motely.isPerishable(v);  // → boolean
const eternal    = Motely.isEternal(v);     // → boolean
const rental     = Motely.isRental(v);      // → boolean

console.log(MotelyItemType[type]);      // e.g. "WeeJoker"
console.log(MotelyJokerRarity[rarity]); // e.g. "Common"
```

All enum tables (`MotelyItemType`, `MotelyItemTypeCategory`, `MotelyJokerRarity`,
`MotelyItemEdition`, `MotelyItemSeal`, `MotelyItemEnhancement`, `MotelyTag`,
`MotelyVoucher`, `MotelyBoosterPack`, `MotelyDeck`, `MotelyStake`) are exported from
`motely-wasm` and can be used for reverse-lookup by numeric value or forward-lookup by name.

## Events

| Event | Payload |
|---|---|
| `Motely.onSeedMatch` | `string` — matching seed |
| `Motely.onScoredResult` | `{ seed, score, tallies }` |
| `Motely.onProgress` | `MotelyProgress` — `percentComplete`, `seedsSearched`, `matchingSeeds`, `seedsPerMillisecond`, `elapsedMilliseconds` |
| `Motely.onFileChanges` | `Change[]` — fires when files change under a directory mounted via `Motely.mountRoot` (browser File System Access API, requires `Bootsharp.FileSystem`). Ignore if your app doesn't mount local directories. |

Subscribe and unsubscribe:

```js
const handler = r => console.log(r.seed);
Motely.onScoredResult.subscribe(handler);
Motely.onScoredResult.unsubscribe(handler);
```

## Submodule exports

| Import path | Contents |
|---|---|
| `motely-wasm` | Default export: `boot`, `getStatus`, `BootStatus`. Named export: `Motely` (main API) |
| `motely-wasm/motely` | Types: `IMotelySearch`, `IMotelySearchSettingsInterop`, `MotelyProgress`, `MotelyScoredSeedResult`, `MotelyDeck`, `MotelyStake`, enums |
| `motely-wasm/motely/filters` | `JamlAesthetic`, `JamlSearchPlan` |
| `motely-wasm/motely/analysis` | `MotelyJamlyzerResult`, `MotelySeedAnalysis` |
| `motely-wasm/bootsharp/file-system` | File-system interop (browser OPFS) — `PermissionMode`, `IFileMounter` |

## Using in a Web Worker

The WASM runtime is single-threaded. For a non-blocking UI, boot a runtime inside a
Worker and drive it with messages. This mirrors the proven setup in the `jaml-ui`
package's `searchWorker.ts`.

```js
// search-worker.js
import bootsharp, { Motely } from "motely-wasm";

let currentSearch = null;

self.onmessage = async ({ data }) => {
  if (data.type === "stop") {
    currentSearch?.cancel();
    self.postMessage({ type: "cancelled" });
    return;
  }
  if (data.type !== "start") return;

  try {
    if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
      await bootsharp.boot("/motely-wasm/bin");
    }

    const onResult = r =>
      self.postMessage({ type: "result", seed: r.seed, score: r.score });
    const onProgress = p =>
      self.postMessage({ type: "progress", percent: p.percentComplete });
    Motely.onScoredResult.subscribe(onResult);
    Motely.onProgress.subscribe(onProgress);

    try {
      currentSearch = Motely.createSearch(data.jaml)
        .withThreadCount(1)
        .withSequentialSearch()
        .start();
      await currentSearch.waitForCompletionAsync();
      self.postMessage({
        type: "complete",
        total: Number(currentSearch.totalSeedsSearched),
        matched: Number(currentSearch.matchingSeeds),
      });
    } finally {
      Motely.onScoredResult.unsubscribe(onResult);
      Motely.onProgress.unsubscribe(onProgress);
      currentSearch = null;
    }
  } catch (error) {
    self.postMessage({ type: "error", message: String(error?.message ?? error) });
  }
};

self.postMessage({ type: "ready" });
```

```js
// main thread
const worker = new Worker(new URL("./search-worker.js", import.meta.url), { type: "module" });

worker.onmessage = ({ data }) => {
  if (data.type === "result") console.log("match:", data.seed, data.score);
  if (data.type === "progress") console.log(`${data.percent.toFixed(1)}%`);
  if (data.type === "complete") console.log("done:", data.total, data.matched);
};

worker.postMessage({ type: "start", jaml });
// worker.postMessage({ type: "stop" }); // cancel early
```
