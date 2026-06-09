# Analyzer design — READ THIS BEFORE TOUCHING THE ANALYZER

This has been re-explained ~14 times. It is now written down. If you are an agent
about to "improve", "DTO-ify", "consolidate", or "modernize" the analyzer: **stop and
read.** The split below is intentional. Collapsing it is the recurring mistake.

## Two analyzers. They are NOT the same thing. They do NOT share a backend.

### 1. `MotelyLegacyTextAnalyzer` — the unit-test ground truth. RAW C#. STRINGS.

- Produces a **flat text block** (`.ToString()`), like the Balatro Blueprint "Text" dump:
  boss / tags / voucher / shop queue / packs, per ante.
- This text **IS** the test oracle. `AnalyzerUnitTests` / Verify() diff this string
  character-for-character against pinned ground truth.
- It must walk **EVERY stream** exposed by `MotelySingleSearchContext.*.cs` — every card
  source the game has (shop, every booster pack type, vouchers, tags, bosses, soul/legendary,
  wheel-of-fortune, aura, seance, sixth sense, certificate, riff-raff, etc. — the full
  "Card Sources" list). A source missing from the walk = a silent hole in the ground truth.
- **DO NOT** turn this into a DTO. **DO NOT** route it through Jamlyzer. Its value is the
  raw string. The strings are the contract. Touching the format breaks the unit tests by design.

### 2. `Jamlyzer` — FOR THE UI. A snapshot DTO. Nothing else cares.

- Existed to produce a structured snapshot (antes → shop queue / packs / tag-granted jokers /
  soul jokers, each item carrying `IsHighlighted` + `MatchedBy`) for a UI to render glow.
- It is **presentation only**. No unit test should depend on it. It is not ground truth.
- **The mistake (the "incident"):** a previous pass made `Jamlyzer` build on top of
  `MotelyLegacyTextAnalyzer` and tried to migrate the legacy strings into Jamlyzer's DTO.
  That couples the test oracle to a UI type and is exactly backwards. Ground truth must not
  depend on a UI DTO.

## Current state (this rip)

`Jamlyzer` / `JamlyzerOptions` / `JamlyzerSnapshot` and the `AnalysisJsonContext` JSON
source-gen were **ripped out**, along with:

- `Motely.CLI` `--output-json` flag, the `RunJamlyze` lens path, and all JSON branches
  (the CLI `--analyze` path now emits only the legacy human-readable text).
- The `Motely.Wasm` `Jamlyzer` `[Export]`.
- `Motely.Tests/JamlyzerUnitTests.cs` (it pinned the wrong, UI-coupled subsystem).

`MotelyLegacyTextAnalyzer` stays as the single source of analyzer truth.

## If/when the UI snapshot comes back

Rebuild it as an **independent** walk over `MotelySingleSearchContext` that emits a clean DTO.
It may share the per-seed walk primitives, but it must **not** be implemented in terms of the
legacy text analyzer, and the legacy text analyzer must **not** be implemented in terms of it.
Two outputs (strings for tests, DTO for UI), one underlying single-seed walk — never one
stacked on the other.
