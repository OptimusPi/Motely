# Driving Game — handoff

A "driving game": the **caller drives a seed's streams deeper, interactively** — "give me the next 100 items," again, again — instead of running one fixed-window analysis and stopping. JS (or any host) holds the wheel and asks the engine to roll forward.

This doc records the design decision we landed on, *why*, and the primitives that already exist to build it.

## The core decision

**Do NOT export / marshal the live `MotelySingleSearchContext` to drive it from the host.**

Two independent reasons, both pointing the same way:

### 1. Lifetime — the ctx is transient
`MotelySingleSearchContext` is created **per-lane inside the vector search loop** and holds **live PRNG cursors**. It is only valid *during* the synchronous call it's handed to — exactly like `Jimmolate.findSeed(ctx)`: the ctx is real, but only for the duration of that one predicate call. The instant the lane advances, the engine recycles it. A Bootsharp instance handle to it would **dangle** — you'd be "driving" something that no longer exists. You cannot durably hold it and call it again later.

### 2. Performance — fine-grained interop is ~10,000x slower
A marshalled ctx is **pass-by-reference instance binding**: every `ctx.something()` from the host is a full round-trip (proxy → serialize args → cross into managed → run → serialize return → cross back), ~microseconds each. Native pulls a PRNG value as a SIMD/register op — nanoseconds, no crossing. A single seed pulls **hundreds–thousands** of stream values (shop/packs/tarot/planet/spectral/vouchers/bosses/tags). Driving those per-item from the host stacks three penalties:

1. **Per-call crossing cost** — ~1,000–100,000x per op
2. **Loss of SIMD** — interop is scalar, one lane at a time; throws away the ~8-wide vectorization the engine exists for
3. **No amortization** — paid for *every candidate*, not just survivors

Multiplicatively: 10,000x+. The lifetime constraint and the perf constraint agree — **fine-grained PRNG work stays native; the host only ever gets coarse results or coarse decisions.**

## The substrate that already works: JAMLyzer paging/resume

JAMLyzer already had to hold all its own streams, so it solved this the right way — it **externalizes stream state into a serializable, re-hydratable bag** instead of exposing the live ctx. That bag is the durable, re-enterable thing. The host holds the *bag*, not the ctx.

Existing API (`Motely.Wasm/Program.cs`, `MotelyJamlyzer`):

- `AnalyzeSeedsPaged(jaml, eventRolls)` — first page of the scroll (explicit roll window)
- result carries a `MotelyJamlyzerStreamStates` bag — the snapshot of where every stream stopped
- `ResumeSeeds(jaml, resumeFrom, eventRolls)` — re-hydrate the bag and continue **exactly where it stopped**

That loop *is* the driving primitive. "Iterate deeper 100x" = call `ResumeSeeds` with the prior page's `streamStates` and `eventRolls = 100`. One crossing carries a whole page of native work across the boundary — coarse, amortized, fast.

**Constraint to remember:** resume is **single-seed only** — the bag's PRNG state is seed-specific. A page belongs to one seed's streams.

## The proven "pass something IN" template

When the host needs to hand a *decision* into the engine (not pull items out), the per-search **interface-as-argument** shape works and is cheap — proven green this session:

- `IJimmolatePredicate { bool FindSeed(MotelySingleSearchContext ctx); }`
- `MotelySearchWith.searchList(jaml, predicate)` — predicate crosses as an instance handle (`$i.import(predicate)`), called **once per surviving seed** with the specialized ctx nested inside
- Coarse + post-filter ⇒ basically free. This is the template for "JS authors the rule, engine runs it native."

(The older `Jimmolate.findSeed = fn` global-static slot still exists and is untouched; the per-search interface form sits beside it.)

## Next steps for the driving game

1. Decide the host-facing shape: e.g. `drive(jaml, seed, pageSize)` → returns `{ items, states }`; then `driveMore(jaml, states, pageSize)` on top of `ResumeSeeds`.
2. Page size = `eventRolls`. Pick sane defaults; expose it.
3. Keep it single-seed per drive session (bag is seed-specific).
4. Host holds the `states` bag between calls — that's the "wheel." Never the ctx.
5. Optional: a coarse `IJimmolatePredicate`-style gate to pick *which* seed to drive before the driving session starts.

## One-line summary

Drive the **stream-state bag** (JAMLyzer resume), never the live ctx — because the ctx dangles *and* per-item interop is 10,000x slower. Coarse in, coarse out, fine-grained work stays native.
