# MotelyJAML agent instructions

MotelyJAML is a real, working production codebase: a vectorized SIMD Balatro seed-search engine with JAML (Jimbo's Ante Markup Language) filters, CLI/TUI heads, data tooling, and tests. Do not describe it as a toy, demo, prototype, or speculative project. Treat JAML filters as executable user intent.

## Current project facts

- Solution: `Motely.slnx`.
- Core engine: `Motely/`.
- CLI: `Motely.CLI/`.
- TUI: `Motely.TUI/`.
- Data/results tooling: `Motely.Data/`.
- Tests: `Motely.Tests/`.
- Filter corpus: `JamlFilters/`.
- JAML is the language. YAML and JSON are surface syntaxes that load to `JamlConfig`; YAML is not the source of truth.
- The engine/domain model is the source of truth for PRNG paths, sources, filters, scoring, and packed item identity.

## Required agent behavior

- Preserve user agency and executable intent. Do not soften, reinterpret, simplify, or caretaker-transform JAML filters before running or validating them.
- If a filter is invalid, fail loudly with the exact load/build error. Do not silently drop unknown keys or invent fallback semantics.
- If a search did not actually run, say it did not run. Never imply seeds were found by a mock, plan, or explanation.
- Prefer real verification: build, focused tests, corpus tests, and actual CLI/search runs when requested.
- When editing JAML/JUMMY/source behavior, inspect existing filter descs and scoring paths first. PRNG/source paths are domain-specific; do not flatten distinct paths because names look similar.
- Do not introduce parallel vocabularies or semantic key tables unless the user explicitly asks. If a loader needs key handling, keep it minimal and tied to existing domain types.
- Do not add generic parser sprawl. JAML should remain a language whose semantics are owned by the engine.

## JAML design constraints

- `boosterPacks: [...]` is a core source shorthand and must be supported where the corresponding source config has booster pack slots.
- `requireMega` / `requireMegaPack` means only count source matches if the referenced booster pack is Mega-sized.
- `smallBlindTag` and `bigBlindTag` are source aliases for tag rolls:
  - `smallBlindTag` => roll/source `[0]`
  - `bigBlindTag` => roll/source `[1]`
- `joker:` is the mixed joker clause. Legendary jokers may appear by name, but ordinary joker paths and legendary/Soul paths are not the same PRNG/source path. Do not naively flatten legendary jokers into ordinary joker source checks.
- Starting draw support is intentionally narrower than all future desired functionality. Treat current behavior as rank/suit per round unless code/tests say otherwise.
- Clause `Sources` defaults are owned by filter desc/scoring code. A missing `sources:` block means the co-located `DefaultSources` apply; an explicit `sources:` block overrides wholesale.

## JUMMY design constraints

- JUMMY is one human line = one JAML criterion.
- For packed item families, preserve canonical identity through `MotelyItem`, `FormatUtils.FormatItem`, and `FormatUtils.TryParseMotelyItem`.
- Do not replace packed-int identity with a second phrase parser universe.

## Real caveats, not self-sabotage

- The current JAML loader exists to keep production green, but loader internals are not the semantic source of truth.
- Some editor/LSP/schema tooling referenced in older commits was removed. Do not assume `jaml-lang`, `jaml-lsp`, `Motely.Schema`, or `JamlVocab.cs` exist unless the working tree contains them.
- Data/drown paths may force single-threaded reads for provider safety; that is a real operational warning, not a sign the engine is unfinished.

## Verification commands

```powershell
dotnet build "D:\MotelyJAML\Motely.Tests\Motely.Tests.csproj" -v q
dotnet test "D:\MotelyJAML\Motely.Tests\Motely.Tests.csproj" -v q
dotnet test "D:\MotelyJAML\Motely.Tests\Motely.Tests.csproj" --filter "FullyQualifiedName~JummyLineTests" -v q
```

For CLI smoke runs, prefer explicit JAML and search mode arguments so the command proves real execution rather than only parsing.
