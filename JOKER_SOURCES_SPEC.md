# Joker Sources Spec (verified)

The source-of-truth for **which sources each joker FilterDesc may declare and must check**.
Sources are **rarity-locked** by Balatro rules — they only *look* the same across clauses.
Verified against balatrowiki.org (Judgement, Rare Tag, Wraith, Riff-Raff, Jokers pages).

## Source → joker rarity it can produce

| Source (stream)            | Produces            | Vector stream method                |
|----------------------------|---------------------|-------------------------------------|
| `ShopItems`                | Common/Uncommon/Rare| `CreateShopItemStream`              |
| `BoosterPacks` (Buffoon)   | Common/Uncommon/Rare| `CreateBuffoonPackJokerStream`      |
| `Judgement`                | Common/Uncommon/Rare| `CreateJudgementJokerStream`        |
| `Wraith`                   | **Rare only**       | `CreateWraithJokerStream`           |
| `RareTag`                  | **Rare only**       | `CreateRareTagJokerStream` (fixed)  |
| `UncommonTag`              | **Uncommon only**   | `CreateUncommonTagJokerStream` (fixed) |
| `RiffRaff`                 | **Common only** (×2)| `CreateRiffRaffJokerStream` (fixed) |
| Soul (Arcana/Spectral pack)| **Legendary only**  | (legendary soul matcher)            |

Fast-path shop streams read the rarity-specific shop PRNG directly:
`CommonShopJokers`→common, `UncommonShopJokers`→uncommon, `RareShopJokers`→rare,
`AllShopJokers`→any.

## Per-clause canonical sources (what each FilterDesc declares + must CHECK)

| Clause            | Valid sources                                                              |
|-------------------|----------------------------------------------------------------------------|
| `CommonJoker`     | ShopItems, BoosterPacks, Judgement, RiffRaff, CommonShopJokers             |
| `UncommonJoker`   | ShopItems, BoosterPacks, Judgement, UncommonTag, UncommonShopJokers        |
| `RareJoker`       | ShopItems, BoosterPacks, Judgement, Wraith, RareTag, RareShopJokers        |
| `Joker` (generic) | all non-legendary: ShopItems, BoosterPacks, Judgement, Wraith, RiffRaff, RareTag, UncommonTag, AllShopJokers |
| `LegendaryJoker`  | ArcanaPacks, SpectralPacks, SoulCard, RequireMegaPack (soul path only)     |

Legendary is a **totally different source model** (soul cards in packs), so it must NOT be
welded into `joker:`. Drop `JokerClause.LegendarySources` + `UsesLegendaryPath`; route
legendary through `legendaryJoker:` only.

## Semantics of a source

A source means: *assume the player obtains/uses that stream and read what it yields.*
e.g. `rareTag` = "the player gets a Rare Tag; what Rare joker does it give?" Tags carry
forward across antes, so **no ante-conditional branching** in SIMD — just read the stream
at the requested roll indices, same shape as Tarot reading the Emperor stream.

## Two implementation tasks (the "check them" mission)

1. **Per-clause Sources types** — replace the shared `JokerSourceConfig` with one type per
   clause holding ONLY that clause's valid sources (table above). No shared config.
2. **Wire every declared source into the SIMD `Filter()`** — today the joker SIMD only reads
   ShopItems + Buffoon packs (Uncommon also reads its fast-path). Each declared specialty
   source (Judgement/Wraith/RiffRaff/RareTag/UncommonTag) must be read via its vector stream,
   mirroring the scalar `CountSpecialtyJokerSources`. SIMD precision can stay loose
   (over-read ok — scoring re-checks); it must NOT skip a declared source.

## Defaults (already implemented)

Each FilterDesc has a co-located `DefaultSources` (8 shop slots + 6 packs; specialty off),
applied via `clause.Sources ?? DefaultSources` in CreateFilter, SIMD, and scalar. A terse
clause (`joker: Blueprint`) just works; any explicit `sources:` overrides wholesale.
