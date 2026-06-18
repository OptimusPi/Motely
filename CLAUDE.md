# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**MotelyJAML** is a fork of tacodiva's **Motely** — a vectorized (512-bit SIMD, 8 seeds at
once per thread) seed-search engine for **Balatro**. The core (`Motely/`) is a C# filter API;
everything else is a head that drives it.

A filter is a two-phase contract:

- `IMotelySeedFilterDesc<TFilter>.CreateFilter(ref MotelyFilterCreationContext ctx)` runs once
  at setup to declare which PRNG streams to cache.
- `IMotelySeedFilter` (a `struct`) — `Filter(ref MotelyVectorSearchContext)` is the hot path:
  returns a `VectorMask` over **8 seeds at once** as a cheap vectorized gate, then
  `SearchIndividualSeeds(mask, predicate)` drops to scalar `MotelySingleSearchContext` only for
  surviving lanes. **Vector gate, scalar confirm.**
- `MotelySearchSettings<TBaseFilter>` is the fluent driver:
  `.WithStake().WithDeck().WithThreadCount().WithListSearch(…).WithAdditionalFilter(…).Start()`.

## Build / test / run

**C# side.** Solution is `Motely.slnx` (XML format). .NET 10 SDK pinned in `global.json`
(`10.0.204`). C# package versions are centralized in `Directory.Packages.props` (Central
Package Management) — including `<MotelyVersion>`, the single source of truth for the published
wasm version. Read the version from there.

```powershell
dotnet build Motely.slnx
dotnet test Motely.Tests/Motely.Tests.csproj
dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~SomeTestName"   # single test
dotnet run --project Motely.CLI -- --jaml simple_test --keyword YOURNAME --cutoff 0
```

A bare `--jaml <name>` resolves to `JamlFilters/<name>.jaml`. The CLI also has `--analyze
<seed>` (human-readable seed dump) and `--native <name>` (run a hardcoded C# filter instead of
JAML). Full CLI guide: `docs/FIND_BALATRO_SEED_WITH_MOTELY_CLI.md`.

**WASM / JS side** (versions independently of CPM via its own `package.json`). From
`Motely.Wasm/`:

```powershell
npm test          # node --test "tests/*.test.mjs" — JS suite against the built dist
npm run build     # dotnet publish Motely.Wasm.csproj -c Release  (NativeAOT-LLVM → dist/)
```

`release.ps1` runs the full publish pipeline (pack Bootsharp.FileSystem → restore → publish
wasm → JS tests → npm publish). Build plus the test suites are the full quality gate.

## Architecture

Single engine, multiple heads. **`Motely/`** is the SIMD runtime plus the JAML system.

**JAML pipeline** (needs cross-file reading to follow):

1. `Motely/Filters/Jaml/JamlConfigLoader*.cs` parses a `.jaml` (YAML) into a `JamlConfig` —
   `deck`/`stake`/`seeds` plus `must` / `should` / `mustNot`, each a list of `IJamlClause`.
2. Each clause type maps to a `…FilterDesc` (the vectorized matcher) via the `CreateDesc`
   switch in `JamlClause.cs`. Descs live in `Motely/Filters/Jaml/`; hand-written native ones in
   `Motely/Filters/Native/`.
3. `JamlSearchBuilder.CreatePlan(config, cutoff)` assembles the descs into a `JamlSearchPlan`,
   pushing a score cutoff into the engine so low-scoring seeds drop at the scorer.
4. The head applies a seed-input mode and calls `.Start()`.

`must`/`mustNot` are hard gates; `should` carries a `score` and only affects ranking. Seed
input is orthogonal to the filter: keyword, random, explicit `--seeds`, source/aesthetic, or
the default sequential batch sweep. CLI wiring is `Motely.CLI/CliSearchMode.cs`; results fan
out through `IMotelyResultSink`.

**Heads split into two execution worlds:**

- **Desktop / native C#** (a .NET process, full CPU SIMD): `Motely.CLI/`, `Motely.TUI/`
  (terminal UI), `Motely.DataLake/` (DuckDB/DuckLake over saved results). They reference
  `Motely/` directly.
- **Browser / WASM**: `Motely.Wasm/` compiles `Motely/` to `browser-wasm` via Bootsharp
  (NativeAOT-LLVM), emitting an ES module to `dist/`. The JS entry points are the
  Bootsharp-exported members of `Motely.Wasm/Program.cs`, not a C# `Main`. `Motely.Home/` is a
  static-file host serving a vanilla-JS SPA that loads that wasm in a web worker; the search
  runs on the user's CPU in the browser, no server.

## Source of truth for names and values

- **Item names are PascalCase, no spaces** (`Blueprint`, `SixthSense`, `Perkeo`). Source them
  from `Motely/Enums/` or `--analyze <seed>`.
- **Seeds** use `1-9` and `A-Z`, up to 8 chars — a 35-character alphabet (`0` reads as `O`).
- Enum values, clause keys, deck/stake names, and CLI flags live in `Motely/Enums` and the JAML
  clause model. Read them there.
- After adding/deleting/moving files, call `switch_solution` on the roslyn-lens MCP to reload
  its view of the solution.

## Bootsharp (Motely.Wasm)

The C#→JS surface is **derived, not chosen**: namespace → module path, type → node, members
camelCase. `[RenameModule]`/`[RenameNode]`/`[RenameMember]` customize it; returning null erases
a node/member from the JS surface (how `BootsharpRenamers` keeps the surface to the members
Bootsharp emits cleanly). After any change, read the generated `dist/generated/modules/*.g.d.mts`
to confirm the real shape. Pinned docs: `d:\bootsharp\docs\guide\`;
samples: `d:\bootsharp\samples\`.
