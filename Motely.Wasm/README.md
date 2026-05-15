# motely-wasm

WebAssembly package for [Motely](https://github.com/OptimusPi/MotelyJAML) — the Balatro seed search engine, with filters written in JAML.

## Install

```sh
npm install motely-wasm
```

## Quick start

```js
import bootsharp, { Motely } from "motely-wasm";

// Boot the .NET WASM runtime. The argument is the URL path under which your
// host serves the package's `bin/` directory (where dotnet.native.wasm lives).
// Pick whichever URL is actually reachable from your page — examples:
//   "/motely-wasm/bin"          (Vite/Storybook staticDirs, Next.js route)
//   "/bin"                       (page served from the package root)
//   "/node_modules/motely-wasm/bin" (raw node_modules served as static)
//   "https://unpkg.com/motely-wasm@17.4.4/bin" (CDN)
// Workers must use the SAME path — don't pass "/bin" if your host serves
// the assets at "/motely-wasm/bin".
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

`bootsharp.boot(binUrl)` initializes the .NET WASM runtime. Call it once before any
`Motely.*` API. The argument is the URL path your host serves the `bin/` directory
at (where `dotnet.native.wasm` lives). It's host-chosen — typical mountings are:

| Host | Mount `bin/` at | Boot call |
| --- | --- | --- |
| Vite / Storybook `staticDirs` | `/motely-wasm/bin` | `boot("/motely-wasm/bin")` |
| Next.js route handler | `/motely-wasm/bin/[...path]` | `boot("/motely-wasm/bin")` |
| Static page in `node_modules/motely-wasm/` | `/bin` | `boot("/bin")` |
| Raw node_modules served as static | `/node_modules/motely-wasm/bin` | `boot("/node_modules/motely-wasm/bin")` |
| Public CDN | `https://unpkg.com/motely-wasm@17.4.4/bin` | `boot("https://unpkg.com/motely-wasm@17.4.4/bin")` |

Web Workers must boot from the **same** URL as the main thread — absolute paths
in a worker resolve against the page origin, so the path that serves the WASM
to the main thread is the same path the worker must pass.

```js
import bootsharp, { BootStatus } from "motely-wasm";

if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
  await bootsharp.boot("/motely-wasm/bin");
}

console.log(bootsharp.getStatus()); // BootStatus.Booted
```

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

## Events

| Event | Payload |
|---|---|
| `Motely.onSeedMatch` | `string` — matching seed |
| `Motely.onScoredResult` | `{ seed, score, tallies }` |
| `Motely.onProgress` | `MotelyProgress` — `percentComplete`, `seedsSearched`, `matchingSeeds`, `seedsPerMillisecond`, `elapsedMilliseconds` |
| `Motely.onFileChanges` | `Change[]` (browser file-system mounts) |

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
