# Motely.Wasm

The Motely engine compiled for the browser with [Bootsharp](https://bootsharp.com). The
TypeScript head is generated from the `[Export]` signatures at build time — records, enums
and dictionaries cross as themselves. Nothing in this project restates an engine type:
no DTO twins, no JSON strings, no hand-typed vocabulary. The JAML grammar surface comes
from the generated `JamlSchema`, so a FilterDesc added tomorrow appears in the browser
without this project changing.

## Build

```sh
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release   # NativeAOT-LLVM + Binaryen
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Debug    # Mono, slower, debuggable
```

The compiled ES module lands in `bin/motely-wasm/` with a `package.json` next to this
file mapping the `motely-wasm` package to it. Binaries are embedded — `boot()` takes no
arguments and no extra files need serving.

`host/main.mjs` boots the module and exposes the API as `globalThis.motely`, firing a
`motely-ready` event when it's callable:

```js
import bootsharp from "../bin/motely-wasm/index.mjs";
import { MotelyWasmApi } from "../bin/motely-wasm/generated/modules/motely/wasm.g.mjs";
await bootsharp.boot();
```

## API

Real types in, real types out. Result-only shapes live in `WasmDtos.cs`; everything else
crosses as the engine type it is. Type declarations: `bin/motely-wasm/generated/modules/motely/wasm.g.d.mts`.

| Export | Returns |
| --- | --- |
| `version()` | engine informational version (`25.1.0+<sha>`) |
| `parseJaml(text)` | `ParseResult` — `{ok, error?, name?, deck?, stake?, must, should, mustNot}` |
| `vocabulary()` | `Map<string, string[]>` — every vocabulary kind from the generated `JamlSchema`, including event clauses nobody hand-listed |
| `discriminators()` | every clause wire the loader constructs — `joker`, `voucher`, `luckyMoney`, … |
| `clauseKeys(disc)` | the keys a clause accepts (`min`, `max`, `score`, `antes`, …) |
| `diagnostics(text)` | `JamlDiagnostic[]` with spans, stable codes, enum severity |
| `hover(text, line, ch)` | `JamlHoverInfo` or `null` |
| `complete(text, line, ch)` | `JamlCompletionItem[]` with kinds and replace spans |
| `explain(topic)` | grammar prose or `null` |
| `semanticTokens(text)` / `semanticTokenTypes()` | LSP 5-int encoding + its legend |
| `runScoreSeeds(jaml, seeds)` | *(async, void)* list-mode search; collect with `takeRun()` |
| `runFindSeeds(jaml, intent)` | *(async, void)* search from a `MotelySearchIntent`; collect with `takeRun()` |
| `takeRun()` | `ScoreRun` — the last completed run; clears the slot |

### Why run/take instead of returning the result?

Bootsharp packs serialized returns into an `Int64`. The .NET runtime marshals that fine
for synchronous returns but has no `Task<Int64>` marshaler (`ToJSNotImplemented` on Mono,
opaque failure on NativeAOT-LLVM), so an async export can never return a record. Plain
`Task` and synchronous serialized returns both work, hence the split. The host recomposes
the pair, so pages simply write:

```js
const run = await motely.scoreSeeds(jaml, ["TPZZOLBB"]);   // ScoreRun, results best-first
```

## Smoke test

```sh
cd Motely.Wasm/tests && npm install
npx playwright-core install chromium-headless-shell   # once, if no browser is preinstalled
node smoke.mjs                                        # serves the repo root
```

Loads `host/index.html` in headless Chromium and asserts the page's self-reported verdict.
The flagship check is the board's done-criterion: parse `JamlFilters/Whimsy_Dicetricks.jaml`,
score `TPZZOLBB` → **245** (and `MB8GJDBB` 227, `OP3ZOBBB` 206, `3VDISOUP` 129), with the
vocabulary served from the generated schema — event clauses included, no hand lists.

Chromium resolution order: `CHROMIUM_PATH` env var → `PLAYWRIGHT_BROWSERS_PATH`
(default `/opt/pw-browsers`) → playwright's own browser cache.
