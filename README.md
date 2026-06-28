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
- `Motely.Data/` — data/results tooling.
- `Motely.Tests/` — the test project.

Other top-level items: `JamlFilters/` (pre-made `.jaml` filter configs) and
`Seeds/`.

## JAML

Filters are written in **JAML** — *Jimbo's Ante Markup Language*. JAML is a real
language: its own vocabulary, grammar, and validator. It has two surface syntaxes
— YAML and JSON — that both parse to the same `JamlConfig`. YAML is just the most
comfortable way to write JAML down; it is not what JAML *is*.

See `JamlFilters/` for ready-made filters to copy and adapt.

### Language validation

JAML is validated by the Motely engine when filters are loaded and when search
plans are built. A typo'd joker name, an unknown clause key, or a bad rank should
fail loudly instead of being silently softened into a different filter. The C#
engine/domain model is the source of truth for clause meaning, source paths, and
PRNG behavior.

## Finding a Balatro Seed

Quick start:
```powershell
dotnet run --project Motely.CLI -- --jaml yourfilter --keyword YOURNAME --cutoff 0
```

The filter lives in `JamlFilters/yourfilter.jaml`. Run from the repo root so
`--jaml` resolves correctly.

## Working with agents

`AGENTS.md` is the authoritative instruction file for AI agents working in this
repo. `CLAUDE.md` intentionally contains only `@AGENTS.md` so Claude loads the
same instructions. This README is the human-facing source of truth for what
Motely is and how to build it.
