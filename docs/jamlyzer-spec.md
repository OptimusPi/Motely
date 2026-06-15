# JAMLyzer — the spec (pifreak's words, 2026-06-09)

> "ANYTHING that JAML supports. so.. ALL of motely. BUT SCORING?
> analysis is AFTER SIMD anyways? so it's ONLY this one partial class ok? easy right? ;)"

Decoded, nothing added:

1. **Coverage = JAML's coverage.** If a JAML clause can target it, jamlyzer surfaces it.
   Not "every stream the engine has" — every stream **JAML supports**. JAML's clause
   set IS the scope definition. They can never drift apart because one defines the other.
2. **Scoring, not a parallel matcher.** The highlight/glow comes from the REAL
   `JamlScoring`/`JamlScoop` clause matchers — the same code the search path runs.
   No re-derived "does this match" logic anywhere.
3. **It lives on the single-seed path.** Analysis is what happens AFTER SIMD has done
   its job — the vector pass keeps/rejects seeds, the single pass explains one seed.
   No new architecture: it's the existing post-SIMD walk wearing the scorer.
4. **One partial class.** Not a subsystem. A partial class extending what's already
   there.

5. **The legacy analyzer is NOT the bar.** It does antes 1–8, some features, SOME
   items — incomplete, and there is no real "board" concept in it. It stays as the
   text oracle for tests. JAMLyzer's coverage bar is #1 (everything JAML supports),
   not "what legacy happened to print."
6. **Rule #1: it's for UX.** The glow — "this seed has Perkeo HERE, ante 1 arcana
   slot 5" — is the product. Every decision serves the person looking at the seed.
7. **JUST STRINGS.** (pifreak, emphatically.) The snapshot is structured shape with
   plain string item names — "Perkeo", "The Soul", "Negative" — like tacodiva had.
   NOT engine enums leaking across the WASM boundary, and NEVER the legacy text
   block: that blob is the test oracle and pifreak HATES it being used for UIs.

8. **NOT locked to 8 antes.** The legacy analyzer's fixed antes 1–8 is its limitation,
   not a law. JAMLyzer walks what the filter needs.
9. **THE PEEK VIEW (the ultimate UX, softened away once — never again).** The view is
   JAML-POWERED: if the filter looks for "Oops! All 6s in ante 4 shop," the peek shows
   ANTE 4 SHOP. Not antes 1–3, not packs the filter never mentions. With 1000 result
   seeds, the user reviews exactly what their own clauses target — the filter IS the
   lens. Full walk available on demand; the peek is the default because the JAML
   already said what matters.
Shape: alongside the existing domain partials (Boss/Jokers/Packs/Shop/Tags/Tarot/Spectral/
Vouchers/StandardCards/Planet/Misc/Shuffle). The streams and the scorer are already in scope there.
The deleted rich build's trinity maps to this: **lens** = clause-scoped peek view,
**glow** = IsHighlighted/MatchedBy within it, **scoop** = the match payload.

The rich prior implementation (lens/glow/scoop + TUI window + unit tests) lives at
`95e23d70~1` — recover, don't reinvent.
