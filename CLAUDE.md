# MotelyJAML

Fork of tacodiva's **Motely** — a 512-bit SIMD Balatro seed-search engine (`Motely/`).
JAML (Jimbo's Ante Markup Language) is the declarative YAML filter language on top.
.NET 10 (`global.json` pins 10.0.204). Solution: `Motely.slnx`.

## Working rules

- Small, reversible diffs. Build on what exists.
- No version bumps unless asked.
- Done means it builds and the relevant tests pass.
- PowerShell on this machine. Prefer the read/edit/grep tools over shelling out.

## Single source of truth for grammar

`Motely/Filters/Jaml/JamlVocab.cs` is the one source of truth for JAML grammar
(root keys, discriminators, per-discriminator clause keys + source keys, enum tables).

- `JamlConfigLoader.cs` derives its allow-lists from `JamlVocab` — no hand-maintained HashSets.
- `Motely.Schema` projects `JamlVocab` → `jaml-lang/src/generated.ts`,
  `jaml-lsp/schemas/jaml.schema.json`, `jaml-lsp/syntaxes/jaml.tmLanguage.json`.
- Add or change a clause: edit `JamlVocab` and write the FilterDesc. The generated
  TS/JSON/grammar are outputs of it, not inputs.

### Open
- `jaml-lang/src/context.ts` hardcodes its own discriminator Set (~lines 87-95) instead of
  importing `Discriminators` from `generated.js`. Dedupe.
- `Motely.Schema` is in no build step → generated artifacts can go stale silently. Add a
  `Motely.Tests` drift test that regenerates in memory and fails if committed output differs.
- VS Code validation: lean on generated `jaml.schema.json` via `redhat.vscode-yaml`; keep
  `jaml-lang`'s `validate()` for the CodeMirror web editor.

## JUMMY

One JUMMY line = one JAML criterion: the descriptive string the game/analyzer prints, plus
an ante tail. Example — `Eternal Blueprint in antes 1 or 2` equals:
```yaml
- joker: Blueprint
  stickers: [Eternal]
  antes: [1, 2]
```

A `MotelyItem` is a single packed `int` (`Motely/MotelyItem.cs`);
type/edition/enhancement/seal/stickers live in its bits. `FormatUtils.FormatItem(int)` and
`FormatUtils.TryParseMotelyItem(string)` are inverses (pretty name first, enum fallback), so
the item half round-trips losslessly. JUMMY adds the tail.

### Done (`JummyLineTests` 21/21 green)
- `Motely/Filters/Jummy/JummyLine.cs` — v0 covers the packed-int families: jokers (edition +
  stickers) and consumables tarot/spectral/planet.
  - `FromClause(JamlClauseBase) -> string?` (clause → line)
  - `TryToClause(string, out clause, out error)` (line → clause)
  - Joker enum recovery reuses the engine constructor (`new MotelyItem(j).Type == item.Type`).
  - Consumables: `MotelyItemType` shares member names per category enum, so
    `Enum.TryParse<MotelyTarotCard>(item.Type.ToString())` recovers the card. No bit-math.
  - Tail grammar: `in ante N` / `in antes A or B` (comma and `or` interchangeable on input;
    output canonicalizes to `or`). Wildcard joker: `Any`.
- `Motely.Tests/JummyLineTests.cs` — the example, modifier round-trips, full sweeps over every
  joker + tarot/spectral/planet, and the packed-int identity law.

### Next
- Standard cards: `StandardCardClause` is rank/suit/enh/seal with partial matches, so it
  doesn't map to one packed item cleanly. Standard-card bit offsets live in `MotelyGlobals`.
- Voucher / tag / boss: separate enums via `FormatVoucher`/`FormatTag`/`FormatBoss`; their
  clauses have a `required Rolls` array needing a sane default.
- Edition on consumables (e.g. `Negative The Fool`) is dropped — clauses don't store it.
- Source tail (`from shop`, `from booster pack`) → clause `Sources`.
- Whole-document `.jummy` ⟷ `.jaml` converter (each line ↔ one clause under
  must/should/mustNot), then wire a head (CLI `--jummy`, WASM). A dead `.jummy` language id
  already sits in `jaml-lsp/package.json` with no grammar.

## Verify

```powershell
dotnet build Motely/Motely.csproj
dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~JummyLineTests"
dotnet test Motely.Tests/Motely.Tests.csproj    # full suite
```

## Motely.Wasm
Bootsharp WASM module — the Jimmolate JS probe + the JAML/seed/search API, in `Program.cs`.
`Motely.Wasm.csproj` publishes the single-file module to `dist/`, where `test.mjs` boots it
and checks `Motely.getVersion()` / `normalizeSeed()`. Fuller reference build (FileSystem feed,
package.json, smoke tests): `D:/MotelyJAML2`.
