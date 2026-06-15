# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**MotelyJAML** is a fork of tacodiva's **Motely** — a vectorized (512-bit SIMD, 8 seeds at
once per thread) seed-search engine for **Balatro**, faster than the OpenCL/GPU searchers
(e.g. Immolate) on most CPUs.

Motely is, first, a clean C# filter API. A filter is a two-phase contract:

- `IMotelySeedFilterDesc<TFilter>` — `CreateFilter(ref MotelyFilterCreationContext ctx)` runs
  once at setup to declare which PRNG streams to cache.
- `IMotelySeedFilter` (a `struct`) — `Filter(ref MotelyVectorSearchContext)` is the hot path:
  it returns a `VectorMask` over **8 seeds at once** (`Vector512`, `VectorEnum256`) as a cheap
  vectorized gate, then `SearchIndividualSeeds(mask, lambda)` drops to scalar
  `MotelySingleSearchContext` only for the lanes that survived. Vector gate, scalar confirm.
- `MotelySearchSettings<TBaseFilter>` is the fluent driver:
  `.WithStake().WithDeck().WithThreadCount().WithBatchCharacterCount().WithListSearch(…)`
  `.WithProviderSearch(…).WithAdditionalFilter(…).Start()` → returns `IMotelySearch`.

## Build / test / run

- Solution: **`Motely.slnx`** (XML format). .NET 10 SDK, pinned in `global.json`.
  Package versions are centralized in `Directory.Packages.props` (Central Package Management).

```powershell
dotnet build Motely.slnx
dotnet test Motely.Tests/Motely.Tests.csproj
dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~SomeTestName"   # single test
```

Run a JAML search (from repo root, so the path resolves):

```powershell
dotnet run --project Motely.CLI -- --jaml simple_test --keyword YOURNAME --cutoff 0
```

A bare `--jaml <name>` resolves to `JamlFilters/<name>.jaml`. Full CLI guide:
`docs/FIND_BALATRO_SEED_WITH_MOTELY_CLI.md`.

**Repo scripts:**
- `clean.ps1` — recursively removes every build output (`bin`/`obj`/`dist`/`publish`) plus
  `node_modules` (regenerable via `npm install`) repo-wide. Source is never touched.
- `release.ps1` — builds and publishes `motely-wasm`. The published version is read from the
  single source of truth: `<MotelyVersion>` in `Directory.Packages.props` (CPM) — never hardcode it.

## Architecture

Single engine, multiple heads. **`Motely/`** is the core: the SIMD search runtime plus the
JAML system. Everything else is a front-end that drives it.

**The JAML pipeline** (the part that needs cross-file reading to understand):

1. `Motely/Filters/Jaml/JamlConfigLoader*.cs` parses a `.jaml` (YAML) file into a
   `JamlConfig` (`JamlConfig.cs`) — top-level `deck`/`stake`/`seeds` plus three clause lists
   `must` / `should` / `mustNot`, each a list of `IJamlClause`.
2. Each clause type (e.g. `JokerClause`, `VoucherClause`, `BossClause`) maps to a
   `…FilterDesc` via the big `switch` in `JamlClause.cs` (`CreateDesc`). The `FilterDesc`
   classes are the actual vectorized matchers — one per concept, living next to the model in
   `Motely/Filters/Jaml/` (and hand-written native ones in `Motely/Filters/Native/`).
3. `JamlSearchBuilder.CreatePlan(config, cutoff)` assembles the descs into an
   `IMotelySearchSettings` (a `JamlSearchPlan`), pushing a fixed score cutoff into the engine
   so low-scoring seeds are dropped at the scorer rather than via callback spam.
4. The head applies a **seed-input mode** to those settings and calls `.Start()`.

**Search-input modes are orthogonal to the filter.** Any filter (JAML or native) can be fed
seeds by: keyword (`--keyword`/`--keywords`, padded to 8 chars), random (`--random N`),
explicit list (`--seeds`), a file/aesthetic source (`--source`/`--aesthetic`), or the default
**sequential** batch sweep (`--startBatch`/`--endBatch`/`--startPercent` or
`--startSeed`/`--stopSeed`; `--batchCharCount` 1–7 controls batch granularity). The CLI
wiring is `Motely.CLI/CliSearchMode.cs`; results fan out through `IMotelyResultSink`
(`ConsoleResultSink`, plus a DuckLake sink when a filter produces structured scores).

**`must` vs `should` vs `mustNot`:** must/mustNot are hard gates; `should` clauses carry a
`score` and only contribute to ranking. Shared clause props: `antes`, `min`, `max`, `score`,
`label`. Joker/card names are PascalCase, no spaces (`Blueprint`, `SixthSense`, `Perkeo`).

**Seeds** use the alphabet `1-9` and `A-Z` — **no `0`** (it normalizes to `O`), max 8 chars.
A cancelled sequential search prints a `--startBatch` / `--startSeed` resume hint.

## Heads (front-ends over `Motely/`)

- `Motely.CLI/` — command-line head (`McMaster.Extensions.CommandLineUtils`). Also does
  `--analyze <seed>` (human-readable seed dump) and `--native <name>` (run a hardcoded C#
  filter instead of JAML).
- `Motely.TUI/` — terminal UI.
- `Motely.Wasm/` — WebAssembly build via **Bootsharp**. Exports `Program.FromYaml` /
  `Program.FromJson` (config parsers), `RunSequentialSearch`/`RunRandomSearch`, and
  `OnProgress`/`OnSeedMatch`/`OnScoredResult` event streams. NativeAOT-LLVM auto-enables
  under `dotnet publish -c Release`; no hand-wiring.
- `Motely.Home/` — an ASP.NET Core minimal-API **static-file host** (not Blazor). Serves
  `wwwroot/app.html`, a vanilla-JS SPA whose search runs in `wwwroot/worker.mjs` loading the
  motely-wasm dist from `/wasm`.
- `Motely.DataLake/` — DuckDB / DuckLake tooling over saved results (the `Seeds/` parquet +
  `.ducklake` files).

## Conventions

- Enum values, clause keys, deck/stake names, and CLI flags have a single source of truth in
  `Motely/Enums` and the JAML clause model — read them rather than guessing.
- Platform is Windows / PowerShell (the Bash tool is also available for POSIX scripts; there
  is no `/dev/null`).

## JAML authoring: hard rules (read every time you write a filter)

**Rule #1 — names are everything.** A wrong PascalCase name (e.g. `SixthSence`, `blue_joker`)
parses fine, validates green, and finds **zero seeds** with no error. Always verify every item
name against the engine enums before using it:

```powershell
# grep the actual enum source — don't guess
grep -r "BlueJoker\|SixthSense\|Perkeo" Motely/Enums/
```

Or run `--analyze <any-seed>` to see the real names the engine uses for every entity.

**Rule #2 — discriminator, one per clause.** Each clause names exactly one thing. The
`ValidateSingleDiscriminator` guard (added 2026-06-15) will throw at parse time if you
accidentally nest two keys. Discriminators: `joker`, `jokers`, `voucher`, `vouchers`,
`tarotCard`, `tarotCards`, `spectralCard`, `spectralCards`, `planetCard`, `boss`, `tag`,
`tags`, `smallBlindTag`, `bigBlindTag`, `standardCard`, `standardCards`, `erraticRank`,
`erraticSuit`, `erraticCard`, `startingDraw`, `event`, `luckyMoney`, `luckyMult`,
`misprintMult`, `wheelOfFortune`, `cavendishExtinct`, `grosMichelExtinct`, `spaceLevelup`,
`businessPayout`, `bloodstoneTrigger`, `parkingPayout`, `glassDestroy`, `wheelStaysFlipped`,
`and`, `or`, `clauses`. Always use `joker:` (generic) — rarity-specific keys are footguns.

**Rule #3 — `max: 1` for rare enablers.** When you need exactly one of a rare joker
(Blueprint, Brainstorm, Perkeo), add `max: 1`. Without it you're demanding the pool produce
it multiple times and you'll get far fewer hits than expected.

**Rule #4 — `sources:` pins the acquisition path.** Rare jokers not naturally in the shop
(Legendary jokers, Spectral-only drops) need `sources: [SoulCard]` or the relevant source.
Without sources pinning, the filter searches the wrong pool and finds nothing or lies.

**Rule #5 — `luckyMult` and `luckyMoney` are event streams, not joker appearances.** They
represent probabilistic in-run triggers (Lucky Card mult/money hits), counted across all
occurrences across all antes. Treat them as hit counters, not item presence checks.

**Rule #6 — the Showman dupe-reroll rule.** Showman must appear in acquisition order
*before* the joker it's allowing duplicates of. The filter must constrain this via `antes:`
ordering, or you'll find seeds where it shows up too late.

**Rule #7 — search small before searching big.** Always prototype with `--keyword YOURNAME`
(fast, personal, 8-char) or `--random 1000` before launching a sequential sweep. Confirm the
filter fires at all and the output looks right. Then widen.

**Rule #8 — verify hits with `--analyze`.** After a seed matches, run
`dotnet run --project Motely.CLI -- --analyze <SEED>` to get a human-readable dump and
confirm the filter caught what you think it caught. Don't claim victory before reading the
analysis.

**Rule #9 — avoid bargain-bin seeds.** A seed can technically pass `must` clauses but be
unplayable (e.g. target joker appears ante 7 with no scaling). Add `antes: [1,2,3]` to gate
early if the joker needs to be found early to matter.

**Rule #10 — `--threads 1` for determinism with small pools.** Multi-threaded search is
non-deterministic in output order. When validating a filter against a small explicit seed list
(`--seeds`) or debugging, add `--threads 1` for reproducible results.

## jaml-lang (the one brain — Rule #1 enforcer)

`jaml-lang/` is a TypeScript npm package that catches wrong names *before* a search runs.
Its vocabulary is generated from the engine — never guessed.

```powershell
# Regenerate vocabulary from engine (run after adding new jokers/vouchers/etc.):
dotnet run --project Motely.CLI -- --vocab > jaml-lang/vocabulary.json

# Validate a JAML filter (returns diagnostics with fuzzy suggestions):
node -e "
  import('./jaml-lang/dist/index.js').then(({ validateNames }) => {
    const r = validateNames(require('fs').readFileSync('JamlFilters/my.jaml','utf8'));
    r.diagnostics.forEach(d => console.log(d.message));
  });
"
```

Before writing any JAML — or drafting any for a user — pipe it through `validateNames()`.
Zero diagnostics = engine will accept it. Any diagnostic = it would silent-zero.

## What to read for deeper context

- `docs/FIND_BALATRO_SEED_WITH_MOTELY_CLI.md` — worked examples, all CLI flags, gotchas
- `docs/Balatro_Master_Encyclopedia.md` — complete entity taxonomy and mechanical effects
- `docs/balatro-synergy.md` — archetype table, synergy matrix, anti-synergy traps
- `docs/SEED_GENIE_PROMPT.md` — how to present seeds to users (make it personal)
- `docs/HANDOFF.md` — what prior agents overclaimed; read before stating anything as fact
- `JamlFilters/` — existing working filters as living examples
