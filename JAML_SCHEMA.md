# JAML Schema — Source of Truth

## Core Rule

**One YAML clause = one FilterDesc = one filter in the AND chain.**

- `must:` — every clause must pass (AND). Each clause is its own filter.
- `should:` — clauses add to score. AND and OR both supported.
- `mustNot:` — exclusion. If any match, seed is rejected.
- Within a single clause, arrays like `jokers: [Blueprint, DNA]` mean "match ANY" (OR).

## How YAML Becomes Typed Clauses

```yaml
must:
  - joker: Blueprint         # → JokerClause { Jokers = [Blueprint] }
  - joker: DNA               # → JokerClause { Jokers = [DNA] }
  - jokers: [Blueprint, DNA] # → JokerClause { Jokers = [Blueprint, DNA] } (OR: find either)
  - voucher: Overstock       # → VoucherClause { Vouchers = [Overstock] }
  - boss: TheNeedle          # → BossClause { Bosses = [TheNeedle] }
```

The loader reads each untyped YAML property bag, detects the type from the key,
parses enum values, and adds a typed clause to the correct list on JamlConfig.

Each clause in a list becomes its own filter:
```csharp
foreach (var clause in config.Jokers)
    subFilters.Add(new JokerFilterDesc(clause).CreateFilter(ref ctx));
```

## FilterDesc ↔ JAML Mapping

### Jokers (per-rarity — different PRNG streams per rarity)

| # | FilterDesc | JAML key(s) | Clause POCO | Values type | SIMD? | Notes |
|---|-----------|-------------|-------------|-------------|-------|-------|
| 1 | `JokerFilterDesc` | `joker:` / `jokers:` | `JokerClause` | `MotelyJoker[]` | TODO | Mixed rarity fallback, shop+packs+sources |
| 2 | `CommonJokerFilterDesc` | auto-detected | `CommonJokerClause` | `MotelyJokerCommon[]` | TODO | Shop, packs, Judgement, RiffRaff |
| 3 | `UncommonJokerFilterDesc` | auto-detected | `UncommonJokerClause` | `MotelyJokerUncommon[]` | TODO | Shop, packs, Judgement, UncommonTag |
| 4 | `RareJokerFilterDesc` | auto-detected | `RareJokerClause` | `MotelyJokerRare[]` | TODO | Shop, packs, Judgement, Wraith, RareTag |
| 5 | `LegendaryJokerFilterDesc` | `legendaryJoker:` | `LegendaryJokerClause` | `MotelyJokerLegendary[]` | TODO | Soul card in Arcana/Spectral packs ONLY |

### Items

| # | FilterDesc | JAML key(s) | Clause POCO | Values type | SIMD? | Notes |
|---|-----------|-------------|-------------|-------------|-------|-------|
| 6 | `VoucherFilterDesc` | `voucher:` / `vouchers:` | `VoucherClause` | `MotelyVoucher[]` | YES | Vectorized ante voucher matching |
| 7 | `TarotCardFilterDesc` | `tarot:` | `TarotCardClause` | `MotelyTarotCard[]` | TODO | Shop, Arcana packs, Emperor, PurpleSeal |
| 8 | `SpectralCardFilterDesc` | `spectral:` | `SpectralCardClause` | `MotelySpectralCard[]` | TODO | Shop, Spectral packs, SixthSense, Seance |
| 9 | `PlanetCardFilterDesc` | `planet:` | `PlanetCardClause` | `MotelyPlanetCard[]` | TODO | Shop, Celestial packs |
| 10 | `StandardCardFilterDesc` | `standardCard:` | `StandardCardClause` | Rank/Suit/Enh/Seal/Ed | TODO | Shop, Standard packs |

### Blinds & Tags

| # | FilterDesc | JAML key(s) | Clause POCO | Values type | SIMD? | Notes |
|---|-----------|-------------|-------------|-------------|-------|-------|
| 11 | `BossFilterDesc` | `boss:` | `BossClause` | `MotelyBossBlind[]` | TODO | Per-ante boss stream |
| 12 | `TagFilterDesc` | `tag:` / `smallBlindTag:` / `bigBlindTag:` | `TagClause` | `MotelyTag[]` | YES | Vectorized, Position field |

### Erratic Deck

| # | FilterDesc | JAML key(s) | Clause POCO | Values type | SIMD? | Notes |
|---|-----------|-------------|-------------|-------------|-------|-------|
| 13 | `ErraticRankFilterDesc` | `erraticRank:` | `ErraticRankClause` | `MotelyPlayingCardRank` | YES | 52-card loop, rank only |
| 14 | `ErraticSuitFilterDesc` | `erraticSuit:` | `ErraticSuitClause` | `MotelyPlayingCardSuit` | YES | 52-card loop, suit only |
| 15 | `ErraticCardFilterDesc` | `erraticCard:` | `ErraticCardClause` | Rank + Suit | YES | 52-card loop, both rank AND suit |

### Events (each unrelated — separate PRNG, separate FilterDesc)

| # | FilterDesc | JAML | Clause POCO | SIMD? |
|---|-----------|------|-------------|-------|
| 16 | `LuckyMoneyFilterDesc` | `event: LuckyMoney` | `LuckyMoneyClause` | TODO |
| 17 | `LuckyMultFilterDesc` | `event: LuckyMult` | `LuckyMultClause` | TODO |
| 18 | `MisprintMultFilterDesc` | `event: MisprintMult` | `MisprintMultClause` | TODO |
| 19 | `WheelOfFortuneFilterDesc` | `event: WheelOfFortune` | `WheelOfFortuneClause` | TODO |
| 20 | `CavendishExtinctFilterDesc` | `event: CavendishExtinct` | `CavendishExtinctClause` | TODO |
| 21 | `GrosMichelExtinctFilterDesc` | `event: GrosMichelExtinct` | `GrosMichelExtinctClause` | TODO |

**Total: 21 FilterDescs**

## Clause Fields

Every clause defines its own (not inherited):
```
antes: int[]    # which antes to search (0-indexed, ante 0 is valid)
min: int        # minimum match count (default: 1, non-nullable)
```

## Top-Level Config

```yaml
name: "My Filter"
deck: Red           # MotelyDeck
stake: White        # MotelyStake
defaults:
  antes: [1,2,3,4,5,6,7,8]
must: [...]         # AND — each clause = one filter, all must pass
should: [...]       # scoring — each clause adds score points
mustNot: [...]      # exclusion — if any match, seed is rejected
```

## JamlConfig (C#)

Typed lists — one per clause type. Each element = one filter:
- in the AND chain for must: clauses (filtering!)
- in the OR chain for should: clauses (scoring!)
```csharp
public sealed class JamlConfig
{
    // Jokers (per-rarity + mixed fallback)
    public List<JokerClause> Jokers = [];
    public List<CommonJokerClause> CommonJokers = [];
    public List<UncommonJokerClause> UncommonJokers = [];
    public List<RareJokerClause> RareJokers = [];
    public List<LegendaryJokerClause> LegendaryJokers = [];

    // Items
    public List<VoucherClause> Vouchers = [];
    public List<TarotCardClause> TarotCards = [];
    public List<SpectralCardClause> SpectralCards = [];
    public List<PlanetCardClause> PlanetCards = [];
    public List<StandardCardClause> StandardCards = [];

    // Blinds & Tags
    public List<BossClause> Bosses = [];
    public List<TagClause> Tags = [];

    // Erratic Deck
    public List<ErraticRankClause> ErraticRanks = [];
    public List<ErraticSuitClause> ErraticSuits = [];
    public List<ErraticCardClause> ErraticCards = [];

    // Events (each unrelated)
    public List<LuckyMoneyClause> LuckyMoney = [];
    public List<LuckyMultClause> LuckyMult = [];
    public List<MisprintMultClause> MisprintMult = [];
    public List<WheelOfFortuneClause> WheelOfFortune = [];
    public List<CavendishExtinctClause> CavendishExtinct = [];
    public List<GrosMichelExtinctClause> GrosMichelExtinct = [];
}
```
