# HANDOFF — what changed, what's verified, what's left

Branch: `claude/blissful-mendel-Nzt84` (8 ahead of `master` v1.0.2).
Rule that drove every change: **no schema drift — bind to `motely-wasm`, invent
nothing.** Full diagnosis in `FINDINGS.md`; history in `CHURN.md`.

## Done & verified (green here: `pnpm typecheck` ✅ `pnpm lint` ✅ `pnpm build` ✅)

### P0 — `Echo`, drift-free  (`src/components/JamlAestheticSelector.tsx`)
The label stays **"Echo"**. The id now binds to whatever aesthetic member the
engine actually exposes:
```ts
const ENGINE_AESTHETIC = JamlAesthetic as unknown as Record<string, JamlAesthetic | undefined>;
const ECHO_AESTHETIC: JamlAesthetic = ENGINE_AESTHETIC.Echo ?? JamlAesthetic.Psychosis;
```
This is **Track B**: a self-upgrading bridge. It compiles today against the
installed engine (which exposes the index-1 identifier) and **automatically adopts
a native `JamlAesthetic.Echo` the moment that ships — no second commit here.**

> **Track A (yours, upstream `motely-jaml`):** rename the index-1 aesthetic in
> `jaml-lang/src/authoring.ts`, regenerate `vocab.generated.ts`, publish a new
> `motely-wasm`. When you do, bump the `motely-wasm` peer/dev range here; the line
> above then resolves to the native member and the fallback becomes dead code.
> (I'm scoped to `optimuspi/jaml-ui` this session, so the upstream commit is
> yours — happy to take `motely-jaml` too if you add it to the session.)

### P1 — Engine-derived joker rarity  (`JokerPicker.tsx`, `jokerRarity.ts`)
- Deleted the ~90-name hand-typed Sets. `getJokerRarity` now reads membership from
  `MotelyJokerCommon/Uncommon/Rare`; Legendary = `MotelyJoker` minus those three.
- Names are matched by lowercase-alphanumeric normalization (engine `GreedyJoker`
  ↔ sprite `Greedy Joker`), with one explicit alias: engine spells "8 Ball" as
  `EightBall`. If a future Balatro/engine update adds a divergent spelling, add it
  to `DISPLAY_NAME_ALIASES` — that's the single intended maintenance point.
- Renamed the internal tier enum `MotelyJokerRarity → JokerRarityTier` (it's a UI
  tag, not an engine type). **Public `JokerRarity` type is unchanged** (still
  exported from `src/index.ts` / `jamlMap/index.ts`). `MysterySlot.tsx` updated.
- Verified the 7 previously-mislabeled jokers (DNA, Vagabond, Baron, Obelisk,
  Baseball Card, Ancient Joker, Campfire) now resolve to **Rare**.

### P2 — Honest manifest  (`package.json`)
Removed `jaml.schema.json` from `files` (no such file; validation is delegated to
the engine by design).

## Not changed — flagged for you

- **`mustnot` (visual) vs `mustNot` (serialized JAML)** casing is intentional and
  handled by the adapters (`utils/jamlVisualFilter.ts`, `JamlMapEditor.tsx`). Left
  as-is; noting it because it surprises people.
- **Stray Storybook debris**: `.sb-run.log`, `.sb-run2.log`, `.sb-run3.log` were
  committed by `c2efc1b "jesus"`. They look like junk and probably want
  `git rm` + a `.gitignore` entry — left untouched since they're yours to confirm.
- **Version**: still `1.0.3`. Bump on next publish if you cut a release.

## Verification handoff (needs a screen — I can't eyeball from the cloud)

Run `pnpm storybook` (port 3141) on the box that has a display and confirm:
1. **Aesthetic selector** spinner reads **"Echo"** (not the old token), cycles
   through Palindrome / Echo / Gross / Funny / Balatro.
2. **JokerPicker** shows the 5 legendaries in the Legendary row, and spot-checked
   rarities look right (DNA / Vagabond / Baseball Card now Rare; Greedy Joker
   Common; 8 Ball Common).
3. **Card3D** (`jaml-ui/r3f`) renders and does its magnetic tilt.

## Next step

This branch → PR into `master` (master is missing Card3D, the typed decoder,
`CLAUDE.md`, and these fixes).
