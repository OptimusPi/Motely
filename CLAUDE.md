# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**MotelyJAML** is the vectorized (SIMD) Balatro seed-search **engine**. It is a fork
of `Tacodiva/Motely` (`upstream` remote; `origin` is `OptimusPi/MotelyJAML`) that adds
**JAML** (Jimbo's Ante Markup Language) — a declarative filter language — and a WASM/JS
distribution (`motely-wasm`). It is consumed two ways:

- vendored as a git submodule by the **BSO** Avalonia app (`../../` is `BalatroSeedOracle`), and
- as the `motely-wasm` npm package by JS apps (jaml-ui, thelongblind6, ErraticDeck.app…).

This repo is engine + language + tests + packaging. There is no UI here.

## Hard rules

- **NEVER run a search.** Do not `dotnet run` Motely.CLI / Motely.TUI / Motely.Wasm, and
  do not run a search "to verify." A search burns enormous time/tokens. You may **build**
  and **test** (tests are bounded). Read/edit/analyze freely.
- **`TreatWarningsAsErrors=true`** (`Directory.Build.props`) with `Nullable=enable` and
  `ImplicitUsings=enable`. A warning fails the build — fix it, do not `#pragma`-suppress.
- **SDK is pinned exactly to `10.0.202`, `rollForward: disable`** (`global.json`).
  `dotnet --version` must print `10.0.202` or builds fail outright. C# `latest`.
- **AOT everywhere.** CLI uses Native AOT (`EnableCliAot`), WASM uses NativeAOT-LLVM.
  The desktop CLI's AOT publish is `dotnet publish Motely.CLI/Motely.CLI.csproj -c Release -r win-x64`
  (AOT auto-enables on any non-wasm RID — no extra prop).
  No reflection-based (de)serialization: JSON via source-gen contexts (`JamlJsonContext`),
  YAML via the static generator (`JamlYamlContext`, `Vecc.YamlDotNet.Analyzers.StaticGenerator`).
  Never `PropertyInfo.SetValue` / `Enum.GetValues(typeof(...))`; use generic `Enum.GetValues<T>()`.

## Build / test commands

The solution file is **`Motely.slnx`** (XML solution, not `.sln`).

```sh
dotnet build Motely.slnx -c Release                # compile-check everything
dotnet test  Motely.Tests/Motely.Tests.csproj      # xunit; the only test project
dotnet test  Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~JimmolateFilterDescTests"
dotnet test  Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~JamlyzerUnitTests"
```

`Motely.Tests/JamlyzerUnitTests.cs` holds **known-seed ground-truth** assertions (e.g. seed
`ALEEB`) — use these to prove engine/PRNG correctness, never "looks like it ran."

WASM publish + gate (Windows; needs `wasm-tools` workload and the Bootsharp sponsor feed —
see below). Publishing emits the npm package to `../motely-wasm/dist`:

```sh
dotnet publish Motely.Wasm -c Release
node Motely.Wasm/motely.test.mjs            # expect: RESULT: PASS
node Motely.Wasm/pack-consumer-smoke.mjs    # publish -> npm pack -> fresh install -> boot
```

## Projects (`Motely.slnx`)

| Project | Output | Role |
| --- | --- | --- |
| `Motely` | library | the engine: SIMD search core, JAML filters, Jimmolate bridge |
| `Motely.CLI` | exe (`MotelyCLI`) | headless search runner; Native AOT auto-enables when published with a desktop RID |
| `Motely.TUI` | exe (`MotelyTUI`) | Terminal.Gui front-end |
| `Motely.Tests` | xunit | the only tests; ground-truth seed assertions live here |
| `Motely.DataLake` | library | DuckDB result persistence (`DuckDB.NET.Data.Full`) |
| `Motely.Wasm` | `browser-wasm` | Bootsharp interop → the `motely-wasm` npm package |

(`Motely.Run` exists on disk but is not in the solution.)

## Architecture (the parts that span files)

**SIMD-first search.** The engine scores many seeds at once across SIMD lanes. The core
contracts are in `Motely/MotelySearch.cs`: a filter is an `IMotelySeedFilterDesc` that
`CreateFilter`s an `IMotelySeedFilter`, whose `Filter(ref MotelyVectorSearchContext)`
returns a `VectorMask` of which lanes survive. Scoring uses the parallel
`IMotelySeedScoreDesc` / `IMotelySeedScoreProvider`.

**Two search contexts, partial-classed by game domain.** The vector path
(`MotelyVectorSearchContext.*.cs` — Joker, Shop, Tarot, Vouchers, Tags, Packs, Planets,
Spectrals…) is the fast SIMD path. The scalar path (`MotelySingleSearchContext.*.cs`,
same domain split plus Boss/Shuffle/StandardCards) processes **one seed at a time**. Both
mirror the same game-generation streams; when you add a generation step, it usually needs
to exist in both. PRNG/seed math is in `SeedMath.cs`, `LuaRandom.cs`/`VectorLuaRandom.cs`,
`MotelyPrngKeys.cs` — correctness-critical; verify against known seeds, never soften.

**Jimmolate** (`docs/JIMMOLATE.md`) bridges the Immolate single-seed mental model onto the
SIMD engine: a scalar predicate (`MotelyIndividualSeedSearcher`, or JS
`Motely.jimmolateProbe`) runs as an *additional* filter, but **only on seeds the SIMD base
filter already passed** — via `JimmolateFilterDesc` calling `ctx.SearchIndividualSeeds`.
Attach with `MotelySearchSettings.WithJimmolate()`. It throws if no searcher is registered.

**JAML — the declarative filter language.** Authored as YAML (or JSON) with top-level
`name`, `deck`, `stake`, and `must` / `should` / `mustNot` clause lists (at least one
required), plus `defaults`. Loaded by `Motely/Filters/Jaml/JamlConfigLoader*.cs`, turned
into a runnable search by `JamlSearchBuilder.cs`. Each clause **discriminator** maps to a
`*FilterDesc.cs` in `Motely/Filters/Jaml/`:
- generic `joker:` → `JokerFilterDesc`; rarity-pinned `commonJoker` / `uncommonJoker` /
  `rareJoker` / `legendaryJoker` / `soulJoker` → the matching `*JokerFilterDesc`. The
  rarity variants pin the joker to its rarity's generation stream — **only correct when
  the joker actually has that rarity.** A mismatch (`commonJoker: Perkeo`) does not error;
  it silently matches nothing. When unsure of rarity, use generic `joker:`.
- `voucher`, `tarot`, `spectral`, `planet`, `boss`, `tag` (+ `smallBlindTag`/`bigBlindTag`),
  `standardCard`, `erraticRank`/`erraticSuit`/`erraticCard`, `event` → their `*FilterDesc`.
- Shared clause props: `antes` (0 = the Soul/legendary slot before ante 1; 1..8 normal),
  `score:N`, `min:N`, `edition`, `seal`, `enhancement`, `stickers`, `sources`. Names are
  **PascalCase**, no spaces/punctuation. Scoring `mode` = `sum` | `max`.
- See `index.jaml` for a fully-commented example and `jaml.schema.jaml` for the grammar.
  Saved community filters live in `JamlFilters/`; engine-native composite filters
  (Perkeo+Observatory, etc.) live in `Motely/Filters/Native/`.

**Jamlyzer** (`Motely/Analysis/`) analyzes individual seeds against a JAML doc (what the
seed actually produces, per-ante). Note the live cleanup in `TODO.md`: legacy string-JAML
entry points (`MotelyJamlyzer*Config` taking `string Jaml`) are a known footgun being
removed; prefer the structured config path.

## WASM / Bootsharp specifics

`Motely.Wasm` compiles to `browser-wasm` and uses **Bootsharp** to generate the JS/TS
bindings for the `motely-wasm` package. Pins are in `Directory.Packages.props`: the three
core packages (`Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject`) restore from nuget.org;
**`Bootsharp.FileSystem` is sponsor-gated** (Rewaffle GitHub Packages feed, authenticated
PAT) and versioned separately by build timestamp. Building/repacking Bootsharp itself is a
local-machine workflow documented in **`AGENTS.md`** (deep reference; sponsor-gated, uses
local `D:/…` paths). The upstream Motely source for diffing is at `D:\motely-upstream`
(`Motely/README.md` is a stub pending an upstream sync).

Bootsharp writes a minimal `package.json` (name, type, exports, browser — see
`D:/bootsharp/src/cs/Bootsharp/Build/PackageTemplate.json`). It does not set `version` or
`types`. The old `Motely.Wasm/finalize-package.mjs` post-processor that stamped those was
**removed** on request. If the published package needs `version` / `types` / TS-aware
exports, handle it inside the build — do **not** re-add a standalone node script.

## Backlog

`TODO.md` is the real, current engineering backlog (Jamlyzer cleanup, DataLake/DuckLake
catalog work, WASM test gaps, downstream consumer rehabs, dev tooling). Check it before
starting work in those areas — several items document footguns and half-done migrations.
