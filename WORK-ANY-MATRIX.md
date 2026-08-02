# WORK — Category any matrix (empty list law)

**Operator:** Nat  
**Author:** Grok (healed 2026-07-31) · **folded 2026-08-01** into `CLAUDE-BITES-MATRIX.md` (E01–E21 SHIPPED; open work = U/H/X tracks there)  
**Law:** one grammar; **empty disc list / empty props = category match**. No `Any` token. No `IsWildcard` flag.  
**Proof:** `dotnet test Motely.Tests` green + real seed for empty spectral/planet/joker paths.  
**Executor queue:** do **not** invent a parallel Grok matrix — open bites live in `CLAUDE-BITES-MATRIX.md` only.

---

## Product law (shipped)

| Fact | Shape |
|------|--------|
| Category any | Empty discriminator value: `joker: []`, `tarotCard: []`, `spectralCard: []`, `planetCard: []`, rarity jokers same, `legendaryJoker: []` |
| Props-only | `joker: []` + `edition: Negative` = any Negative joker; `standardCard:` + `suit: Hearts` = any Hearts |
| `standardCard` | Always props-shaped: null rank/suit/seal/edition/enhancement = don’t-care |
| Token `Any` / `any` / `ANY` | **Rejected** as a bad enum name (not a wildcard) |
| Line form | Named items only (`Perkeo`, `Red Seal … King of Hearts`). **No** bare `Any` line |
| Default sources | Still **shop 0–7** when `sources:` omitted (ordinary jokers/tarot/spectral/planet/standard) |
| TheSoul / BlackHole | Named only → special path + pack defaults. Empty spectral list does **not** count Soul/BH |
| SoulCardOnly | Separate flag on legendary — do not overload empty list |
| `IsWildcard` | **Deleted** everywhere |

---

## Wire examples

```yaml
# any joker, shop default, antes 1–8 after builder fill
must:
  - joker: []

# any rare joker with edition filter
must:
  - rareJoker: []
    edition: Foil
    antes: [1]

# any ordinary spectral (not Soul/BH)
must:
  - spectralCard: []
    antes: [1, 2, 3, 4]

# any planet
must:
  - planetCard: []

# any playing card / suit-only
must:
  - standardCard:
  - standardCard:
      suit: Hearts
```

Writer emits empty arrays for empty lists (`joker: []`), never the string `Any`.

---

## Status

| Slice | Status |
|-------|--------|
| Kill `IsWildcard` | **done** |
| Empty disc parse all ValueEnum families | **done** |
| Match/scoring empty → category | **done** (joker + rarity + legendary + tarot + spectral + planet) |
| Writer empty array | **done** |
| Line: no `Any` | **done** |
| LSP: no `Any` completion | **done** |
| Token `Any` rejected | **done** + test |
| spectral empty proof (ALEEB Ghost shop Sigil) | **done** |
| planet empty sequential proof | **done** |
| joker empty list proof | **done** |
| `JAML.md` | **done** |
| jaml-ui pickers / sprites for empty | **open** (UI track — not engine) |

---

## Non-goals

| Park | Why |
|------|-----|
| jaml-ui moniker redesign | separate track |
| Adding `Any` to C# enums | pollutes pools |
| Changing shop-default to all sources | silent behavior bomb |
| Text-only filter language rewrite | product call later |

---

## Grep gates (must stay green)

```sh
rg -n 'IsWildcard' Motely Motely.Tests --type cs   # expect 0
rg -n 'joker: Any|tarotCard: Any|StringArrayNode\(\[\"Any\"\]\)' Motely Motely.Tests --type cs
dotnet test Motely.Tests/Motely.Tests.csproj --nologo
```

---

## Burn line

> Empty list is the wildcard. Props filter the category. No fake `Any` token. Ship diffs, not hedges.
