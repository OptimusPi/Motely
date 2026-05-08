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

Takes a `Func<string, bool>` predicate and runs it via `SearchIndividualSeeds`. Write logic the Immolate way — get the seed string, do your checks imperatively — and it plugs straight into the Motely pipeline after the SIMD pre-filters.

```csharp
public struct JimmolateFilterDesc(Func<string, bool> predicate)
    : IMotelySeedFilterDesc<JimmolateFilterDesc.JimmolateFilter>
```

## Writing a Jimmolate filter

The SIMD pre-filters do the heavy lifting (vouchers, bosses, tags — cheap, vectorized, early exit). `JimmolateFilterDesc` handles the logic that's too complex for SIMD: pack iteration, Soul card checks, conditional streams.

Use `MotelySingleSearchContext` directly for full imperative control:

```csharp
// Example: check packs in ante 1 and 2 for a Soul card → Perkeo
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
```

## The name

Named after Immolate — the original OpenCL GPU seed searcher for Balatro. Immolate's `filter(instance* inst)` is the spiritual ancestor of `MotelySingleSearchContext`. Jimmolate is what happens when you take that mental model and plug it into heaven-tier SIMD super speed.

Immolate: booty butt hole (slow, GPU-only, no SIMD abstraction)
Motely + Jimmolate: tiny angelic heaven (SIMD pre-filter + imperative fallback)
