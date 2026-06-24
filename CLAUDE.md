# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# MotelyJAML

Fork of tacodiva's **Motely** — a 512-bit SIMD Balatro seed-search engine (`Motely/`).
JAML (Jimbo's Ante Markup Language) is the declarative filter language on top — its
own vocabulary and grammar, written in YAML or JSON syntax.
.NET 10 (`global.json` pins 10.0.204, `rollForward: latestFeature`). Solution: `Motely.slnx`.

## Working rules

- Small, reversible diffs. Build on what exists.
- No version bumps unless asked.
- Done means it builds and the relevant tests pass.
- PowerShell on this machine. Prefer the read/edit/grep tools over shelling out.

## Build & test

```powershell
dotnet build Motely.slnx
dotnet test Motely.Tests/Motely.Tests.csproj                                  # full suite
dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~JummyLineTests"  # one class
```

Run a search from the CLI (from repo root so `--jaml` resolves):

```powershell
dotnet run --project Motely.CLI -- --jaml yourfilter --keyword YOURNAME --cutoff 0
```

The filter lives in `JamlFilters/yourfilter.jaml`. Full authoring guide:
`FIND_BALATRO_SEED_WITH_MOTELY_CLI.md`.

## Projects (`Motely.slnx`)

- `Motely/` — core engine: vectorized SIMD seed search, filters, JAML loader.
- `Motely.CLI/` — command-line head.
- `Motely.TUI/` — terminal UI head.
- `Motely.Wasm/` — Bootsharp WASM head (Jimmolate JS probe + JAML/seed/search API in `Program.cs`); publishes a single-file module to `dist/`.
- `Motely.Schema/` — projects `JamlVocab` to TS/JSON/grammar artifacts (see below).
- `Motely.Data/` — data/results tooling.
- `Motely.Tests/` — the test project.

Non-project dirs: `JamlFilters/` (ready-made `.jaml`), `jaml-lang/` (language service),
`jaml-lsp/` (LSP server + VS Code extension), `Seeds/`, `corpus/`, `docs/balatro-mechanics.md`.

## Single source of truth for grammar

`Motely/Filters/Jaml/JamlVocab.cs` is the one source of truth for JAML grammar
(root keys, discriminators, per-discriminator clause keys + source keys, enum tables).

- `JamlConfigLoader.cs` derives its allow-lists from `JamlVocab` — no hand-maintained HashSets.
- `Motely.Schema` projects `JamlVocab` → `jaml-lang/src/generated.ts`,
  `jaml-lsp/schemas/jaml.schema.json`, `jaml-lsp/syntaxes/jaml.tmLanguage.json`.
- To add or change a clause: edit `JamlVocab` and write the FilterDesc. The generated
  TS/JSON/grammar are outputs, not inputs — never hand-edit them.

Generation runs on every `Motely.Wasm` build; run by hand with `node jaml-lang/generate.mjs`.

## Filter architecture

Filters live in `Motely/Filters/`:

- `Jaml/` — clauses driven by JAML config. `AnteCards/` (joker, tarot, planet, spectral,
  standard card, legendary/soul), `AnteFeatures/` (voucher, tag, boss, starting draw),
  `Events/` (lucky money/mult, wheel of fortune, payouts, extinctions, level-ups).
- `Native/` — hand-written C# FilterDescs (Perkeo/observatory, soul jokers, erratic, etc.).
- `LogicClause.cs`, `Native/AndFilterDesc.cs`, `Native/OrFilterDesc.cs`, `NegationFilterDesc.cs` — boolean composition of clauses (must / should / mustNot).

## JUMMY

One JUMMY line = one JAML criterion: the descriptive string the game/analyzer prints plus
an ante tail. Example — `Eternal Blueprint in antes 1 or 2` equals:

```yaml
- joker: Blueprint
  stickers: [Eternal]
  antes: [1, 2]
```

A `MotelyItem` is a single packed `int` (`Motely/MotelyItem.cs`); type/edition/enhancement/
seal/stickers live in its bits. `FormatUtils.FormatItem(int)` and
`FormatUtils.TryParseMotelyItem(string)` are inverses, so the item half round-trips losslessly.
JUMMY adds the tail. Implementation: `Motely/Filters/Jummy/JummyLine.cs`
(`FromClause` clause→line, `TryToClause` line→clause); tests in `Motely.Tests/JummyLineTests.cs`.
