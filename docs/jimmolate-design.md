# Jimmolate design

## What Jimmolate is

**Jimmolate** is Motely’s hook for **Immolate-style imperative seed logic**: one seed at a time, order-sensitive, branchy procedural checks against `MotelySingleSearchContext` (streams, antes, shop queues, etc.). It is **not** the Balatro **Immolate** spectral card; that was accidental scaffolding and must not define the product.

The **C# bridge** today is `JimmolateFilterDesc`: it routes surviving SIMD lanes through `MotelyVectorSearchContext.SearchIndividualSeeds` so user code runs as `JimmolateSeedPredicate(ref MotelySingleSearchContext)`.

## Lineage: Immolate vs Motely

- **Immolate** (historical OpenCL searcher): `.cl` filters express `filter(instance* inst)` — imperative logic over a **single** seed instance. Parallelism lives **outside** the filter (many seeds evaluated in parallel).
- **Motely** splits the problem:
  - **Broad narrowing**: JAML and native SIMD filters produce `VectorMask` survivors cheaply.
  - **Survivor logic**: `MotelySingleSearchContext` runs the branchy, procedural work per lane.

Jimmolate preserves the Immolate **mental model** (“write logic like Immolate”) while **Motely remains the execution source of truth**.

## Execution shape (supported path)

```
JAML / SIMD base filter  →  VectorMask survivors
       →  BatchSeeds into additional-filter batches
       →  JimmolateFilterDesc.Filter → SearchIndividualSeeds(predicate)
       →  procedural logic on MotelySingleSearchContext
```

Upstream narrowing can be **JAML-backed** or a **native SIMD filter**; the invariant is: **additional filters only see seeds that passed the prior stage**, packed into batches with invalid lanes cleared.

For comparison, filters such as `PerkeoObservatoryDesc` illustrate “SIMD first, then `SearchIndividualSeeds` on survivors.” `JimmolateFilterDesc` is the **delegate-shaped** version of the survivor pass.

## JAML vs Jimmolate

| | JAML | Jimmolate |
|---|------|-----------|
| Style | Declarative, schema-validatable | Procedural, order-sensitive |
| Strength | Cheap broad constraints, tooling | Branchy “walk the run” logic |
| Relationship | **Compose** | **Compose** |

Neither replaces the other: JAML pins the search space; Jimmolate expresses what’s awkward or impossible to spell declaratively.

## Authoring promise

**“Write logic like Immolate; run it through Motely.”**

That implies:

- Predicates use `MotelySingleSearchContext` (same authoritative streams/rules as the rest of Motely).
- Production searches should usually pair Jimmolate with real narrowing, not run it across whole sequential spaces alone.

## UI / MCP: uploaded legacy `.cl` files

A practical conversion path:

1. **Extract safe JAML prefilter hints** where the semantics map cleanly.
2. **Preserve order-sensitive logic** as a Jimmolate recipe/procedure when it does not map to JAML.
3. **Emit warnings** when conversion is approximate or semantics diverge.
4. **Verify against known seeds** (golden checks) where possible.

## Performance rules

- **Small explicit seed lists** may run Jimmolate-heavy paths without broad SIMD if the caller accepts cost.
- **Broad searches** should **require or strongly warn for** JAML/SIMD narrowing first; Jimmolate without narrowing is “evaluate this imperative predicate on everything,” which does not scale.

## Proof and release discipline

Before treating any JS/WASM/MCP authoring surface as stable:

1. Prove the **C# bridge** with a focused test: base filter narrows lanes → `JimmolateFilterDesc` runs **only** on those survivors → results match a pure C# control filter on the same seed list.
2. Only then design/export JS APIs.

See `Motely.Tests/JimmolateFilterDescTests.cs`.

## References (code)

- `Motely/Filters/Native/JimmolateFilterDesc.cs`
- `Motely/MotelyVectorSearchContext.cs` (`SearchIndividualSeeds`)
- `Motely/Filters/Jaml/JamlShouldScoreDesc.cs` (composition patterns)
- `Motely/Filters/Native/PerkeoObservatoryDesc.cs` (SIMD + survivor pattern)
