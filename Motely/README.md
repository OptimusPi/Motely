# Motely — the MotelyJAML engine

**Motely** is a fast Balatro seed-searching library. It uses your CPU's 512-bit
registers and SIMD to search **8 seeds at once per thread** — and is, to the original
author's knowledge, the fastest general-purpose CPU-based Balatro searcher, competitive
with (often beating) GPU searches.

> Motely was created by [@Tacodiva](https://github.com/Tacodiva/Motely), commissioned by
> [@OptimusPi](https://github.com/OptimusPi/) (pifreak). **MotelyJAML** is pifreak's fork
> that adds a declarative filter language, per-seed analysis, and a WASM/JS distribution on
> top of that engine.

## What MotelyJAML adds on top of Motely

| | What it is | Where |
| --- | --- | --- |
| **JAML** — Jimbo's Ante Markup Language | A declarative filter language. Describe the seed you want in YAML (`must` / `should` / `mustNot` clauses over jokers, vouchers, tarots, tags, decks…) instead of hand-writing a filter. Compiles to Motely's vectorized SIMD search. | `jaml.schema.jaml` (grammar), `index.jaml` (worked example), `Filters/Jaml/` |
| **JAMLyzer** | Point it at one seed and a JAML doc and it tells you what that seed actually produces, per ante — the analysis counterpart to search. | `Analysis/` |
| **Jimmolate** | The Immolate "just write code against the seed" experience, bridged onto SIMD: a scalar `seed => bool` predicate that runs **only on the seeds the fast base filter already passed**. | `Filters/Native/JimmolateFilterDesc.cs`, `../docs/JIMMOLATE.md` |
| **motely-wasm** | The engine compiled to `browser-wasm` via Bootsharp and shipped as an npm package, so JS/web apps can search and analyze in the browser. | `../Motely.Wasm/` → `../motely-wasm/` |

## Start here

- **Author a JAML filter** → `../jaml.schema.jaml` (every key) and `../index.jaml` (a
  commented, real filter).
- **Write / run a Jimmolate predicate** → `../docs/JIMMOLATE.md` (build, run, JS surface).
- **Convert an old Immolate `.cl` filter** → the `immolate-to-jimmolate` skill
  (`../.claude/skills/immolate-to-jimmolate/`).
- **Work in this repo** → `../CLAUDE.md` (build/test commands, architecture, hard rules).
- **What's planned / known footguns** → `../TODO.md`.

## How the engine fits together (one paragraph)

A filter is an `IMotelySeedFilterDesc` that creates an `IMotelySeedFilter`; its
`Filter(ref MotelyVectorSearchContext)` returns a `VectorMask` of which of the 8 SIMD lanes
survived (see `MotelySearch.cs`). There are two parallel views of a seed's game generation:
the **vector** path (`MotelyVectorSearchContext.*.cs`, fast, many seeds at once) and the
**scalar** path (`MotelySingleSearchContext.*.cs`, one seed at a time) — both split into
partial classes per game domain (Jokers, Shop, Tarot, Vouchers, Tags, Packs, Planets…).
JAML clauses compile into vector filters; Jimmolate predicates run on the scalar path over
base-filter survivors. PRNG/seed math lives in `SeedMath.cs`, `LuaRandom.cs` /
`VectorLuaRandom.cs`, and `MotelyPrngKeys.cs`.

> **Build, don't run.** A full search is enormously expensive. To verify changes, build
> (`dotnet build ../Motely.slnx -c Release`) and run the bounded tests
> (`dotnet test ../Motely.Tests/Motely.Tests.csproj`) — `JamlyzerUnitTests` has known-seed
> ground-truth assertions. Don't kick off a search to "check."
