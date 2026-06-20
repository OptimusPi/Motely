# MotelyJAML — Agent Context

Motely is a vectorized (SIMD / AVX-512) Balatro seed-search engine written in C#.
Filters are authored in **JAML** (Jimbo's Ante Markup Language), a YAML-based DSL.
This file is the agent quick-start. Read it once; it covers everything you need.

---

## Repo layout

```
Motely/                    — core engine (C#, do not edit without a test)
  Enums/                   — canonical spellings for every game entity
  Filters/Jaml/            — JAML loader + models
corpus/                    — RAG corpus: one tight .jaml file per item/event
  jokers/        bosses/   consumables/   vouchers/
  tags/          cards/    decks/         events/
docs/                      — reference docs (encyclopedia, research, mechanics)
JamlFilters/               — published user-facing filters
jaml-lang/                 — JAML language package (TypeScript)
```

---

## Ground truth — use these, in priority order

| Source | Use for |
|--------|---------|
| `d:\Balatro\game.lua` | Item centers, `config`, costs, rarity, `unlock_condition`, `enhancement_gate`, voucher `requires`, tag `min_ante` |
| `d:\Balatro\functions\common_events.lua` | Pool availability: Showman duplicate bypass, voucher prerequisites, tag gates, enhancement gates, hidden Soul/Black Hole, Gros Michel/Cavendish flags |
| `d:\Balatro\card.lua` | Runtime joker/consumable behavior, probability changes, scoring hooks, clone/sell/order interactions |
| `d:\Balatro\localization\en-us.lua` | Canonical player-facing effect/unlock wording after mechanics are checked |
| `Motely/Enums/*.cs` | **Exact enum spellings** for every item |
| Official Balatro wiki via EXA | Broad mechanics/fun-combo discovery and player language; verify facts against Lua |
| `docs/Balatro_Master_Encyclopedia.md` | Secondary reference only after Lua agrees |

**Never use** `d:\JAMMY\data\BalatroKnowledge\JokerReference.md` — known wrong values.

---

## Enum file index

| You need the spelling for… | Look in |
|---------------------------|---------|
| Jokers (all 150) | `Motely/Enums/MotelyJokers.cs` |
| Boss blinds + min-ante | `Motely/Enums/MotelyBossBlind.cs` |
| Decks | `Motely/Enums/MotelyDeck.cs` |
| Stakes | `Motely/Enums/MotelyStake.cs` |
| Tarot cards | `Motely/Enums/MotelyTarotCard.cs` |
| Planet cards | `Motely/Enums/MotelyPlanetCard.cs` |
| Spectral cards | `Motely/Enums/MotelySpectralCard.cs` |
| Vouchers | `Motely/Enums/MotelyVoucher.cs` |
| Tags | `Motely/Enums/MotelyTag.cs` |
| Editions / Enhancements / Seals | `Motely/Enums/MotelyItemEdition.cs`, `MotelyItemEnhancement.cs`, `MotelyItemSeal.cs` |

JAML clause schema (all valid clause keys): `Motely/Filters/Jaml/JamlConfigLoader.Models.cs`

---

## Corpus authoring rules

### Clause shape

```yaml
must:
  - joker: JokerName          # generic key for ALL jokers regardless of rarity
    antes: [1]                # TIGHT: Ante 1 first shop only
    sources:
      shopItems: [0, 1, 2, 3]
      boosterPacks: [0, 1]
should:
  - joker: SynergyPartner
    antes: [1, 2, 3]
    score: 45
```

- **`joker: <Name>`** always — never `commonJoker`, `uncommonJoker`, `rareJoker`.
- **`antes: [1]`** in every `must` clause. No broad ranges like `[1, 2, 3]`.
- **`should`** scores reflect real synergy strength; include a `label:` for non-obvious pairings.
- **`mustNot`** only for hard build-killers (e.g. boss that debuffs the joker's suit).

### Source rules by item type

| Item type | Correct sources |
|-----------|----------------|
| Regular jokers | `shopItems: [0,1,2,3]`, `boosterPacks: [0,1]` |
| Legendary jokers (Canio/Triboulet/Yorick/Chicot/Perkeo) | `arcanaPacks: [0,1]`, `spectralPacks: [0,1]` |
| Tarot cards | `shopItems: [0,1,2,3]`, `boosterPacks: [0,1]` |
| Spectral cards | `boosterPacks: [0,1]` only — **not shopItems** |
| Planet cards | `shopItems: [0,1,2,3]`, `boosterPacks: [0,1]` |
| Vouchers | `shopItems: [0,1,2,3]` |

### Event keys (use in `must` or `should` clauses)

`luckyMoney`, `luckyMult`, `misprintMult`, `wheelOfFortune`, `cavendishExtinct`,
`grosMichelExtinct`, `spaceLevelup`, `businessPayout`, `bloodstoneTrigger`,
`parkingPayout`, `glassDestroy`, `wheelStaysFlipped`, `judgement`, `wraith`

---

## Tarot → card enhancement reference

Common mistake: agents confuse which tarot creates which enhancement. Authoritative list:

| Tarot | Creates | Notes |
|-------|---------|-------|
| The Magician | **Lucky** card (×2 cards) | 1/5 money trigger, 1/15 mult trigger |
| The Empress | **Mult** card (×2 cards) | +4 Mult when scored |
| The Hierophant | **Bonus** card (×2 cards) | +30 Chips when scored |
| The Lovers | **Wild** card (×1) | counts as any suit |
| The Chariot | **Steel** card (×1) | ×1.5 Mult while held in hand |
| Justice | **Glass** card (×1) | ×2 Mult on score; 1/4 chance to destroy |
| The Devil | **Gold** card (×1) | +$3 if held at round end |
| The Tower | **Stone** card (×1) | +50 Chips; no rank/suit |
| Strength | Raises rank by 1 (×2 cards) | Queens → Kings, Aces → 2s |
| Death | Copies right card onto left (×2 selected) | retains seal/edition/enhancement |
| The Hanged Man | Destroys (×2 cards) | deck thinning |

Aura (spectral) adds an **edition** (Foil/Holo/Poly) — not an enhancement.
Deja Vu (spectral) adds a **Red Seal** — not an enhancement.

---

## Boss blind minimum ante

From `Motely/Enums/MotelyBossBlind.cs` (`BossRequiredAnteOffset` flags):

| Min ante | Bosses |
|----------|--------|
| 1 | TheClub, TheGoad, TheHead, TheHook, TheManacle, ThePillar, ThePsychic, TheWindow |
| 2 | TheArm, TheFish, TheFlint, TheHouse, TheMark, TheMouth, TheNeedle, TheWall, TheWater, TheWheel |
| 3 | TheEye, TheTooth |
| 4 | ThePlant |
| 5 | TheSerpent |
| 6 | TheOx |
| Ante 8 only | AmberAcorn, CeruleanBell, CrimsonHeart, VerdantLeaf, VioletVessel (finishers) |

Use `antes: [N]` in boss `must` clauses matching the min ante above.

---

## Cavendish / Gros Michel note

Cavendish only appears after Gros Michel goes extinct. The `must` clause structure:

```yaml
must:
  - grosMichelExtinct: [1]
  - joker: Cavendish
    antes: [2]
    sources:
      shopItems: [0, 1, 2, 3]
      boosterPacks: [0, 1]
```

---

## Build / test

```powershell
dotnet build Motely.slnx
dotnet test Motely.Tests/Motely.Tests.csproj
```

.NET 10 SDK required (`global.json` pins `10.0.204`).
