# jaml-ui — open work

The previous version of this file was a component-by-component Storybook
backlog (Jimbo*, JamlMapEditor, CategoryPicker/JokerPicker, the r3f card
table…) against a design system deleted wholesale in commit `92cc8c2`
("kimi revival", 2026-07-03). None of those files exist anymore — see
`HANDOFF.md` for what replaced them. Re-filing tasks against the old file
map wastes a session; this list starts fresh from what's actually here.

## Open

- **In-app JAML authoring help.** There is currently no live guidance surface
  in `examples/mcp-seed-finder` or `examples/seed-finder` — no inline hints,
  no hover docs, no "why did this fail" surfaced from `jaml-codemirror`'s
  diagnostics beyond raw squiggles (if even wired — check
  `SeedFinderApp.tsx`'s usage of `jaml-codemirror` props before building).
  This is the concrete ask behind "make JAML authoring more helpful" — build
  it, don't look for a half-finished version.
- **Unify vocab sourcing between `jaml-codemirror` and `jaml-lsp`.** Both
  packages independently call `MotelyJaml.listItems(kind, "")` from their own
  `vocab.ts`. They already drifted once (the `Any`-wildcard fix landed twice,
  separately, in commit `4e88c2f`). Extract one shared vocab module (a new
  small package, or fold into `jaml-lang`) both packages import, so the next
  fix doesn't need to happen twice.
- **`MotelyJamlyzer.analyzeSeeds` perf cliff.** Ante 8 ≈ 3ms, ante 39 hangs
  the tab past 30s (synchronous, computes every ante any clause references).
  `analyzeSeedsPaged`/`resumeSeeds` hit the identical wall. This is a Motely
  (C# engine) fix, tracked here because it blocks anything in this repo that
  wants to jamlyze past ~ante 10 without a client-side ante clamp.
- **`JamlyzerView` ante-0 gap.** Caps `maxAnte=8`, generates ante buttons
  `1..n` and never `0`. Not swapped in anywhere yet (seedfinder.app's
  hand-rolled X-ray does ante-0 correctly and is used instead) — fix before
  the next consumer reaches for this component.

## Closed 2026-07-06

- 5 lint errors (`packages/jaml-codemirror`, `packages/jaml-lsp`, one stray
  `@storybook/react` import).
- `pnpm typecheck` gap — added `typecheck` scripts to all three `packages/*`
  members plus a root `pnpm typecheck:all`.
