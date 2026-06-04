# motely-wasm

WebAssembly build of [MotelyJAML](https://github.com/OptimusPi/MotelyJAML) — the SIMD Balatro seed search engine with JAML filter support. Powers [seedfinder.app](https://seedfinder.app).

## Install

```sh
npm install motely-wasm
```

## Boot

The WASM binary is sideloaded (separate file, not base64-embedded). In Node, read the bytes and pass them directly — `fetch` can't read `file://` URLs:

```js
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import bootsharp from "motely-wasm";

const dist = resolve(dirname(fileURLToPath(import.meta.url)), "node_modules/motely-wasm/dist");
const wasm = await readFile(resolve(dist, "bin", bootsharp.manifest.wasm));
await bootsharp.boot({ wasm });
```

In the browser, pass a root URL instead:

```js
await bootsharp.boot("/motely-wasm/dist");
```

## Imports

```js
// Main API — the C# Program class, renamed to Motely
import { Program as Motely } from "motely-wasm/dist/generated/modules/motely/wasm.g.mjs";

// Enums — MotelyDeck, MotelyStake, etc.
import * as enums from "motely-wasm/dist/generated/modules/motely/enums.g.mjs";

// Types — MotelyProgress, IMotelySearch, MotelySingleSearchContext, etc.
import * as types from "motely-wasm/dist/generated/modules/motely.g.mjs";
```

## Quick start

```js
// Wire required imports before boot
Motely.reportWasmError = (msg) => console.error("[WASM]", msg);
Motely.jimmolateProbe = () => false;

// Subscribe to events
Motely.onSeedMatch.subscribe(seed => console.log("match:", seed));
Motely.onProgress.subscribe(p => console.log(`${p.percentComplete.toFixed(1)}%`));

// Parse a JAML filter
const config = Motely.parseJaml(`
name: WeeMonday
deck: Erratic
stake: Black
must:
  - joker: WeeJoker
    antes: [1]
`);

// Run a search (blocks until complete — run in a Worker for non-blocking UI)
const search = Motely.runSequentialSearch(config);
console.log(search.matchingSeeds, "matches out of", search.totalSeedsSearched);
```

## API

```js
Motely.parseJaml(jaml)                    // string → JamlConfig (throws on invalid JAML)
Motely.explainJaml(config)                // human-readable plan summary
Motely.createPlan(config)                 // JamlSearchPlan (tally columns, CSV header)
Motely.jamlToJson(jaml)                   // JAML string → JSON string
Motely.jsonToJaml(json)                   // JSON string → JAML string
Motely.jamlyzer(config)                   // analyze seeds in config.seeds per-ante
Motely.seedContext(seed, deck, stake)     // MotelySingleSearchContext for one seed
Motely.nativeFilterNames()                // built-in native filter names

Motely.runSequentialSearch(config, ...)   // sequential seed search
Motely.runRandomSearch(config, count)     // random seed sample
Motely.runSeedListSearch(config)          // search only config.seeds
Motely.runAestheticSearch(config, ...)    // aesthetic/scored search
Motely.runNativeListSearch(name, seeds)   // named native filter over a seed list
Motely.runPassthroughListSearch(seeds)    // no filter, just iterate seeds
```

## Events

```js
Motely.onSeedMatch.subscribe(seed => {})           // string — each matching seed
Motely.onScoredResult.subscribe(result => {})      // { seed, score, tallies }
Motely.onProgress.subscribe(p => {})               // MotelyProgress
Motely.onFileChanges.subscribe(changes => {})      // file system changes (browser OPFS)
```

## File system (browser)

`Motely.pickRoot`, `mountRoot`, `unmountRoot`, `readTextFile`, `writeTextFile` use the browser File System Access API via `Bootsharp.FileSystem`. Initialize the JS extension before booting:

```js
import * as fs from "@rewaffle/bootsharp-file-system";
import { Bootsharp } from "motely-wasm/dist/generated/modules/bootsharp/file-system.g.mjs";

fs.init(Bootsharp.FileSystem.FileMounter);
await bootsharp.boot("/motely-wasm/dist");
```

## Jimmolate

To enable scalar predicate filtering after the base SIMD pass:

```js
Motely.jimmolateProbe = (ctx) => {
    // ctx is MotelySingleSearchContext — inspect the seed however you like
    return true; // return true to keep the seed
};
```
