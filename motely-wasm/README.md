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

// One callback set per WASM load — subscribe after boot, before .start().
Motely.onScoredResult.subscribe(r => console.log("match:", r.seed, r.score));
Motely.onProgress.subscribe(p => console.log(`${p.percentComplete.toFixed(1)}%`));

const search = Motely.fromJaml(jaml).withSequentialSearch().start();

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
| Public CDN (pinned version) | `https://unpkg.com/motely-wasm@<version>/bin` | `boot("https://unpkg.com/motely-wasm@18.2.2/bin")` |

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

Same ESM import as the browser. Point `boot()` at the published `bin/` directory
(Node `fetch` accepts `file://` URLs):

```js
import bootsharp, { Motely } from "motely-wasm";

const binRoot = new URL("bin/", import.meta.resolve("motely-wasm/package.json")).href;
await bootsharp.boot(binRoot);

const status = Motely.validateJaml(jaml);
```

Requires Node ≥ 20.6 (`import.meta.resolve`). Node 22 LTS is the recommended floor.

**Publish gate (repo):** after `dotnet publish Motely.Wasm -c Release`, run
`node Motely.Wasm/motely.test.mjs` (installed-layout harness) and
`node Motely.Wasm/pack-consumer-smoke.mjs` (`npm pack` → fresh `npm install` → same boot path).

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

Use `Motely.createSearchSettings()`, `Motely.createNativeSearchSettings(name)`, or
`Motely.fromJaml(jaml)` — then chain modes. Callbacks are registered once per WASM load on `Motely`.

```js
import { Motely } from "motely-wasm";

Motely.onSeedMatch.subscribe(seed => { /* … */ });
Motely.onScoredResult.subscribe(r => { /* … */ });
Motely.onProgress.subscribe(p => { /* … */ });

const search = Motely.fromJaml(jaml).withSequentialSearch().start();

// Async (yields between batches — good on the main thread or in a Worker)
await search.waitForCompletionAsync();

// or synchronous (blocks until done — only inside a Worker)
// search.runSearchUntilCompletion();

console.log(search.isCompleted, search.totalSeedsSearched, search.matchingSeeds);
search.cancel(); // stop early
```

## Stream cursor

`Motely.createStreamCursor(seed, deck, stake, ante, kind)` returns a stateful cursor over
one of Balatro's per-ante PRNG streams — one packed int per item, no JAML filter needed.
The `kind` argument selects which stream; one factory + one enum arg covers every stream
type:

```js
import { Motely } from "motely-wasm";
import { MotelyStreamKind } from "motely-wasm/motely";
import { MotelyDeck, MotelyStake } from "motely-wasm/motely/enums";

const cursor = Motely.createStreamCursor("AAAAAAAA", MotelyDeck.Red, MotelyStake.White, 1, MotelyStreamKind.Shop);
const items = cursor.getNextChunk(6); // 6 packed ints, one WASM crossing
```

**Always prefer `getNextChunk(n)` over repeated `getNext()` calls** — each call is a WASM
interop crossing, so batching is the right default.

| `MotelyStreamKind` value | Stream | Returned int |
|---|---|---|
| `Shop` | Mixed shop (jokers, tarots, planets, spectrals on Ghost, standard cards with MagicTrick) | Packed item (`decodeItemType`, `decodeItemCategory`, …) |
| `Joker` | Shop jokers — includes edition + sticker bits | Packed item |
| `Tarot` | Shop tarots | Packed item |
| `Planet` | Shop planets | Packed item |
| `Spectral` | Shop spectrals (non-Ghost returns `SpectralExcludedByStream`) | Packed item |
| `LegendaryJoker` | Legendary fixed-rarity joker stream | Packed item |
| `RareTagJoker` | Rare Tag hand-out joker stream | Packed item |
| `Tag` | Tags — raw `MotelyTag` enum cast to int. Decoders do not apply; use `MotelyTag[value]`. | `(int)MotelyTag` |
| `Voucher` | Vouchers — raw `MotelyVoucher` enum cast to int. Uses an empty run state, so odd-indexed (prerequisite-required) vouchers are skipped by the engine. Use `MotelyVoucher[value]`. | `(int)MotelyVoucher` |

## Packed-int decoders

Stream cursors return packed integers. Bit fields are extracted with the decode helpers
on `Motely`:

```js
import { Motely } from "motely-wasm";
import { MotelyStreamKind } from "motely-wasm/motely";
import {
    MotelyItemType, MotelyItemTypeCategory, MotelyJokerRarity,
    MotelyItemEdition, MotelyItemSeal, MotelyItemEnhancement,
} from "motely-wasm/motely/enums";

const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Joker);
const v = cursor.getNext();

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
`MotelyVoucher`, `MotelyBoosterPack`, `MotelyDeck`, `MotelyStake`) live at
`motely-wasm/motely/enums` and can be used for reverse-lookup by numeric value or
forward-lookup by name. `MotelyStreamKind` lives at `motely-wasm/motely`.

## Events

Callbacks are registered on `Motely` once per `bootsharp.boot()` — not on each settings
chain. Every search started from that WASM load shares the same handlers; run one search at
a time or use separate worker boots if you need isolated callbacks.

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
| `motely-wasm/motely` | `IMotelySearch`, `SearchSettings`, `IMotelyStreamCursor`, `MotelyProgress`, `MotelyScoredSeedResult`, `MotelyStreamKind` |
| `motely-wasm/motely/enums` | All Balatro enums — `MotelyItemType`, `MotelyItemTypeCategory`, `MotelyJokerRarity`, `MotelyItemEdition`, `MotelyItemSeal`, `MotelyItemEnhancement`, `MotelyTag`, `MotelyVoucher`, `MotelyBoosterPack`, `MotelyDeck`, `MotelyStake`, `MotelyBossBlind`, etc. |
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
