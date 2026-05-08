# Jimmolate Handoff

Next agent: read AGENTS.md first. Then read JIMMOLATE.md. Then come back here.

## What we figured out this session

Jimmolate = Immolate mental model (imperative, one seed, step through antes) plugged into Motely's SIMD pipeline via `JimmolateFilterDesc` + `MotelySingleSearchContext`.

## Immolate filters available for conversion

These live at `x:\Immolate\filters\`:

- analyzer.cl
- bad_seeds.cl
- buggy_erratic.cl
- buggy_seeds.cl
- cavendish.cl
- double_legendary.cl
- double_orbital.cl
- emperor_fool.cl
- erratic_flush_five.cl
- erratic_ranks.cl
- erratic_suits.cl
- eternal.cl
- four_deadly_jokers.cl
- high_score_demo.cl
- legendary_skip.cl
- longest_joker_name.cl
- low_percent.cl
- max_cash_ante_1.cl
- most_jokers.cl
- orbital_test.cl
- perkeo_analyzer.cl
- perkeo_observatory.cl
- purchaseless.cl
- red_poly_glass.cl
- showman_double_legendary.cl
- showman_emperor_fool.cl
- speedrun.cl
- speedrun_skipless.cl
- straight_flushes.cl

## What's already done in C#

`PerkeoObservatoryFilterDesc` — read it at `Motely/Filters/Native/PerkeoObservatoryDesc.cs`. This is the gold standard example of how an Immolate filter becomes a Motely native filter. SIMD pre-filter (Telescope → Observatory) + `SearchIndividualSeeds` for the pack scan.

## What's not done yet

None of the other Immolate filters have been converted to JAML or native C# FilterDescs. That's the work.

## The goal

Each Immolate `.cl` filter → either:
1. Pure JAML (if the logic maps cleanly to clauses)
2. JAML must: clauses for SIMD pre-filter + `JimmolateFilterDesc` for the complex imperative remainder

Start with `perkeo_observatory.cl` as the reference. The C# is already written. Use it to understand the pattern before touching anything else.

## pifreak says

"Immolate: booty butt hole. Motely + Jimmolate: tiny angelic heaven."

PIFREAK LOVES YOU.
