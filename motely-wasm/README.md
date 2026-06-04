# motely-wasm

Balatro seed finder and per-seed analyzer, compiled to WebAssembly via [Bootsharp](https://bootsharp.com). Runs in browsers and Node/Deno/Bun.

## Install

```sh
npm install motely-wasm
```

## Module layout

The root barrel (`motely-wasm`) re-exports only the Bootsharp runtime (`boot`, `getStatus`, `manifest`, …). The Motely API and enums live in generated submodules and **must be imported directly** via the package's subpath exports:

```js
import bootsharp from "motely-wasm";
import { Program as Motely } from "motely-wasm/motely/wasm";
import * as enums from "motely-wasm/motely/enums";
```

Additional submodules: `motely-wasm/motely/analysis`, `motely-wasm/motely/filters/jaml`, `motely-wasm/motely/filters`.

## Boot

Binaries are sideloaded (published to `dist/bin/` as separate files). Pass the wasm bytes directly — `boot()` accepts either raw bytes or a URL root.

### Node

```js
import { readFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import bootsharp from "motely-wasm";
import { Program as Motely } from "motely-wasm/motely/wasm";
import * as enums from "motely-wasm/motely/enums";

// [Import] callbacks must be assigned BEFORE boot.
Motely.reportWasmError = (message) => console.error("[WASM ERROR]", message);
Motely.jimmolateProbe = () => false; // required even when not using Jimmolate

// import.meta.resolve("motely-wasm") → <pkg>/dist/index.mjs; dirname is already <pkg>/dist.
const distDir = dirname(fileURLToPath(import.meta.resolve("motely-wasm")));
const wasm = await readFile(resolve(distDir, "bin", bootsharp.manifest.wasm));
await bootsharp.boot({ wasm });
```

### Browser

```js
import bootsharp from "motely-wasm";
import { Program as Motely } from "motely-wasm/motely/wasm";

Motely.reportWasmError = (message) => console.error("[WASM ERROR]", message);
Motely.jimmolateProbe = () => false;

// Pass the URL root where dist/bin/ is served.
await bootsharp.boot("/assets/motely-wasm/dist/bin");
```

## Core API

All methods are on the `Motely` (i.e. `Program`) namespace.

### Parse a JAML filter

```js
const cfg = Motely.parseJaml(`
name: My Filter
deck: Red
stake: White
must:
  - joker: Triboulet
    antes: [1, 2, 3]
`);
```

`parseJaml` throws on invalid YAML. Use `jamlToJson` / `jsonToJaml` to convert between representations. Use `explainJaml` to get a human-readable summary of a parsed config.

### Search

All `run*Search` methods return an `IMotelySearch` — call `.start()` then listen to events, or call `.runSearchUntilCompletion()` / `await .runSearchAsync()` for synchronous / async use.

```js
// Subscribe to results before starting.
Motely.onSeedMatch.subscribe((seed) => console.log("match:", seed));
Motely.onProgress.subscribe((p) => console.log(`${p.percentComplete.toFixed(1)}%`));

// Sequential: scan all seeds in batch range [0, 1).
const search = Motely.runSequentialSearch(cfg, 0n, 1n);
await search.runSearchAsync();

// List: check specific seeds.
cfg.seeds = ["PIFREAK1", "DEADBEEF"];
const listSearch = Motely.runSeedListSearch(cfg);
listSearch.runSearchUntilCompletion();
console.log(listSearch.matchingSeeds); // bigint

// Scored: emits onScoredResult instead of onSeedMatch.
Motely.onScoredResult.subscribe((r) => console.log(r.seed, r.score));
const scored = Motely.runSequentialSearch(cfg, 0n, 1n);
await scored.runSearchAsync();
```

### Analyze a seed

`jamlyzer` runs the JAMLyzer on all seeds in `cfg.seeds` and returns per-ante detail: boss blind, voucher, tags, shop queue, booster packs.

```js
cfg.seeds = ["PIFREAK1"];
const result = Motely.jamlyzer(cfg);
for (const { seed, analysis } of result.seeds) {
    for (const ante of analysis.antes) {
        console.log(`ante ${ante.ante} boss: ${enums.MotelyBossBlind[ante.boss]}`);
        for (const { item, matched } of ante.shopQueue) {
            console.log("  shop item", item.value, matched ? "(matched)" : "");
        }
    }
}
```

### Seed context (low-level)

For custom logic, get a `MotelySingleSearchContext` and query PRNG streams directly:

```js
const ctx = Motely.seedContext("PIFREAK1", enums.MotelyDeck.Red, enums.MotelyStake.White);
const voucher = ctx.getAnteFirstVoucher(1);
console.log("ante 1 voucher:", enums.MotelyVoucher[voucher]);
```

## Jimmolate

Jimmolate is a custom JS scalar predicate that runs on every seed that passes the base JAML filter. Wire it up and call `enableJimmolate()` after boot:

```js
// Must be set BEFORE boot.
Motely.jimmolateProbe = (ctx) => {
    // ctx is MotelySingleSearchContext — same API as seedContext().
    return ctx.getSeed().startsWith("PI");
};

await bootsharp.boot({ wasm });
Motely.enableJimmolate();

cfg.seeds = ["PIFREAK1", "XYZABCDE"];
Motely.onSeedMatch.subscribe((s) => console.log("jimmolate match:", s));
Motely.runPassthroughListSearch(cfg.seeds).runSearchUntilCompletion();
```

Use `runPassthroughListSearch` to skip the JAML filter entirely and let Jimmolate do all the culling.

## JAML quick reference

```yaml
name: string          # optional display name
deck: Red             # MotelyDeck value (case-insensitive)
stake: White          # MotelyStake value
seeds: []             # pre-populate for list searches

must:                 # all clauses must match
  - joker: Triboulet
    antes: [1, 2]     # ante numbers (1–8)
    min: 1            # default 1
    max: 2            # optional upper bound

should:               # scoring; doesn't filter, raises score
  - voucher: Telescope
    antes: [1, 2]
    score: 10

mustNot:              # any match rejects the seed
  - boss: TheHook
    antes: [1]
```

Clause types: `joker`, `voucher`, `boss`, `tag`, `spectral`, `tarot`, `planet`, `pack`.  
Sources can be narrowed via `sources: { shopItems: [0, 1], boosterPacks: [0] }`.

## Enums

All enum values are available in the `enums` module:

- `MotelyDeck` — Red, Blue, Yellow, Green, Black, Magic, Nebula, Ghost, Abandoned, Checkered, Zodiac, Painted, Anaglyph, Plasma, Erratic
- `MotelyStake` — White, Red, Green, Black, Blue, Purple, Orange, Gold
- `MotelyBossBlind`, `MotelyVoucher`, `MotelyTag`, `MotelyBoosterPack`

## Utility

```js
// Convert between JAML text and JSON.
const json = Motely.jamlToJson(jamlText);
const jaml = Motely.jsonToJaml(json);

// Human-readable description of a parsed config.
const explanation = Motely.explainJaml(cfg);

// List available native (non-JAML) filter names.
const names = Motely.nativeFilterNames();
```
