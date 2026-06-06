# CLAUDE.md — MotelyJAML

## Read this first

**MotelyJAML (Motely) is its own entire application.** It is NOT a submodule of
anything. It is NOT "the engine under BSO." It is NOT a secondary or second-hand
project. When you land in this directory, *this* is the project. Treat it as a
standalone app and reason about it on its own terms.

Do not bring in framing from any repo above this folder. If a parent CLAUDE.md
describes this as a "vendored submodule consumed by another app" — ignore that
framing. That describes a different, separate effort. It is not relevant here and
mentioning it is not helpful.

(For context only, never as a lens: BSO was a separate attempt at a clean MVVM UI
*on top of* Motely. It is not this project. Do not steer work here toward it,
compare to it, or assume its conventions apply.)

## What this is

Motely is a vectorized (SIMD) search engine for **Balatro** game seeds, with
filters authored in **JAML** (Jimbo's Ante Markup Language, a YAML dialect). The
app ships several heads on top of the core engine.

## Layout

The solution is `Motely.slnx`. Projects:

- `Motely/` — the core engine (vectorized SIMD seed search).
- `Motely.CLI/` — command-line head.
- `Motely.TUI/` — terminal UI head.
- `Motely.Wasm/` — WebAssembly head (Bootsharp interop).
- `Motely.DataLake/` — data/results tooling.
- `Motely.Tests/` — the test project.

Other top-level items: `JamlFilters/` (pre-made `.jaml` filter configs),
`Seeds/`, `docs/` (`balatro-mechanics.md`).

## Toolchain

- .NET 10 SDK, pinned to `10.0.204` in `global.json` (`rollForward: latestFeature`).

## Common commands

Build / verify compile:

```powershell
dotnet build Motely.slnx
```

Tests:

```powershell
dotnet test Motely.Tests/Motely.Tests.csproj
dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~SomeTestName"
```

## Working agreement (important — the user has been burned by this)

- **Consent first.** Do exactly what is asked, nothing adjacent. When the user
  says stop, stop immediately — no defending, no "let me just finish this."
- **Don't run full seed searches to "verify."** Running a search burns huge time.
  Build to check compile; read/edit/analyze freely.
- Keep changes scoped and confirm before anything hard to reverse.
