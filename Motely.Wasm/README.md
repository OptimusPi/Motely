# motely-wasm

`motely-wasm` is the Bootsharp/WebAssembly package for MotelyJAML: the production Balatro seed-search engine, JAML loader, JAMLyzer analyzer, JUMMY one-line parser, and selected seed utilities exposed to JavaScript.

JAML is Jimbo's Ante Markup Language. YAML and JSON are the concrete syntaxes; both load to the same typed `JamlConfig` that the engine executes.

## Install

```sh
npm install motely-wasm
```

## Boot

Bootsharp exports a default runtime object plus named API namespaces. Subscribe to exported events and bind imports before `boot()`.

```js
import bootsharp, {
  MotelyJaml,
  MotelyJamlyzer,
  MotelySearch,
  Jimmolate,
} from "motely-wasm";

MotelySearch.onProgress.subscribe((p) => {
  console.log(`searched=${p.seedsSearched} matches=${p.matchingSeeds}`);
});

MotelySearch.onSeedMatch.subscribe((seed) => console.log(seed));
MotelySearch.onScoredResult.subscribe((result) => console.log(result.seed, result.score));

Jimmolate.filter = (inst) => true;

await bootsharp.boot();
```

## Parse and validate JAML

Use `fromYaml` or `fromJson` once, then pass the returned `JamlConfig` to analyzer/search calls. Invalid filters fail loudly; unknown keys are not silently ignored.

```js
const jaml = MotelyJaml.fromYaml(`
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

Events stream alongside for live UIs: `onProgress` ticks while the search runs,
`onSeedMatch` delivers each bare seed as it's found, `onScoredResult` delivers each typed
result incrementally.

Available modes:

```js
const fromList = await MotelySearch.searchList(jaml);
const fromRandom = await MotelySearch.searchRandom(jaml, 1000);
const fromWalk = await MotelySearch.searchSequential(jaml, 0n, 1n, 1);
```

`searchSequential` uses bigint batch indices because the C# parameters are `long`.

## Jimmolate

Jimmolate is a JS-authored seed filter in the engine filter chain, speaking the real Immolate `.cl` contract: `filter(inst) => score` — a number, and the host sets the bar with a score cutoff. Returning `true`/`false` also works for convenience; booleans coerce to 1/0. The filter receives the live `MotelySingleSearchContext` as an interop instance, so it can drive every query a native C# filter can. Bind it before `boot()`; keep-all (`(inst) => 1`) is the neutral binding.

The ante-1 fingerprint filter from the C# unit tests, authored in JS — real voucher and boss queries pulling one needle out of the decoys:

```js
Jimmolate.filter = (inst) => {
  if (inst.getAnteFirstVoucher(1) !== MotelyVoucher.MagicTrick) return 0;
  const result = inst.getBossForAnteWithState(1, inst.newRunState());
  return result.boss === MotelyBossBlind.TheWindow ? 1 : 0;
};
await MotelySearch.searchList(jaml);
```

A JAML with zero must/should/mustNot clauses (deck, stake, seeds only — the real Immolate shape) is a first-class search: the Jimmolate filter carries the whole decision.

Stream walkers that thread `ref` state are C#-only shapes; their state-threaded twins (value in, value out) are the JS-facing equivalents.

## JUMMY

JUMMY is one human line per JAML criterion — the terse spelling of a JAML clause, so it lives on `MotelyJaml`. Both calls delegate to the engine's `JummyLine` parser/formatter so packed item identity stays canonical.

```js
MotelyJaml.validateLine("Eternal Blueprint in antes 1 or 2"); // null
MotelyJaml.canonicalizeLine("Showman in antes 1, 2");         // "Showman in antes 1 or 2"
```

## Vocabulary

`MotelyJaml.listItems(kind, query)` serves the real engine vocabulary — jokers, vouchers, tags, bosses, and the rest — for autocomplete and agent grounding. Names come straight from the engine enums, so nothing hand-maintained can drift.

```js
MotelyJaml.listItems("joker", "lucky"); // ["LuckyCat", ...] — case-insensitive substring match
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
npm test        # publishes the Release build, then runs the Node suite against dist/index.mjs
npm run test:ui # Playwright drives the real test UI in Chromium against the same artifact
npm run serve   # hand-drive the test UI at http://127.0.0.1:4173/
npm run pack:check
```

The test UI (`testui/index.html`) is a plain ES-module page — boot, validate, search, results
table — and the Playwright specs in `tests-ui/` prove the package where UX lives: a real browser.

The release script at the repository root syncs `package.json` to `<MotelyVersion>` in `Directory.Packages.props`, publishes the WASM build, runs the JS tests against that artifact, and then calls `npm publish`.

## Current coverage focus

The package test suite mirrors the C# behavior tests that are meaningful through the public WASM surface:

- boot/runtime and version export
- JAML YAML/JSON parse and validation strictness
- JAMLyzer ante structure, event windows, score-by-analysis, and stream-state resume
- real list/random/sequential searches
- Jimmolate accept/reject predicate behavior against the live context
- AND scoring, default source fallback, Hieroglyph pack-slot reachability, and luck-source regressions
- JUMMY canonicalization
- seed math and keyword utility parity

Corpus-file loading and live ref-struct seed-router introspection remain native test concerns rather than JavaScript package behavior.
