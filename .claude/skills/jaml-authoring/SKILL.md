---
name: jaml-authoring
description: JAML filter language reference — clause semantics, JUMMY one-liners, and the vocabulary API. Read before writing or editing any .jaml filter.
user-invocable: false
---

# Authoring JAML

JAML (Jimbo's Ante Markup Language) is the filter language. YAML and JSON are both concrete syntaxes; both load to the same typed `JamlConfig` (`Motely/Filters/Jaml/JamlConfig.cs`). Validation is loud — every key is checked at load, so a valid file is a correct file.

## Shape

```yaml
name: example          # becomes the filterId (normalized) — names the seed lake CSV
deck: Red
stake: White
seeds: [AAAAAAAA]      # optional saved seeds
must:                  # every clause required — each appends a SIMD filter
  - voucher: Overstock
    antes: [1]
should:                # weighted scoring — installs JamlShouldScoreDesc
  - joker: Showman
    antes: [1, 2]
    score: 5           # weight; label: names the tally column
mustNot:               # exclusions — each wraps in NegationFilterDesc
  - boss: TheNeedle
    antes: [1]
```

- **must** narrows: seeds pass every clause or leave the stream.
- **should** scores: `Σ tally × weight`, cutoff via CLI `--cutoff` or host API.
- **mustNot** excludes.
- A JAML with zero clauses (deck/stake/seeds only) is a first-class search when a host predicate (Jimmolate) carries the decision.
- `with:` on a clause carries modifiers (`luck`, `vouchers`) that change roll odds.

## Vocabulary — ground every name

Item names come from the engine enums. `MotelyJaml.ListItems(kind, query)` (WASM) serves them live; in C#, the enums under `Motely/Enums/` are the source. Kinds include joker, voucher, tag, boss, tarot, planet, spectral. Substring match is case-insensitive. Check a name there before writing it into a clause.

## JUMMY — one human line per clause

`Motely/Filters/Jummy/JummyLine.cs` parses/formats the terse spelling:

```
Eternal Blueprint in antes 1 or 2
Red Seal Polychrome Steel King of Hearts in ante 1
```

`MotelyJaml.ValidateLine(line)` returns null when valid; `CanonicalizeLine` normalizes spelling. Round-trip through these to keep packed item identity canonical.

## Testing a filter

```sh
dotnet run --project Motely.CLI -- --jaml <file> --endBatch 100   # quick sweep
dotnet run --project Motely.CLI -- --analyze SEED --jaml <file>   # per-seed breakdown
```

The corpus lives in `JamlFilters/`; `JamlCorpusLoaderTests` keeps every file there loading, so a new filter added to the corpus gets tested for free.
