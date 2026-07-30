# motely-wasm

`motely-wasm` is the Bootsharp/WebAssembly package for MotelyJAML: the production Balatro seed-search engine, JAML loader, JAMLyzer, one-line JAML parser, and selected seed utilities exposed to JavaScript.

JAML is Jimbo's Ante Markup Language. The C# engine owns the grammar: load a filter as JAML text and it becomes a typed `JamlConfig` the search runs. Clauses can be one-line natural language or structured documents — both are JAML.

## Install

```sh
npm install motely-wasm
```

## Boot

Bootsharp exports a default runtime object plus named API namespaces. Subscribe to exported events and bind imports before `boot()`.

This package ships embedded: the boot resources travel inside the module, so `boot()` takes no argument. `getStatus()` reports where the runtime is — boot when it returns `BootStatus.Standby`, and it's ready to use at `BootStatus.Booted`.

```js
import bootsharp, {
  MotelyJaml,
  MotelyJamlyzer,
  MotelySearch,
} from "motely-wasm";

MotelySearch.onProgress.subscribe((p) => {
  console.log(`searched=${p.seedsSearched} matches=${p.matchingSeeds}`);
});

MotelySearch.onSeedMatch.subscribe((seed) => console.log(seed));
MotelySearch.onScoredResult.subscribe((result) => console.log(result.seed, result.score));

await bootsharp.boot();
```

## Parse and validate JAML

Use `fromJaml` once, then pass the returned `JamlConfig` to analyzer/search calls. Invalid filters fail loudly, and unknown keys raise errors too, so every mistake surfaces at parse time.

```js
const jaml = MotelyJaml.fromJaml(`
name: example
deck: Red
stake: White
seeds: [AAAAAAAA, BBBBBBBB]
must:
  - voucher: Overstock
    antes: [1]
`);

const error = MotelyJaml.validate("must: [");
if (error) console.error(error);
```

## Analyze seeds with JAMLyzer

```js
const results = MotelyJamlyzer.analyzeSeeds(jaml);
for (const result of results) {
  console.log(result.seed, result.antes[0].voucher, result.antes[0].packs.length);
}
```

Scrollable analysis uses the `streamStates` object returned by a previous page. The state is seed-specific, so resume with a single-seed `JamlConfig`.

```js
const first = MotelyJamlyzer.analyzeSeedsPaged(jaml, 10)[0];
const next = MotelyJamlyzer.resumeSeeds(jaml, first.streamStates, 10)[0];
```

## Search

Call it, await it, use it — the promise resolves with the scored results:

```js
const results = await MotelySearch.searchList(jaml);
for (const r of results) console.log(r.seed, r.score, r.tallies);
```

Each result is a `MotelyScoredSeedResult`: `seed` (string), `score` (number), and `tallies` — the raw per-clause hit counts, one per should-clause in JAML order. Tallies cross the boundary as an `Int32Array`; call `Array.from(r.tallies)` when a plain array matters.

Events stream alongside for live UIs: `onProgress` ticks while the search runs,
`onSeedMatch` delivers each bare seed as it's found, `onScoredResult` delivers each typed
result incrementally.

Available modes (same shape as CLI):

```js
const fromList = await MotelySearch.searchList(jaml);
const fromRandom = await MotelySearch.searchRandom(jaml, 1000);
const fromWalk = await MotelySearch.searchSequential(jaml, 0n, 1n, 1);

// CLI --collect N: aesthetics first, then sequential for the remainder
const batch = await MotelySearch.collect(jaml, 100n);
const first = await MotelySearch.findOne(jaml); // collect(jaml, 1n)

// CLI --collect N + --startBatch/--endBatch: sequential range only
const slice = await MotelySearch.collectSequential(jaml, 5n, 0n, 1n, 1);
```

`stopAfter` and batch indices are JS BigInt (C# `long`).

## The fleet — one engine per web worker

A batch index is a seed prefix. `batchChars` names how many trailing characters a single batch enumerates, so one batch covers `35 ** batchChars` seeds and the space holds `35 ** (8 - batchChars)` batches. Batch `0` is the block of seeds beginning `1111…`, batch `1` the next block, and so on up through `GGGG…`. Handing a worker a range of batch indices is the same as handing it a range of seed prefixes.

Give each worker its own contiguous block and the fleet needs no coordination at all:

```js
const totalBatches = 35 ** (8 - batchChars);
const blockSize = Math.floor(totalBatches / fleetSize);

// worker w owns [w * blockSize, (w + 1) * blockSize) — the last worker takes the remainder
const startBatch = w * blockSize;
const endBatch = w === fleetSize - 1 ? totalBatches : (w + 1) * blockSize;
await MotelySearch.searchSequential(config, BigInt(startBatch), BigInt(endBatch), batchChars);
```

Blocks are disjoint by construction, so two workers never report the same seed and the fleet shares nothing: no queue, no locks, no mid-run coordination. A worker computes its own range from its own index, so it never needs to know the fleet size or agree with anyone. Losing a worker simply leaves its block unfinished, and every other block is untouched.

Search the seed space for however long the user cares to wait. The full 35⁸ ≈ 2.25 trillion seeds take days; real searches sample, then stop. Blocks make that sampling addressable — you can say which prefixes you covered, resume from a saved cursor, and hand block `w` to a second browser or a server just as easily as to a second worker.

Report progress per worker. Each worker owns its own counts, and summing them for a headline number is the consumer's job — a fleet has no single global tick to subscribe to.

Two environment rules keep a worker from hanging silently:

- **Use a module worker.** `new Worker(url, { type: "module" })`. Chromium forbids dynamic `import()` inside a classic worker, and import maps don't reach workers, so pass `motely-wasm`'s absolute URL and `import()` it inside.
- **Release `self.onmessage` before `boot()`.** `dotnet.js` instantiates assets over the worker's global message channel and never resolves while an app handler holds it ([dotnet/runtime#114918](https://github.com/dotnet/runtime/issues/114918)). Take the init message once, null the handler, and run all further traffic over a transferred `MessagePort`.

Stop a worker with `worker.terminate()` — it ends the batch mid-loop, immediately.

```js
// worker.js
self.onmessage = (e) => {
  self.onmessage = null; // free the global channel for dotnet.js
  const { port, motelyUrl, jaml, startBatch, endBatch, batchChars } = e.data;
  (async () => {
    const engine = await import(motelyUrl);
    if (engine.default.getStatus() === engine.default.BootStatus.Standby)
      await engine.default.boot();

    const config = engine.MotelyJaml.fromJaml(jaml);
    const found = [];
    const onScored = (r) => found.push({ seed: r.seed, score: r.score });
    engine.MotelySearch.onScoredResult.subscribe(onScored);
    await engine.MotelySearch.searchSequential(config, BigInt(startBatch), BigInt(endBatch), batchChars);
    port.postMessage({ type: "finished", found });
  })();
};
```

Boot each worker's engine once and reuse it: booting is the expensive part, searching is not.

## One-line JAML

A JAML clause can be written as one human line — the terse spelling of a clause, so it lives on `MotelyJaml`. Both calls delegate to the engine's `JamlLine` parser/formatter so packed item identity stays canonical.

```js
MotelyJaml.validateLine("Eternal Blueprint in antes 1 or 2"); // null
MotelyJaml.canonicalizeLine("Showman in antes 1, 2");         // "Showman in antes 1 or 2"
```

## Enum lists (`listItems`)

`MotelyJaml.listItems(kind, query)` is a thin export of generated `JamlSchema.ListItems`. Kind is a discriminator wire (`joker`, `tarotCard`, …) or an enum-typed property key (`edition`, `deck`, …). Short nicknames like `tarot` / `planet` are not kinds.

```js
MotelyJaml.listItems("joker", "lucky"); // ["LuckyCat", ...] — case-insensitive substring match
MotelyJaml.listItems("planetCard");     // canonical wire, not "planet"
```

## Utilities

`MotelyUtilities` exposes seed math and keyword sequence helpers used by the CLI/provider modes.

```js
MotelyUtilities.seedToTotalIndex("11111111");      // 66231629136n
MotelyUtilities.totalIndexToSeed(66231629136n);    // "11111111"
MotelyUtilities.searchIndexToSeed(0n, 8);          // "11111111"
MotelyUtilities.repeatCharKeywords(3)[0];          // "AAA"
```

## Local development

From `Motely.Wasm/`:

```sh
npm test        # pretest = Debug publish, then Node suite against dist/index.mjs
npm run test:ui # Playwright: Chromium + testui (Search / Mount / Save / Load)
npm run serve   # hand-drive the test UI at http://127.0.0.1:4173/
npm run pack:check
```

The test UI (`testui/index.html`) is a plain ES-module page with CodeMirror 6: live engine lint and completion, a results table, and buttons for list search plus optional folder mount/save/load when the FS-enabled build is present. Playwright specs in `tests-ui/` pin known default-doc seeds (`AAAAAAAA`, `BBBBBBBB`) in a real browser.

Releasing, from `Motely.Wasm/`: sync `"version"` in `package.json` to `<MotelyVersion>` in the repo-root `Directory.Packages.props`, run `npm test` and `npm run test:ui` green, then `npm publish`.

## Current coverage focus

The package test suite mirrors the C# behavior tests that are meaningful through the public WASM surface:

- boot/runtime and version export
- JAML parse and validation strictness
- JAMLyzer ante structure, event windows, score-by-analysis, and stream-state resume
- real list/random/sequential searches that pin known seeds (or random: walk count + search-index roundtrip)
- AND scoring, default source fallback, Hieroglyph pack-slot reachability, and luck-source regressions
- One-line JAML canonicalization
- seed math and keyword utility parity

Corpus-file loading and live ref-struct seed-router introspection remain native test concerns rather than JavaScript package behavior.
