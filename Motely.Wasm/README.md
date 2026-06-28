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

MotelySearch.onSeedMatch.subscribe((seedOrRow) => console.log(seedOrRow));
MotelySearch.onScoredResult.subscribe((result) => console.log(result.seed, result.score));

Jimmolate.findSeed = (seed, deck, stake) => true;

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

Searches emit results through events. The returned promise only signals completion.

```js
const found = [];
const onResult = (r) => found.push(r.seed);
MotelySearch.onScoredResult.subscribe(onResult);
try {
  await MotelySearch.searchList(jaml);
} finally {
  MotelySearch.onScoredResult.unsubscribe(onResult);
}
```

Available modes:

```js
await MotelySearch.searchList(jaml);
await MotelySearch.searchRandom(jaml, 1000);
await MotelySearch.searchSequential(jaml, 0n, 1n, 1);
```

`searchSequential` uses bigint batch indices because the C# parameters are `long`.

## Jimmolate

Jimmolate is an optional JS-authored seed predicate in the engine filter chain. The browser-safe WASM surface receives `seed`, `deck`, and `stake`; live `MotelySingleSearchContext` ref-struct streams remain engine-side and are not marshalled to JavaScript.

```js
Jimmolate.findSeed = (seed, deck, stake) => seed === "UNITTEST";
Jimmolate.enabled = true;
try {
  await MotelySearch.searchList(jaml);
} finally {
  Jimmolate.enabled = false;
}
```

## JUMMY

JUMMY is one human line per JAML criterion. The WASM wrapper delegates to the engine's `JummyLine` parser/formatter so packed item identity stays canonical.

```js
MotelyJummy.validate("Eternal Blueprint in antes 1 or 2"); // null
MotelyJummy.canonicalize("Showman in antes 1, 2");        // "Showman in antes 1 or 2"
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
npm test
npm run pack:check
```

`npm test` runs `dotnet publish Motely.Wasm.csproj -c Release` first, then runs the Node test suite against `dist/index.mjs`.

The release script at the repository root syncs `package.json` to `<MotelyVersion>` in `Directory.Packages.props`, publishes the WASM build, runs the JS tests against that artifact, and then calls `npm publish`.

## Current coverage focus

The package test suite mirrors the C# behavior tests that are meaningful through the public WASM surface:

- boot/runtime and version export
- JAML YAML/JSON parse and validation strictness
- JAMLyzer ante structure, event windows, score-by-analysis, and stream-state resume
- real list/random/sequential searches
- Jimmolate accept/reject/predicate/deck-stake behavior
- AND scoring, default source fallback, Hieroglyph pack-slot reachability, and luck-source regressions
- JUMMY canonicalization
- seed math and keyword utility parity

Corpus-file loading and live ref-struct seed-router introspection remain native test concerns rather than JavaScript package behavior.
