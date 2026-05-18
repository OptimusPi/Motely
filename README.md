# MotelyJAML

Motely is a SIMD-accelerated Balatro seed-search engine. JAML — Jimbo's Ante Markup Language — is the YAML-based filter language it runs.

Pick a seed strategy in JAML, point Motely at it, get back the seeds that match.

```yaml
name: WeeMonday
deck: Erratic
stake: Black
must:
  - joker: WeeJoker
    antes: [1]
should:
  - voucher: Observatory
    antes: [1, 2]
    score: 5
```

Two surfaces share the same engine:

- **`MotelyCLI` / `MotelyTUI`** — local searches across all cores, AVX/AVX-512 where available.
- **`motely-wasm`** — the same engine published as an npm package. Runs in browsers, Node, Bun, Deno, and edge workers via Bootsharp + NativeAOT-LLVM.

## Quick start (CLI)

```powershell
dotnet build Motely.slnx -c Release
dotnet run --project Motely.CLI -c Release -- --jaml JamlFilters/01WeeMonday.jaml
```

Useful flags (full list: `--help`):

| Flag | What it does |
| --- | --- |
| `--jaml <path>` | Run a JAML filter |
| `--analyze <SEED[,SEED…]>` | Analyze specific seeds; add `--output-json` for NDJSON |
| `--native <name>` | Run a hand-written C# filter (e.g. `PerkeoObservatory`, `Trickeoglyph`) |
| `--random <N>` | Sample N random seeds instead of sequential |
| `--keyword <WORD>` / `--keywords W1,W2` | Restrict search to seeds containing keyword(s) |
| `--startSeed` / `--stopSeed` | Bound a sequential range by literal seed string |
| `--startBatch` / `--endBatch` / `--startPercent` | Bound by batch index or percent of the keyspace |
| `--threads <N>` | Defaults to `Environment.ProcessorCount` |
| `--cutoff <N\|auto>` | Drop seeds below score N, or follow the running max |
| `--save-seeds` | Write the top 1000 hits back into the JAML file's `seeds:` block |
| `--drown` | Replay saved seeds for a filter from `results/<id>/results.csv` via DuckDB |
| `-q` / `--quiet` | Suppress per-batch progress lines |

## Quick start (TUI)

```powershell
dotnet run --project Motely.TUI -c Release
```

Terminal.Gui frontend: filter library, JAML editor, results browser, and an embedded API server window for distributed workers.

## Quick start (npm)

```sh
npm install motely-wasm
```

```js
import bootsharp, { Motely } from "motely-wasm";

await bootsharp.boot("/motely-wasm/bin"); // browser: URL path; Node: pass { wasm: bytes }

const jaml = `
name: WeeMonday
deck: Erratic
stake: Black
must:
  - joker: WeeJoker
    antes: [1]
`;

if (Motely.validateJaml(jaml) !== "valid") throw new Error("bad JAML");

Motely.onScoredResult.subscribe(r => console.log(r.seed, r.score));
Motely.onProgress.subscribe(p => console.log(`${p.percentComplete.toFixed(1)}%`));

const search = Motely.createSearch(jaml).withSequentialSearch().start();
await search.waitForCompletionAsync();
```

Full package docs, including the Node boot path, web-worker example, and submodule exports: **[`Motely.Wasm/README.md`](Motely.Wasm/README.md)**.

## Project layout

| Project | Purpose | Target |
| --- | --- | --- |
| `Motely/` | Core engine, JAML parser, scoring, SIMD search contexts | `net10.0` |
| `Motely.CLI/` | Command-line searcher (`MotelyCLI`) | `net10.0`, optional AOT |
| `Motely.TUI/` | Terminal.Gui app — editor, library, results, API server | `net10.0` |
| `Motely.DataLake/` | DuckDB result and seed sinks | `net10.0` |
| `Motely.Wasm/` | Bootsharp entry; publishes the npm package | `net10.0` + `browser-wasm` |
| `Motely.Tests/` | xUnit tests, golden JAML corpus, regression fixtures | `net10.0` |
| `JamlFilters/` | Curated example filters (`*.jaml`) |  |
| `Seeds/` | Local result CSVs and the `motely.ducklake` DuckDB store |  |

Root-level generated artifacts: `jaml.schema.json` and `jaml-schema.cs`. Don't hand-edit — they come from `Motely.CLI/MotelyJAML.schema.generator.cs`.

## JAML in 30 seconds

- One required top-level: **`must`**, **`should`**, or **`mustNot`** (combine any subset). `must` short-circuits on first fail; `mustNot` rejects on match; `should` accumulates score.
- Optional top-level: `name`, `deck`, `stake`, `seeds:` (curated list for `--jamlyzer` / `--save-seeds`).
- Clause discriminators: `joker`, `commonJoker`, `uncommonJoker`, `rareJoker`, `legendaryJoker`, `voucher`, `tarot`, `spectral`, `planet`, `boss`, `tag`, `smallBlindTag`, `bigBlindTag`, `standardCard`, `erraticRank`, `erraticSuit`, `erraticCard`, `startingDraw`, `event`.
- Shared per-clause props: `antes: [1..8]`, `score`, `min`, `edition`, `stickers`, `seal`, `enhancement`.
- Identifiers are PascalCase, no spaces. Enum case is forgiving but PascalCase is canonical.

See `JamlFilters/` for real examples and `Motely.Tests/GoldenJamlFiles/` for the parse-stability corpus.

## Building & testing

```powershell
dotnet build Motely.slnx -c Release
dotnet test Motely.Tests
```

`TreatWarningsAsErrors` is on everywhere — fix the cause, don't silence the warning.

Clean every `bin/`, `obj/`, and `motely-wasm/{bin,dist}` from the tree:

```powershell
.\clean.ps1
```

## Publishing the npm package

The engine version lives in `<MotelyVersion>` in `Directory.Packages.props`. Bumping it bumps every project (assembly version), the npm package version, and the WASM-exported `Motely.version()` constant.

```powershell
dotnet publish Motely.Wasm -c Release
node Motely.Wasm/motely.test.mjs        # must report RESULT: PASS
cd motely-wasm
npm publish --access public
```

Bootsharp emits the npm package under `motely-wasm/` (gitignored). The full publish playbook — pre/post-publish gates, known-broken `exports` shapes, the `npm view` byte-check — lives in [`CLAUDE.md`](CLAUDE.md).

## Bootsharp / NativeAOT-LLVM

`Motely.Wasm` builds with `RuntimeIdentifier=browser-wasm` and the Bootsharp toolchain emits a NativeAOT-LLVM WebAssembly module plus generated JS bindings. The result is a plain ES module — no Emscripten glue, no DOM assumptions, runs anywhere with WASM + an ES-module loader. The local Bootsharp feed isn't committed; see `nuget.config` for the per-user merge point.

## Local artifacts (gitignored)

- `motely-wasm/` — Bootsharp publish output (regenerated by `dotnet publish Motely.Wasm`)
- `Seeds/` — search outputs and DuckLake store
- `JamlFilters/old/` — archived filter drafts

## License

See repository LICENSE.
