# Motely/JAML MCP App Brain Dump for v0 Agent

This is a handoff for building/updating the Seedfinder MCP App around MotelyJAML, `motely-wasm`, JAML, JUMMY, and Balatro synergy/search UX.

## Ground truth

- MotelyJAML is the engine. JAML filters are executable user intent, not prompts.
- JAML is Jimbo's Ante Markup Language. YAML and JSON are surface syntaxes that load to the same typed `JamlConfig`.
- Do not silently drop unknown keys. Invalid filters should fail loudly with the actual loader/build error.
- Ordinary joker paths and legendary/Soul paths are different PRNG/source paths. Do not flatten `legendaryJoker` into `joker`.
- Clause `sources:` overrides defaults wholesale. Missing `sources:` means the FilterDesc-owned defaults apply.
- `boosterPacks: [...]` is a core source shorthand where the source config has booster pack slots.
- `requireMega` / `requireMegaPack` means count only source matches whose referenced booster pack is Mega-sized.
- `smallBlindTag` and `bigBlindTag` are aliases for tag roll/source `[0]` and `[1]`.
- JUMMY is one human line = one JAML criterion. It must preserve canonical packed item identity through the engine formatter/parser.

## Current local Phase 1 state

`Motely.Wasm/` was restored from git history into the current branch and updated for Motely 23.

Package files now present:

- `Motely.Wasm/Motely.Wasm.csproj`
- `Motely.Wasm/Program.cs`
- `Motely.Wasm/package.json`
- `Motely.Wasm/README.md`
- `Motely.Wasm/LICENSE`
- `Motely.Wasm/tests/*.test.mjs`

Local package version: `23.0.0`, matching `<MotelyVersion>23.0.0</MotelyVersion>`.

Verification already run:

```powershell
cd D:\MotelyJAML\Motely.Wasm
npm test
npm run pack:check
```

Results:

- `npm test`: 45 passing JS/WASM tests.
- `npm run pack:check`: tarball includes `dist`, `README.md`, `LICENSE`, and `package.json`; package is about 2.1 MB packed / 6.5 MB unpacked.
- Focused native parity tests:
  - 40 passing non-benchmark tests across JAML scoring/loading/defaults/Hieroglyph/luck/Jimmolate/utilities.
  - 12 passing focused Analyzer tests excluding the benchmark-style sweep.
- Broad `dotnet test` hung in this session because the broad Analyzer filter included the benchmark-style scroll sweep; after stopping the leftover `testhost`, focused filters passed.

## `motely-wasm@23` public JS surface

Import shape from Bootsharp:

```js
import bootsharp, {
  MotelyWasm,
  MotelyJaml,
  MotelyJummy,
  MotelyUtilities,
  MotelyJamlyzer,
  MotelySearch,
  Jimmolate,
  MotelyDeck,
  MotelyStake,
} from "motely-wasm";
```

Boot first, but bind imports and event subscriptions before boot:

```js
MotelySearch.onProgress.subscribe((p) => console.log(p.seedsSearched));
MotelySearch.onSeedMatch.subscribe((row) => console.log(row));
MotelySearch.onScoredResult.subscribe((r) => console.log(r.seed, r.score));
Jimmolate.findSeed = (seed, deck, stake) => true;
await bootsharp.boot();
```

JAML:

```js
const config = MotelyJaml.fromYaml(jamlText);
const jsonConfig = MotelyJaml.fromJson(jsonText);
const error = MotelyJaml.validate(jamlOrJsonText); // null when valid
const plan = MotelyJaml.createPlan(config);
const names = MotelyJaml.nativeFilterNames();
```

JAMLyzer:

```js
const results = MotelyJamlyzer.analyzeSeeds(config);
const page1 = MotelyJamlyzer.analyzeSeedsPaged(singleSeedConfig, 10)[0];
const page2 = MotelyJamlyzer.resumeSeeds(singleSeedConfig, page1.streamStates, 10)[0];
```

Search:

```js
await MotelySearch.searchList(config);
await MotelySearch.searchRandom(config, 1000);
await MotelySearch.searchSequential(config, 0n, 1n, 1);
```

Important: search results come through events; the promise just signals completion. Longs are JS `bigint`.

Jimmolate:

```js
Jimmolate.findSeed = (seed, deck, stake) => seed === "UNITTEST";
Jimmolate.enabled = true;
try { await MotelySearch.searchList(config); }
finally { Jimmolate.enabled = false; }
```

WASM Jimmolate intentionally receives only `seed`, `deck`, and `stake`. The live `MotelySingleSearchContext` is a ref-struct/native concern and should not be marshalled into JS.

JUMMY:

```js
MotelyJummy.validate("Eternal Blueprint in antes 1 or 2"); // null
MotelyJummy.canonicalize("Showman in antes 1, 2");        // "Showman in antes 1 or 2"
```

Utilities:

```js
MotelyUtilities.seedToTotalIndex("11111111");   // 66231629136n
MotelyUtilities.totalIndexToSeed(66231629136n); // "11111111"
MotelyUtilities.searchIndexToSeed(0n, 8);       // "11111111"
MotelyUtilities.repeatCharKeywords(3);          // ["AAA", ..., "ZZZ"]
```

## Bootsharp / NativeAOT-LLVM recommendations

Source read: `D:/bootsharp/docs/guide/llvm.md`, `D:/bootsharp/docs/guide/build-config.md`, and samples.

- Bootsharp 0.8+ automatically enables NativeAOT-LLVM for `Release` publishes targeting `browser-wasm`.
- The current `Motely.Wasm.csproj` is acceptable and already produced a Release publish with LLVM logs:
  - `Generating native code`
  - `LLVM compilation to IR finished...`
  - `Bootsharp ES module published...`
- No extra NativeAOT-LLVM MSBuild block is required in Motely.Wasm.
- Binaryen is optional but recommended for final size/perf. Install `wasm-opt` and keep it on PATH. Bootsharp will warn and continue if missing.
- Current embedded single-file style is good for npm/CDN simplicity: empty `BootsharpBinariesDirectory` means the WASM/resources are base64-inlined and `boot()` needs no served side files.
- Tradeoff: embedded resources make `dist/generated/resources.g.mjs` large (~6 MB unpacked). If app startup or browser parse time becomes a problem, consider sideloading binaries with `BootsharpBinariesDirectory`, but that complicates MCP CSP/resource serving. For MCP iframe simplicity, embedded is the right default.
- Current `Program.cs` surface is approved in principle:
  - flat `index` module via `RenameModule`
  - typed `JamlConfig` crossing JS boundary instead of repeatedly passing text
  - explicit wrappers for JUMMY/utilities rather than exporting internals
  - `RenameMember` hides un-marshallable ref-struct stream members
  - Jimmolate stays marshallable (`seed`, `deck`, `stake`)
- Do not export `MotelySingleSearchContext` or ref-struct stream APIs to JS. If the UI needs richer per-seed introspection, add narrow marshallable helper exports that run engine-side and return plain data.

## seedfinder.app state and required update

Local `D:/seedfinder.app` worktree is effectively empty/old on `main`, behind `origin/main` by 8 commits. Do not checkout/reset over it without user approval.

Remote `origin/main` currently has the real app. Important files inspected via git object reads:

- `package.json`
- `lib/mcp/app-meta.ts`
- `lib/mcp/app-html.ts`
- `lib/mcp/server.ts`
- `app/mcp/route.ts`

Current `origin/main:package.json` pins:

```json
"jaml-lang": "^0.2.0",
"jaml-ui": "^2.3.0",
"motely-wasm": "22.0.0"
```

Current npm latest observed:

- `jaml-ui@2.4.0`
- `motely-wasm@22.2.2` now, but this work prepares local `motely-wasm@23.0.0`
- `@modelcontextprotocol/ext-apps@1.7.4`
- `@modelcontextprotocol/sdk@1.29.0`

`jaml-ui@2.4.0` peer deps:

```json
{
  "motely-wasm": ">=22.0.0",
  "react": "^18.2.0 || ^19.0.0",
  "react-dom": "^18.2.0 || ^19.0.0",
  "react-icons": ">=5.0.0"
}
```

`jaml-ui@2.4.0` deps include `jaml-lang@^1.0.0`, so the app's `jaml-lang@^0.2.0` is stale too.

### Critical seedfinder breakage

`origin/main:lib/mcp/app-html.ts` still uses the old `motely-wasm@22` subpath API:

```js
import bootsharp from "motely-wasm";
import { Program as Motely } from "motely-wasm/motely/wasm";
// plus importmap entries for:
// motely-wasm/motely/wasm
// motely-wasm/motely/enums
// motely-wasm/motely/filters/jaml
// motely-wasm/bootsharp/file-system
```

That is not the `motely-wasm@23` API. v23 exposes named namespaces from package root. Update app HTML to use:

```js
import bootsharp, { MotelyJaml, MotelySearch } from "motely-wasm";
```

and remove old subpath importmap entries. The importmap should only need:

```json
"motely-wasm": "${motelyPkg}/dist/index.mjs"
```

Then replace old usage:

```js
config = Motely.fromYaml(jamlRef.current);
Motely.onScoredResult.subscribe(onScored);
const run = Motely.runSequentialSearch(config, BigInt(cursor), BigInt(end));
await run.runSearchAsync();
Motely.onScoredResult.unsubscribe(onScored);
```

with v23 usage:

```js
config = MotelyJaml.fromYaml(jamlRef.current);
MotelySearch.onScoredResult.subscribe(onScored);
MotelySearch.onProgress.subscribe(onProgress);
await MotelySearch.searchSequential(config, BigInt(cursor), BigInt(end), BATCH_CHARS);
MotelySearch.onScoredResult.unsubscribe(onScored);
MotelySearch.onProgress.unsubscribe(onProgress);
```

Caveat: v23 `MotelySearch.searchSequential` is promise-based and does not currently return a cancellable run object. Existing seedfinder stop button expects `run.cancel()` from v22. Options:

1. Short term: keep `BATCHES_PER_RANGE` small and implement stop between ranges. UI stop becomes cooperative between calls, not mid-loop.
2. Better: add a small v23 WASM cancellation surface before final release if mid-loop cancellation is essential. For example:
   - export `CancelRequested`/`RequestCancel` state in `MotelySearch`
   - wire it into `RunSearchAsync`/search settings if engine supports cancellation
   - or return a marshallable search handle object from `SearchSequentialStart` with `cancel()` and `completion` semantics.
3. If preserving existing seedfinder behavior is more important than v23 API cleanliness, keep a compatibility wrapper in `Program.cs` temporarily (`RunSequentialSearch` returning a handle). This is more surface area and should be weighed carefully.

Recommendation: do not reintroduce the entire old `Program` API. Prefer updating the app to the v23 named namespace surface and add only a narrow cancellation export if the stop button must cancel mid-loop.

### seedfinder metadata update

Bump these in `lib/mcp/app-meta.ts` when updating the app:

```ts
export const SERVER_VERSION = "9.0.0";           // or next chosen app/server version
export const APP_RESOURCE_URI = "ui://seedfinder/app-v17.html"; // bump to bust host cache
export const MOTELY_WASM_VERSION = "23.0.0";
export const JAML_UI_VERSION = "2.4.0";
```

Also update comments that currently say v16/v22.

### package.json recommendation for seedfinder

After `motely-wasm@23.0.0` is published:

```json
"jaml-lang": "^1.0.0",
"jaml-ui": "^2.4.0",
"motely-wasm": "23.0.0"
```

Use package manager install/update, do not hand-edit lockfiles.

## MCP App implementation guidance

Use the current MCP Apps SDK pattern:

- Server registers a tool and a resource.
- Tool `_meta.ui.resourceUri` points to the resource URI.
- Resource read callback returns the bundled/inline HTML and `_meta.ui.csp`.
- Always include text fallback in tool `content` for non-UI hosts.
- Register all client app handlers before `app.connect()`.
- CSP belongs on the returned resource content `_meta.ui.csp`; keep seedfinder's no-external-origin proxy approach if possible.
- If the iframe makes network requests, every target origin must be in CSP and must handle CORS.

Current seedfinder server already follows the broad shape:

- `registerAppResource(...)`
- `registerAppTool(show_seedfinder_app, ..., _meta: { ui: { resourceUri } })`
- `WebStandardStreamableHTTPServerTransport`
- resource URI versioning
- no-CDN proxy comments/importmap

Keep the hard rule in server instructions: server never runs the 2.3T search; client-side `motely-wasm` does.

## Recommended v0 agent task list

1. Wait until `motely-wasm@23.0.0` is published.
2. In `D:/seedfinder.app`, get the real app branch/worktree safely. Do not reset without user approval; current local worktree is stale/empty but remote has the app.
3. Update packages with npm:
   - `motely-wasm@23.0.0`
   - `jaml-ui@2.4.0`
   - `jaml-lang@^1.0.0`
4. Update `lib/mcp/app-meta.ts` versions and bump `APP_RESOURCE_URI`.
5. Update `lib/mcp/app-html.ts` importmap and imports to v23 root API.
6. Replace `Motely.fromYaml` with `MotelyJaml.fromYaml`.
7. Replace old `Motely.runSequentialSearch(...).runSearchAsync()` with `MotelySearch.searchSequential(config, start, end, BATCH_CHARS)`.
8. Decide stop-button behavior:
   - cooperative between ranges now, or
   - add a narrow cancellation export to `motely-wasm` before final release.
9. Update result handling:
   - `onScoredResult` yields structured `{ seed, score }` today; tallies may not marshal as an array.
   - `onSeedMatch` may carry a CSV-ish row in some scored paths (`seed,score,tally...`). Normalize defensively if needed.
10. Re-run app build/typecheck.
11. Verify MCP app in host/basic-host with resource cache busted by new `APP_RESOURCE_URI`.

## Balatro/JAML knowledge base direction

The MCP app/server should help the model draft real JAML, not just chat about combos. Recommended corpus structure:

- `id`
- `name`
- `archetype` (e.g. economy, retrigger, steel, glass, lucky, spectral, legendary, tag-route)
- `why_it_matters` (plain-language synergy)
- `engine_reality` (what Motely can actually search: shop/packs/events/tags/starting draw)
- `jaml_template`
- `source_notes` (ordinary joker vs legendary, packs, Mega requirement, tag rolls)
- `good_should_clauses` with score rationale
- `common_invalid_forms` (e.g. `joker: Perkeo` with `shopItems`)
- `seed_search_strategy` (list/random/sequential, keyword estimates)
- `ui_hint` (what the app should show/explain)

Examples of high-value synergy docs:

- Legendary starts: Perkeo/Triboulet/Canio/Yorick/Chicot via `legendaryJoker`, Arcana/Spectral/Soul paths.
- Blueprint/Brainstorm + Baron/Mime/Photograph retrigger packages.
- Lucky Cat + The Magician + Oops All 6s + luckyMoney/luckyMult source luck.
- Glass Joker + Justice + Oops All 6s + glassDestroy.
- Steel Joker + The Chariot + DNA + held-in-hand steel.
- Stone Joker + Marble Joker + The Tower.
- Economy: Golden Ticket/Midas/Pareidolia, Business Card, Parking, Mail-In Rebate patterns.
- Voucher and tag routes: Telescope, Overstock, VoucherTag, RareTag/UncommonTag, small/big blind tags.
- Hieroglyph/Petroglyph reachable pack slots: ante rewind can expose ante-1 pack slots 4/5; explicit restricted slots remain exact.

The model should always produce a runnable JAML block plus a brief explanation of why each `must`/`should` belongs. If the user asks for a search, deliver via `show_seedfinder_app` with the JAML loaded.

## Known limitations to preserve honestly

- If a search did not run, say it did not run.
- MCP server should not claim found seeds unless the iframe/client actually found them.
- Full corpus loading is a native repo test concern unless the app bundles a corpus explicitly.
- Live single-seed context stream driving is a C# engine/Jimmolate capability, not a JS marshalling surface.
- Stop/cancel semantics changed between old seedfinder v22 surface and v23 named namespace surface; handle this deliberately.
