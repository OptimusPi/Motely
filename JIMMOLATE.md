# Jimmolate

Jimmolate is the bridge between Immolate's imperative mental model and Motely's SIMD pipeline.

## The mental model

Immolate (`filter.cl`): one seed, step through antes, check things in order. GPU runs thousands in parallel but each kernel is imperative — you write the loop.

Motely: SIMD pre-filters knock out 97%+ of seeds 8 at a time. Survivors go to `SearchIndividualSeeds` which hands each one to `MotelySingleSearchContext`. **That's the same imperative mental model as Immolate.**

```
SIMD (fast, kills the obvious rejects)
    ↓ survivors only
MotelySingleSearchContext (imperative, one seed, step through antes)
    ↓ same mental model as filter(instance* inst)
```

## JimmolateFilterDesc

Takes a `JimmolateSeedPredicate` delegate and runs it via `SearchIndividualSeeds`. Write logic the Immolate way — step through antes, check packs, chase conditional streams — and it plugs straight into the Motely pipeline after the SIMD pre-filters.

```csharp
public delegate bool JimmolateSeedPredicate(ref MotelySingleSearchContext searchContext);

public readonly struct JimmolateFilterDesc(JimmolateSeedPredicate predicate)
    : IMotelySeedFilterDesc<JimmolateFilterDesc.JimmolateFilter>
```

**Important:** Jimmolate skips SIMD entirely — it calls `SearchIndividualSeeds` on every surviving lane with no pre-filter mask. Always pair it with at least one real SIMD filter upstream (as the base filter or a previous `.WithAdditionalFilter()`). Without that, it touches every seed.

## The reference pattern: PerkeoObservatoryFilterDesc

`PerkeoObservatoryFilterDesc` is what a fully native Jimmolate-style filter looks like — SIMD voucher checks, then `SearchIndividualSeeds` on survivors only:

```csharp
// SIMD: knock out anything without Telescope → Observatory
VectorMask matching = ...;
return searchContext.SearchIndividualSeeds(
    matching,                          // pre-filter mask from SIMD
    (ref MotelySingleSearchContext ctx) =>
    {
        // imperative pack iteration here
    }
);
```

`JimmolateFilterDesc` is the generic delegate version of this pattern — plug in any predicate without writing a full filter struct.

## Writing a Jimmolate predicate

Use `MotelySingleSearchContext` directly for full imperative control:

```csharp
// Example: check packs in ante 1 for a Soul card → Perkeo
var desc = new JimmolateFilterDesc((ref MotelySingleSearchContext searchContext) =>
{
    var boosterPackStream = searchContext.CreateBoosterPackStream(1, true, false);
    var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
    {
        var tarotStream = searchContext.CreateArcanaPackTarotStream(1, true);
        if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
        {
            var soulStream = searchContext.CreateLegendaryJokerStream(1);
            return searchContext.GetNextJoker(ref soulStream).Type == MotelyItemType.Perkeo;
        }
    }

    return false;
});
```

Attach it after a real SIMD base filter:

```csharp
new MotelySearchSettings<SomeSimdFilterDesc>(simdFilter)
    .WithAdditionalFilter(desc)
    ...
```

## UI constraint

The UI **must** require at least one real SIMD clause before Jimmolate can be added. Jimmolate without a SIMD pre-filter is just a slow per-seed loop over the entire seed space.

## The name

Named after Immolate — the original OpenCL GPU seed searcher for Balatro. Immolate's `filter(instance* inst)` is the spiritual ancestor of `MotelySingleSearchContext`. Jimmolate is what happens when you take that mental model and plug it into heaven-tier SIMD super speed.

Immolate: booty butt hole (slow, GPU-only, no SIMD abstraction)
Motely + Jimmolate: tiny angelic heaven (SIMD pre-filter + imperative fallback)
