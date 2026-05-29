# MotelyJAML — Long-Term TODO

Backlog of real items that came up but didn't get done. Not memories. Just things.

## Engine

- **Jamlyzer cleanup.** Kill string-jaml legacy entry points (`MotelyJamlyzerConfig`, `MotelyJamlyzerSeedAnalysisConfig`, `MotelyJamlyzerSeedListConfig` all take `string Jaml` — same footgun killed in `Motely.Wasm/Program.cs`, still alive here). Delete dead code (`TryResolveSeedListConfig`, `TryResolveConfig` in `MotelyJamlyzer.cs:273-326` — no callers). DRY the three near-identical `AnalyzeSeed` / `AnalyzeSeeds` / `Analyze` entry points.
- **Add `includeStreamStates` to `MotelyJamlyzerResult`.** Per-seed final stream states (doubles) attached to results so later WASM-side `GetNext` can resume from where Jamlyzer left off. Cheap (a few MB for a million items). Opt-in flag on the config.
- **Migrate legacy `MotelySeedAnalyzer` callers to Jamlyzer-with-default-JAML.** Keep `MotelySeedAnalyzer` only for text-block / Immolate.cl PRNG-comparison use.
- **Fix `--save-seeds` writing `SEED,SCORE` format.** Round-trips through `NormalizeSeeds` → score `0` becomes `O`, entries become 10-char strings with commas, fail seed validation on reload. Either strip score before write or split on comma in NormalizeSeeds.
- **Heuristic must/should auto-derive at JAML load time.** If a clause has `score:` or `min:`, default to `should:`; otherwise `must:`. Reduces the "I said should and you put it in must" failure mode.

## DataLake

- **Use DuckLake's catalog model properly.** Currently per-filter `.duckdb` files masquerading as a lake. Real DuckLake = ONE catalog DB, filters become tables in it, schema evolution is a catalog op.
- **Schema-mismatch self-heal.** `MotelyLakeSeedSink.Append` (line 71) crashes when JAML's `should:` count changes. Should be a catalog migration: snapshot old schema, write new, evolve. Not a crash.
- **Resume front-loading.** When opening a lake for a filter that already has seeds, re-score them under the current JAML and pre-seed the result set. The original promise of having a DB: keep your place across 3-day searches.
- **Pre-populated catalog with curated decks.** Ship Erratic Deck (and other) known-dank seeds as parquet bundled with the app or first-run-downloaded from public DuckLake. New install has a starter pack of seeds, no search needed to play.
- **Concurrent-reader-while-writer.** With DuckLake done right, the desktop app can browse matches while a 72-hour CLI search is still running — no exclusive lock.

## WASM / Bootsharp

- **22/22 test pass.** Two `analyzer.test.mjs` tests still fail on probe-seed lookup. Use a known seed (ALEEB or similar from `Motely.Tests/JamlyzerUnitTests.cs:74`) where the Buffoon pack and shop contents are pinned.
- **Rewrite `Motely.Wasm/README.md`.** Documents deleted decoders (`decodeItemType` etc.), `Motely.version()`, submodule paths (`motely-wasm/motely`, `/enums`, etc.), `node-boot`, `validateJaml`, schema. All gone — README still lies.
- **Audit `Motely.Wasm/Program.cs` for wrapper crud.** Identify exports that are wrappers around `MotelyJamlyzer` / `MotelySeedRouterDesc` and could be the underlying type exposed directly. Shrink Program.cs to ceremony + jimmolate probe + file-system bridge.
- **Export `MotelyJamlyzer` / `MotelySeedRouterDesc` directly via Bootsharp instance binding** instead of single-method facades in Program.cs.
- **Pack the (now landed) Bootsharp patches into an upstream issue + PR.** Three patches: `IsByRef` → `ref` in `BuildSyntax`, `ShouldInspectMethod` filter for generic / ref-struct / ref-value-type, `CSInteropGenerator.EmitMethodExport/Import` use disambiguated `JSName` for wrapper names. Repro from `MotelySingleSearchContext.GetNextJoker` overloads. Files: `D:/bootsharp/src/cs/Bootsharp.Publish/Common/Global/GlobalType.cs`, `Common/Inspection/TypeInspector.cs`, `GenerateCS/CSInteropGenerator.cs`. Diff already saved.

## Apps / Consumers

- **Fix `thelongblind6`** (Next.js + R3F app at `x:\thelongblind6`). `motely-wasm` import paths reference subpaths (`motely-wasm/motely`) that don't resolve in v19 — must import from root. Missing `public/motely-wasm/bin/` for runtime boot.
- **Full rehab `seedfinder.app`** (Vite SPA at `d:\seedfinder.app`). Dual-CDN brain damage: importmap points at esm.sh, boot URL at jsdelivr, local `.tgz` ignored. Pick one, ideally local `/motely-wasm/bin` like jaml-ui.
- **Full rehab `ErraticDeck.app`** (Next.js Cloudflare app at `d:\ErraticDeck.app`). Pinned `^15.1.0` in package.json, hardcoded `v14.3.3` in `lib/motelyCdn.ts`, jaml-ui peer dep wants v19 → 5-major skew. Bump to v19.0.x, kill the hardcoded CDN, integrate jaml-ui's boot pattern.
- **Investigate `examples/mcp-seed-finder`** in `x:\jaml-ui`. Likely throwaway per "potentially trash" — but it's the seed for the MCP-for-Claude.ai-friend deliverable. Either salvage or write fresh.
- **jaml-ui Storybook.** Currently the public-facing Vercel deploy, described as bad. JimboText / m6x11 font issues. Schema drift across stories. Real surface to rebuild.

## Dev Tooling

- **VS Code extension (real one, schema-grounded).** Use the Bootsharp-emitted TypeScript types in `motely-wasm/dist/generated/modules/*.g.d.mts` as the authoritative schema source — schema drift becomes structurally impossible. Static smarts: autocomplete, hover docs, diagnostics, must/should auto-derive quick-fix.
- **JAML LSP.** The value-add over schema-only: boot motely-wasm in the extension host, run Jamlyzer against the cursor's JAML doc as you type. Live seed preview ("show me what `RN71DOOB` looks like under THIS jaml right now"), inline JAML-Map rendering, real-time scoring.

## Cleanup

- **`Motely.CLI` assembly name mismatch.** `<AssemblyName>MotelyCLI</AssemblyName>` (no dot) in `Motely.CLI.csproj:21` doesn't match folder/csproj name `Motely.CLI`. Drop the override, let it default to `Motely.CLI`, then `InternalsVisibleTo("Motely.CLI")` is the natural form everywhere.
- **`Motely.Wasm.csproj` `FinalizeNpmPackage` polish.** Now a small Node script (`finalize-package.mjs`) instead of inline PowerShell. Could be smaller still if Bootsharp grew a `BootsharpPackageVersion` knob — file as upstream feature request.
- **`jaml.schema.jaml`** (the joke). Meta-schema in JAML describing JAML. For da memes.
