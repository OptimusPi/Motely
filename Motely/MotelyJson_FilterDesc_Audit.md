MotelyJson FilterDesc Audit Report
=================================

Scope
-----
- Reviewed Motely core in `x:\BalatroSeedOracle\external\Motely\Motely`, with `filters\PerkeoObservatoryDesc.cs` as the golden example.
- Focused on MotelyJson filter descs (criteria-based, JAML/JAML-style pipeline).
- Classified each filter by SIMD hotpath style and scalar verification usage.

Golden Example (Tacodiva)
-------------------------
`filters\PerkeoObservatoryDesc.cs`:
- Vectorized voucher prefilter (A1/A2) + early exit.
- Scalar verification via `SearchIndividualSeeds` on the reduced mask for Soul card verification.
- Pattern: SIMD mask first, scalar only when needed.

Classification Key
-------------------
A) SIMD-only mask return (pure vectorized, returns `VectorMask`)
B) SIMD prefilter + `SearchIndividualSeeds` scalar verification
C) Wrapper/combiner (composite, invert, negation, pre-and-base), not a standalone SIMD filter

MotelyJson FilterDesc Classification
------------------------------------
Category A — SIMD-only mask return
- `MotelyJsonEventFilterDesc` (vectorized event rolls; pure mask)
- `MotelyJsonVoucherFilterDesc` (vectorized voucher matches; pure mask)
- `MotelyJsonJokerRarityEditionPreFilterDesc` (vectorized rarity+edition prefilter)
- `MotelyJsonErraticRankFilterDesc` (vectorized 52-card rank count)
- `MotelyJsonErraticSuitFilterDesc` (vectorized 52-card suit count)
- `MotelyJsonErraticCardFilterDesc` (vectorized 52-card rank+suit count)
- `MotelyJsonErraticRankAndSuitFilterDesc` (combined SIMD rank+suit in one loop)

Category B — SIMD prefilter + SearchIndividualSeeds
- `MotelyJsonJokerFilterDesc` (SIMD unless Min>1; then scalar verification)
- `MotelyJsonSoulJokerFilterDesc` (vectorized Soul detection + scalar verification)
- `MotelyJsonSoulJokerEditionOnlyFilterDesc` (vectorized edition check + scalar verification)
- `MotelyJsonTagFilterDesc` (vectorized tag scan + scalar Min verification)
- `MotelyJsonPlanetFilterDesc` (vectorized shop/pack scan + scalar Min verification)
- `MotelyJsonSpectralCardFilterDesc` (vectorized shop/pack scan + scalar Min verification)
- `MotelyJsonTarotCardFilterDesc` (vectorized shop/pack scan + scalar Min verification)
- `MotelyJsonPlayingCardFilterDesc` (scalar verification only; uses SearchIndividualSeeds)
- `MotelyJsonBossFilterDesc` (scalar verification only; uses SearchIndividualSeeds)

Category C — Wrapper/combiner (non-SIMD primary)
- `MotelyJsonCompositeFilterDesc` (ANDs filters, handles And/Or nesting)
- `MotelyJsonPreAndBaseFilterDesc` (pre-filter AND base filter)
- `MotelyJsonNegationFilterDesc` (OR of inner filters, then invert)
- `MotelyJsonInvertFilterDesc` (invert inner filter mask)

Notable Behavior: EditionOnly uses SearchIndividualSeeds
-------------------------------------------------------
`MotelyJsonSoulJokerEditionOnlyFilterDesc` currently calls `SearchIndividualSeeds`, which means the edition-only prefilter is still doing scalar verification.
If the requirement is "EditionOnly should not call SearchIndividualSeeds", this is a mismatch and should be adjusted.

Rules/Skills Check
------------------
- No obvious out-of-place rules in `external\Motely\.cursor\rules`.
- The Cursor create-skill guidance file is blocked by `.cursorignore` at:
  `C:\Users\pifre\.cursor\skills-cursor\create-skill\SKILL.md`
  (access denied by ignore rules).

Skill Draft (Cursor) — Motely FilterDesc Audit & JAML Wiring
-----------------------------------------------------------
Suggested path:
`x:\BalatroSeedOracle\.cursor\skills\motely-filter-desc-auditor\SKILL.md`

Purpose
- Audit or add Motely FilterDesc implementations and wire them into JAML/JAML JSON pipeline.

Triggers
- "Add a new filter", "audit filter hotpath", "wire filter to JAML", "MotelyJson filter review".

Required Inputs
- Filter category (item type), inputs (value/values/edition/sources/antes), and expected SIMD behavior.
- Whether the filter needs scalar verification or can stay SIMD-only.

Audit Checklist
- Confirm FilterDesc exists and uses criteria (MotelyJson*FilterCriteria).
- Check for SIMD-only hotpath:
  - No LINQ in vector loops.
  - No string comparisons in hot path.
  - Uses cached streams (`ctx.Cache...`) when needed.
- Check scalar verification usage:
  - Only used for rare paths or Min thresholds.
  - Ensure it is behind a small prefilter mask.
- Ensure clause defaults are populated (WantedAntes, EffectiveAntes).
- Confirm empty clause handling uses `Debug.Assert` (programming error).

Wiring Checklist (JAML/JAML JSON)
- `MotelyJsonPerformanceUtils.TypeMap` includes the type string alias.
- `MotelyJsonConfig.PostProcess` parses enums and wildcards.
- `MotelyJsonFilterClauseTypes` has a typed clause + `FromJsonClause`.
- `FilterCategoryMapper.GetCategory` routes to correct category.
- `SpecializedFilterFactory.CreateSpecializedFilter` includes the filter.
- `MotelyCompositeFilterDesc` handles AND/OR grouping for the new category.
- `MotelyRunConfig` or scoring wiring if it affects `should`/`seed score`.

Suggested Outputs
- Report of SIMD vs scalar verification path.
- Summary of new wiring points touched.
- Performance notes (cache usage, early exit).
