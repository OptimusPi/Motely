# JamlFilterDesc Checklist

Every `IMotelySeedFilterDesc` in `Motely/Filters/Jaml/`. Three goals each:

1. **Define props (no polymorphism)** — the clause owns its WHAT as plain fields. No base-class/inheritance tricks; each desc is its own source of truth.
2. **Define the valid sources** — the clause declares every WHERE it can come from (and only the valid ones).
3. **Ensure SIMD + JamlScoring are complete** — the vectorized `Filter()` and the scalar `JamlScoring` path both read *every* declared source/prop. No declared-but-ignored sources.

---

## Events (`Filters/Jaml/Events/`)

### BloodstoneTriggerFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### BusinessPayoutFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### ParkingPayoutFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### CavendishExtinctFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### GrosMichelExtinctFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### SpaceLevelupFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### GlassDestroyFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### WheelStaysFlippedFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### LuckyMoneyFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### LuckyMultFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### WheelOfFortuneFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### MisprintMultFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

---

## Ante Cards (`Filters/Jaml/AnteCards/`)

### JokerFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### CommonJokerFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### UncommonJokerFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### RareJokerFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### LegendaryJokerFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### TarotCardFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### PlanetCardFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### SpectralCardFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### StandardCardFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### ErraticRankFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### ErraticSuitFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### SpecialSpectralCardFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### LegendarySoulEditionFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

---

## Ante Features (`Filters/Jaml/AnteFeatures/`)

### BossFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### StartingDrawFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### TagFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### VoucherFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

### MultiVoucherFilterDesc
- [ ] 1. Define props (no polymorphism)
- [ ] 2. Define the valid sources
- [ ] 3. Ensure SIMD + JamlScoring are complete

---

## Combinators (`Filters/Jaml/`)

These wrap inner clause(s) instead of matching a leaf, so the three goals read differently:
1. **Compose inner, no polymorphism** — holds inner `IMotelySeedFilterDesc`(s); the combinator itself adds no leaf props.
2. **Define the valid nesting** — what it may wrap (single vs. list, any clause vs. restricted), and how empty/degenerate cases resolve.
3. **Ensure SIMD + JamlScoring are complete** — the vectorized mask combine (`~`, `&`, `|`) and the scalar `JamlScoring` aggregation agree.

### NegationFilterDesc
- [x] 1. Compose inner, no polymorphism — wraps one inner desc
- [ ] 2. Define the valid nesting
- [ ] 3. Ensure SIMD + JamlScoring are complete

### AndFilterDesc *(to build)*
- [ ] 1. Compose inner, no polymorphism
- [ ] 2. Define the valid nesting
- [ ] 3. Ensure SIMD + JamlScoring are complete

### OrFilterDesc *(to build)*
- [ ] 1. Compose inner, no polymorphism
- [ ] 2. Define the valid nesting
- [ ] 3. Ensure SIMD + JamlScoring are complete
