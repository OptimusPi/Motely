# Motely (MotelyJAML)

Motely is a vectorized (SIMD) seed-search engine for **Balatro**. Filters are
authored in **JAML** (Jimbo's Ante Markup Language).

## Build

```powershell
dotnet build Motely.slnx
```

## Test

```powershell
dotnet test Motely.Tests/Motely.Tests.csproj
dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~SomeTestName"
```

## Toolchain

- .NET 10 SDK, pinned to `10.0.204` in `global.json` (`rollForward: latestFeature`).

## Layout

The solution is `Motely.slnx`. Projects:

- `Motely/` — the core engine (vectorized SIMD seed search).
- `Motely.CLI/` — command-line head.
- `Motely.TUI/` — terminal UI head.
- `Motely.Wasm/` — WebAssembly head (Bootsharp interop).
- `Motely.DataLake/` — data/results tooling.
- `Motely.Tests/` — the test project.

Other top-level items: `JamlFilters/` (pre-made `.jaml` filter configs),
`jaml-lang/` (the JAML language), `Seeds/`, and `docs/`
(`balatro-mechanics.md`).

## JAML

Filters are written in JAML — a YAML-based config describing what to look for in a
seed. See `JamlFilters/` for ready-made filters to copy and adapt, `jaml-lang/`
for the language itself, and `docs/balatro-mechanics.md` for the game mechanics
filters target.

## Working with agents

`CLAUDE.md` covers how AI agents should work in this repo (consent, running
policy, the Bootsharp docs). This README is the human-facing source of truth for
what Motely is and how to build it.
