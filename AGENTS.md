# MotelyJAML agent instructions

MotelyJAML is a vectorized SIMD Balatro seed-search engine in active production use: JAML (Jimbo's Ante Markup Language) filters, CLI/TUI heads, data tooling, and tests. JAML filters are executable user intent — run them as written.

## Current project facts

- Solution: `Motely.slnx`.
- Core engine: `Motely/`.
- CLI: `Motely.CLI/`.
- TUI: `Motely.TUI/`.
- Data/results tooling: `Motely.Data/`.
- Tests: `Motely.Tests/`.
- Filter corpus: `JamlFilters/`.
- JAML is the language. YAML and JSON are surface syntaxes that load to `JamlConfig`; the engine/domain model is the source of truth for PRNG paths, sources, filters, scoring, and packed item identity.

## Tooling — mandatory for every agent and subagent

- This is a C# codebase. Navigate it with the **roslyn-lens** MCP tools (`find_symbol`, `find_references`, `find_implementations`, `get_type_overview`, `get_file_overview`, `get_diagnostics`, etc.). Roslyn understands the semantic model; text search does not. Use it to find symbols, callers, overrides, and type shapes before editing.
- Use proper Claude Code tools for files: **Read, Edit, Write, Grep, Glob**. Do **not** shell out to `cat`, `grep`, `sed`, `awk`, `head`, or `tail` through Bash for reading/searching/editing — those are banned here.
- Bash/PowerShell is for running things (build, test, search runs, git), not for inspecting code.
- This applies to spawned subagents too: pass these rules along. An agent that reaches for `grep`/`cat` instead of roslyn-lens + Read/Grep is doing it wrong.

## How to work here

- Run JAML filters exactly as written. If you think a filter is wrong, run it as-is first, then say so — executing the user's intent comes before critiquing it.
- When a filter is invalid, fail loudly with the exact load/build error so the user sees what broke. Unknown keys surface as errors; semantics come from the engine, not invented fallbacks.
- Report search results truthfully: if a search ran, say what it found; if it didn't run, say that plainly.
- Verify for real — build, focused tests, corpus tests, and actual CLI/search runs when asked.
- Before editing JAML/JUMMY/source behavior, read the existing filter descs and scoring paths. PRNG/source paths are domain-specific; keep distinct paths distinct even when names look alike.
- Keep loader key-handling minimal and tied to existing domain types. JAML's semantics live in the engine — extend the language there. Add new vocabularies or semantic key tables only when the user explicitly asks for them.

## JAML design constraints

- `boosterPacks: [...]` is a core source shorthand, supported wherever the corresponding source config has booster pack slots.
- `requireMega` / `requireMegaPack` counts a source match only when the referenced booster pack is Mega-sized.
- `smallBlindTag` and `bigBlindTag` are source aliases for tag rolls:
  - `smallBlindTag` => roll/source `[0]`
  - `bigBlindTag` => roll/source `[1]`
- `joker:` is the mixed joker clause. Legendary jokers may appear by name, but ordinary joker paths and legendary/Soul paths are separate PRNG/source paths — keep them separate in source checks.
- Starting draw support is intentionally narrower than the full desired feature set. Treat current behavior as rank/suit per round unless code/tests say otherwise.
- Clause `Sources` defaults are owned by filter desc/scoring code. A missing `sources:` block applies the co-located `DefaultSources`; an explicit `sources:` block overrides wholesale.

## JUMMY design constraints

- JUMMY is one human line = one JAML criterion.
- For packed item families, preserve canonical identity through `MotelyItem`, `FormatUtils.FormatItem`, and `FormatUtils.TryParseMotelyItem` — packed-int identity stays canonical rather than a second phrase-parser universe.

## Operational caveats

- The current JAML loader keeps production green; the engine/domain model remains the semantic source of truth.
- Check the working tree before assuming `jaml-lang`, `jaml-lsp`, `Motely.Schema`, or `JamlVocab.cs` exist — older commits reference tooling that was since removed.
- Data/drown paths may force single-threaded reads for provider safety. That's a real operational constraint for correctness, working as intended.

## Verification commands

```powershell
dotnet build "D:\MotelyJAML\Motely.Tests\Motely.Tests.csproj" -v q
dotnet test "D:\MotelyJAML\Motely.Tests\Motely.Tests.csproj" -v q
dotnet test "D:\MotelyJAML\Motely.Tests\Motely.Tests.csproj" --filter "FullyQualifiedName~JummyLineTests" -v q
```

For CLI smoke runs, prefer explicit JAML and search mode arguments so the command proves real execution rather than only parsing.
