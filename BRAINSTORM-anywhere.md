# Brainstorm 2026-09-06 — "anywhere" and friends (corrected)

Nat + Claude (Cowork). First draft listed six items; two already existed. Corrected list below.
See JamlFilters/a.jaml ("Whimsy Dicetricks") for the working syntax of everything marked EXISTS.

## EXISTS — do not build

- `min: N` on any clause = count. (`count:` was proposed; it is `min`.)
- `or:` / `and:` as bare lists with per-arm `antes:` = same-ante grouping.
  Three Oops behind a Negative tag in ante 3 or 4, today:

      - or:
          - and:
              - smallBlindTag: NegativeTag
                antes: [3]
              - joker: OopsAll6s
                antes: [3]
                min: 3
                sources: { shopItems: [0, 1, 2, 3, 4, 5] }
          - and:
              - smallBlindTag: NegativeTag
                antes: [4]
              - joker: OopsAll6s
                antes: [4]
                min: 3
                sources: { shopItems: [0, 1, 2, 3, 4, 5] }

- Source keys already wired for jokers: shopItems, boosterPacks, judgement, wraith, riffRaff,
  uncommonTag, rareTag. Spectrals: sixthSense (+ packs). Legendaries: arcanaPacks, spectralPacks.
  "Showman from anywhere" is writable now by listing them all.

## 1. `sources: anywhere` — sugar only

Expands at load to every source key the item kind supports (the list above), so nobody has
to type seven keys. Strict default unchanged. `--anywhere` CLI flag applies it to sourceless
clauses. Pure loader expansion; no engine change.

## 2. `nthJoker` source — the real gap

Edition tags fire on the Nth base-edition joker generated in the ante, across rerolls,
skipping non-jokers and already-editioned jokers. Shop index is the wrong coordinate.
`sources: { nthJoker: [0, 1, 2, 3] }` = the joker is among the first N base jokers this ante.
Computed from the shop stream by filtering type==joker && edition==None.

With this, Lola's ask is exact, not "first three rerolls and hope":

      - and:
          - smallBlindTag: NegativeTag
            antes: [4]
          - joker: OopsAll6s
            antes: [4]
            min: 3
            sources: { nthJoker: [0, 1, 2, 3] }

Verified by hand on KGBY8NE2 ante 4: stream is HighPriestess, Dusk, Oops, Oops, Oops —
Oops at shop index 2 is joker index 1.

## 3. Tag-path check on tag sources

`uncommonTag: [0]` currently matches the pull stream even when no Uncommon Tag exists on
any reachable blind (1X6CD5J1 ante 5: stream[0] is Showman, tags are Buffoon/Investment/
Buffoon/Juggle). A source match should require the tag on a small/big blind of that ante
or the one before (skip fires in the next shop). Same for rareTag. Small check, real bug.

## 4. Ante 0 auto-compute

If ante 1's voucher is Hieroglyph, compute ante 0 without a clause that mentions it.

## 5. Jamlyzer scope = clause scope

Report only the antes / sources / kinds the clauses reference unless `--full`.

## Order

2, then 3, then 1, then 4, 5. The scratch/SolverRun folder and Motely/Analysis/MotelyItemSolver.cs
are a C# draft of 3 (the tag check) plus a path printer; keep or delete, they are not wired in.
