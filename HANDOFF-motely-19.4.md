# Handoff — motely-wasm 19.4.0 migration

_Branch: `claude/eloquent-carson-whMh3`. (See `HANDOFF.md` for the earlier
`blissful-mendel` work — this file is additive, not a replacement.)_

## Where things stand

### PR #31 — Remove unused eslint-disable directives ✅ MERGED
Cleared 15 `react-hooks/rules-of-hooks` lint warnings in three Jimbo story files.
Comment-only; already in `master`.

### PR #32 — Update motely-wasm to 19.4.0 🟢 OPEN
https://github.com/OptimusPi/jaml-ui/pull/32
`pnpm typecheck` / `lint` / `build` all pass locally; Vercel preview deployed
clean. Re-check the GitHub `typecheck · lint · build` job's final state before
merge (webhooks don't deliver CI success).

## What 19.4.0 broke, and how it was handled

1. **Exports moved to subpaths.** `Motely` → `Program` (`motely-wasm/motely/wasm`);
   enums → `motely-wasm/motely/enums`; types → `motely-wasm/motely`; JAML/aesthetic
   → `motely-wasm/motely/filters/jaml`; analysis → `motely-wasm/motely/analysis`.
   20 import sites re-pointed; `Program` aliased to `Motely` so call sites unchanged.

2. **Jimmolate probe signature** changed `(seed, deck, stake)` → `(ctx)`. Bridged
   once in `src/lib/motely/runtime.ts` (`ctx.getSeed()/ctx.deck/ctx.stake`) so all
   consumers keep their existing predicate contract.

3. **Item/joker value enums DELETED** (`MotelyItemEdition/Seal/Enhancement`,
   `MotelyStandardcardRank/Suit`, `MotelyJoker*`). Vendored verbatim from 19.1.1
   into `src/lib/motely/motelyCompatEnums.ts` — the decoder needs their exact
   numeric bit-layout values. Delete the shim if the engine re-exposes them.

Also: `JamlAesthetic` gained native `Echo`, dropped `Psychosis` (fallback
simplified); peer floor → `>=19.4.0`; `CLAUDE.md` refreshed (incl.
`validateJaml` → `parseJaml`).

## Verify

```
pnpm install
pnpm typecheck   # clean
pnpm lint        # clean
pnpm build       # dist/ + d.ts
pnpm storybook   # port 3141
```
