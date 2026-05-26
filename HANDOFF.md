# HANDOFF — `jaml-ui` for the next agent

Date: 2026-05-19, end of a long session. Audience: the next Claude (or human)
opening this repo. The user has been grinding this codebase for ~13 months;
their patience for re-derived facts is zero. Read this before you act.

---

## Latest session — 2026-05-20

State on exit: branch `chore/storybook-cleanup-backlog`, **working tree clean
(all committed)**, `pnpm typecheck` + `pnpm build` green. Storybook was running
on `:3141` (dev server, `allowedHosts: true` so the `motelyjaml-pi.8pi.me`
tunnel + LAN IP reach it) — restart with `pnpm exec storybook dev -p 3141`.

Done this session:
- **#16 + parallel batch (#15/#40/#50) closed.** Added `### Fonts` to CLAUDE.md
  (`--j-font` UI vs `--j-font-code`). Marked done in TASKS.md.
- **#18 KEYSTONE done** — the 320×568 story harness. The global decorator in
  `.storybook/preview.tsx` (JimboBackground outside + JimboApp lock, global
  `layout: 'fullscreen'`) was already in place; the work was the *sweep*: ~22
  stories flipped off `jimboHarness:false`+`layout:'centered'`+arbitrary-frame
  decorators onto the default harness. Documented opt-out exceptions:
  `jimboBackground` (the swirl itself), `JimboPanelSplitter` (`'fluid'`, #72),
  `Showcase` (owns its own JimboApp). Verified visually (Curator, CodeEditor,
  CategoryPicker, JokerPicker). Picker bottom-overflow is **expected** — it's
  the per-component debt (#42/#45/#43/#44/#24) the old 320×520 sandboxes were
  hiding, not a harness bug. TASKS.md #18 marked ✅.
- **motely-wasm 18.1.1 → 18.2.1** via `pnpm update`. Codegen moved exports:
  fixed 4 hook imports → `MotelyDeck`/`MotelyStake` from `motely-wasm/motely/enums`,
  `JamlAesthetic` from `motely-wasm/motely/filters/jaml`. Dep also got
  restructured (by a sibling commit) to devDep `^18.2.1` + peer `>=18.1.0` —
  correct for a library. Deleted the stale `motely-wasm-18.2.0.tgz`.
- **Killed the local-tgz-override doc.** CLAUDE.md `### Updating motely-wasm`
  now says `pnpm update`, period. NOTE: the bad advice also lives in an
  auto-memory note `motely-wasm-local-publish-flow` — delete/edit that note or
  it re-infects CLAUDE.md.

Next per TASKS.md sequencing: foundations **#13** (shadow tokens — tokenize the
30%→80% press-shadow + kill soft-blur outliers), **#65** (color sprawl),
**#54** (panel bottom-edge clip). Or the now-visible picker overflow #42/#45.

Behavioral note from this session: the user explicitly called out sycophancy
and editorializing — including moralizing prose baked into CLAUDE.md. State the
mechanism, do the work, skip the verdicts. CLAUDE.md/docs are for facts only.

---

## Do not re-derive

These are facts I (or prior agents) **got wrong** and the user had to correct.
Don't relearn them the hard way.

### Sprite cell facts in `assets/Enhancers.png` (7 columns × 5 rows)

| Cell | What's there |
| ---- | --- |
| (0, 0) | **Red deck-back** (NOT a blank base, NOT the Ace) |
| (1, 0) | **Plain blank card body.** This is the "card base" layer for unenhanced StandardCards. |
| (2, 0) | Gold seal art |
| Rows 2–3 | The rest of the deck-backs (Blue, Yellow, Green, Black, Magic, Nebula, Ghost, Abandoned, Checkered, Erratic, Painted, Anaglyph, Plasma, Zodiac, Challenge). Full map in `src/components/DeckSprite.tsx::DECK_SPRITE_POS`. |
| Row 1 | The 8 enhancements (Bonus/Mult/Wild/Lucky at row 1, Glass/Steel/Gold/Stone on row 0/1). See `src/sprites/spriteData.ts::ENHANCER_MAP`. |
| (4–6, 4) | Purple / Red / Blue seals. |

**Do not say "no deck-back asset exists."** It does. It's in Enhancers.png.
The standard playing-card faces are in `assets/8BitDeck.png` (13×4 = 52 face
cells; no deck-backs there).

### `src/ui/sprites.tsx::DeckSprite` vs `src/components/DeckSprite.tsx`

The `components/DeckSprite.tsx` is the **canonical** deck-with-stake-sticker
component. It samples Enhancers.png with the full 17-deck map and overlays
a stake sticker via `stickers.png`. `src/ui/sprites.tsx::DeckSprite` is a
narrower variant I (badly) rewrote to use cell (0,0); use the `components/`
one for anything user-facing.

### motely-wasm boot is INLINED, not wrapped

Per `AGENTS.md` line 16-26 and `CLAUDE.md` line 53: there is no
`ensureMotelyReady`, no `MotelyProvider`, no `useMotelyRuntime`. The
canonical pattern is top-level `await bootsharp.boot("/motely-wasm/bin")`
in the consumer entry point. Hooks that need it inline the Standby-guard.
**Don't reintroduce a wrapper** — the user has explicitly killed two prior
attempts.

`src/lib/motely/runtime.ts` still exports `ensureMotelyReady` and 9 callsites
still use it (task #6). That's a load-bearing refactor; do it deliberately
in its own session, not as a side quest.

---

## Hard rules (CLAUDE.md / AGENTS.md — read both)

These ALL come from user instructions in this session. Each one has a real
example of me violating it earlier. They are not aesthetic preferences;
they are the design contract.

1. **iPhone SE 5 portrait, 320×568, HARD LOCKED.** Every consumer (MCP App,
   desktop, browser). No scroll, no stretch, no reflow. `.j-app` is
   `width/min/max: 320px; height/min/max: 568px;`. If a component doesn't
   fit at 320×568, redesign the component, not the lock.

2. **Every component is a `Jimbo*` component.** No raw `<button>`, no
   anonymous inline helpers. Missing primitive? Add a `Jimbo*` to
   `src/ui/`, give it a story, export from `src/ui.ts`. **Don't bulk-add
   three or four "in case" — pick the one you need and ship it.**

3. **No emoji as icons.** Use `react-icons` (`react-icons/fi`). But also
   don't render `<FiCopy />` directly inside a `JimboIconButton` and call
   it done — wrap behavior into a Jimbo component (e.g. `JimboCopyButton`).
   The library is *a React component library*, so behavior + glyph + state
   belongs in one reusable shell.

4. **Item names go in `JimboTooltip`**, not inline labels. Sprite + tooltip.
   Don't fight for spacing labeling things players already recognize.

5. **No grey text on grey backgrounds.** `tone="grey"` on `--j-darkest`,
   `--j-dark-grey`, `--j-teal-grey`, or `--j-surface-inset` is grey-on-grey.
   `--j-grey` has been bumped to `#a8bcbf` so the tone reads on dark panels.

6. **No tone-swapping on copy buttons.** Red stays red on click; only the
   label changes to "Copied". Buttons don't switch red→green.

7. **No gold buttons or gold badges anywhere.** Gold is for text-on-dark
   (prices, titles, headers) only. `JimboBadgeTone` no longer accepts `gold`.

8. **`overflow: hidden` on panels is forbidden.** Balatro deliberately
   bleeds cards/sprites past panel edges. `.j-panel`, `.j-inner-panel`,
   `.j-panel-spinner__panel` are all `overflow: visible`. Description
   clipping (for the wiggle fix) lives on the description element itself,
   not on the panel.

9. **Inner panels have the same light-silver border as outer panels.** A
   previous "no borders on inner surfaces" rule was wrong — confirmed
   against the Credits screen (IMG_3 in the session, showing Publishing /
   Localization / Porting / Testing each with the silver outline).

10. **Settings stories are SEED-SEARCHER settings.** No Pixel Art Smoothing,
    no CRT slider, no Music Volume. This is not the game — it's the seed
    curator. Examples: Worker threads, Ante depth, Max results, Time budget.

11. **`Back` button is auto-injected by `JimboModal` via `showBack`.**
    Don't also add a custom "Back" in the action row — two stacked Back
    buttons happened twice already.

12. **No "fake" stories that hardcode fixtures and pretend to be real.**
    `Jamlyzer.stories.tsx`'s `seed === 'ALEEB'` string comparison still
    pretends to be the analyzer. (Task #9, still open.)

---

## What's in the library now (the design system)

Components added or hardened this session — use these, don't reinvent:

| Component | Purpose |
| --- | --- |
| `JimboTabs` / `JimboVerticalTabs` | Tabs are JimboButtons with a red bouncing-triangle indicator. Only the arrow animates. |
| `JimboButton` | Reserves padding-bottom for its drop-lip so `overflow:hidden` parents can't clip the shadow. Drop-lip is 30% black. |
| `JimboValueBadge` | Red pill that displays a number; click → inline `<input type=number>`; Enter/blur commits with clamp+snap. Used as slider thumb. |
| `JimboSlider` | Dark trough, red fill, JimboValueBadge thumb at the fill boundary (NOT pinned at the right). Click the thumb to edit the number. |
| `JimboSpinner` | `< value >` two-arrow cycler. Formerly misnamed `JimboStepper`. Use this for any "pick one of N" control — even what used to be `JimboSelect` is now a thin wrapper around this. |
| `JimboStepper` | **Page-dot indicator.** Filled white = current, grey = others. Optional `onIndexChange` makes dots clickable. |
| `JimboPanelSpinner` | Spinner + media/title/description panel. Panel height is locked at 140px so descriptions can't wiggle the modal. |
| `JimboTile` | Lowest-level sprite primitive: `<JimboTile sheet="enhancers" x={1} y={0} />`. Use when you have raw coords. |
| `JimboCopyButton` | The canonical clipboard button. Single red tone, label "Copy" → "Copied" for 1.5s, then back. Used by `JimboCopyRow` and `JimboCodeBlock`. **Do not hand-roll another copy button.** |
| `JimboPanelSplitter` | (Renamed from `PanelSplitter`.) Slides all the way to either edge so one pane can fully collapse. |
| `JimboBackground` | Full shader props: `primary`/`secondary`/`dark` colors (hex or `[r,g,b]`), `speed`, `spinRotation`, `spinAmount`, `pixelFilter`, `contrast`, `lighting`, `transitionMs`. Color changes lerp over `transitionMs`. |

Pending consolidations (clipboard hand-rolls that should switch to
`JimboCopyButton`): `src/components/JamlCurator.tsx::handleCopySeed` and
`src/components/JamlSeedSpinner.tsx::handleCopy`. Both also have inline
`<LuCopy size={12} />` lucide-react icons that violate the "react-icons,
not lucide" preference.

---

## What's open

Task IDs match the in-session TaskList state at the time of writing.

- **#6 rip `ensureMotelyReady`.** 9 callsites: useSearch, useAnalyzer,
  useJamlLibrary, useSearchPool, searchWorker, searchPoolWorker,
  JamlIde.stories, .storybook/preview.tsx, motely.ts. Breaking change.
- **#7 strip dead Tailwind classes.** `src/ui/ide/JamlEditor.tsx`,
  `src/ui/radial/*`, `src/ui/mascot/SeedMascot.tsx` use Tailwind utilities
  in a repo with no Tailwind. They're no-op CSS classes. Replace with
  inline styles or `.j-*` utility classes.
- **#9 real Jamlyzer/Showcase stories.** Replace the `seed === 'ALEEB'`
  fixtures with a story that boots motely and runs
  `Motely.analyzeJamlSeeds`. Hard because the storybook preview already
  inlines `ensureMotelyReady` — wait until #6 is done.
- **#13 redesign `JimboInfoCard`.** User flagged it as "not terrible but
  pretty made up." The likely real pattern from screenshots is a
  full-color button card (red/blue/teal/purple/orange) with the title +
  state on the colored surface and no separate aside slot — see Credits
  screen (IMG_3) and Collection grid (IMG_3680).
- **#20 wire `JimboTooltip` on every sprite render.** Strip inline name
  labels. Sprites: JimboSprite, StandardCard, DeckSprite, StakeSprite,
  JamlMapEditor pickers, JamlCurator, CardList, Showcase hot-filters.
- **#22 legendary joker animated faces.** 6 jokers (Canio, Triboulet,
  Yorick, Chicot, Perkeo, Hologram) need their animated face overlay
  stacked on the static body, per Blueprint's animation pattern.
  `JOKER_FACES` in `src/sprites/spriteData.ts` already declares them with
  `animated: true`; the rendering just doesn't honor it yet.

Closed task IDs (don't reopen): 1, 2, 3, 4, 5, 8, 10, 11, 12, 14, 15, 16,
17, 18, 21. Task #19 was **deleted** because its title carried my wrong
original claim ("no deck-back asset exists"); the actual issue was
resolved before the task was filed.

---

## Behavior traps you will fall into

1. **Filing tasks for every user comment instead of fixing inline.** The
   user explicitly called this out as thrashing. If a comment is one CSS
   tweak, do it in the file and move on. Reserve TaskCreate for things
   that span files or need a real plan.

2. **Asking "which do you want?" instead of doing.** When the user
   already gave guidance ("grey", "make it 30%", "320×568"), implement
   it. AskUserQuestion is for actual forks, not for re-confirming.

3. **Over-rationalizing rules into AGENTS.md.** Don't legislate from every
   utterance. The user once corrected me on this — they were venting
   observations, not writing policy.

4. **Claiming "no asset exists" or "no pattern in screenshots" without
   reading first.** Read the image. Verify. The screenshots in
   `examples/screenshots2/` are 31 images, fully visible to the Read tool.

5. **Building shallow wrappers (JimboIconButton + naked `<FiCopy />`)
   instead of full-behavior components (`JimboCopyButton` that owns the
   clipboard logic AND the label state AND the timing).** The library
   is for *reusable behavior*, not just styled containers.

6. **Reverting fixes the user already shipped.** Earlier I removed inner
   panel borders thinking they were invented; they weren't (see Credits
   screen). Earlier I claimed cell (0,0) was the plain card base; it's
   cell (1,0). Don't second-guess the user's primary-source feedback.

---

## File map (the bits you'll touch first)

```
src/
├── ui/                       ← Jimbo design system, src/ui.ts barrel
│   ├── panel.tsx             JimboPanel, JimboButton, JimboBackButton, JimboModal
│   ├── jimbo.css             The single stylesheet, ~2900 lines
│   ├── JimboCopyButton.tsx   Canonical copy primitive — USE THIS
│   ├── JimboValueBadge.tsx   Click-to-edit numeric pill
│   ├── JimboSlider.tsx       Dark trough + red fill + ValueBadge thumb
│   ├── JimboSpinner.tsx      `< value >` cycler (formerly JimboStepper)
│   ├── JimboStepper.tsx      Page-dot indicator
│   ├── JimboTile.tsx         Raw (sheet, x, y) sprite primitive
│   ├── JimboTabs.tsx         Buttons + bouncing arrows
│   ├── JimboBackground.tsx   Parameterized WebGL swirl
│   └── ...
├── components/               ← Higher-level game components
│   ├── StandardCard.tsx      Layered card render (enhancer base + deck face + edition + seal)
│   ├── DeckSprite.tsx        Deck-with-stake-sticker (canonical)
│   ├── CardFan.tsx           Sine-arc fan, smile direction
│   └── ...
├── sprites/
│   └── spriteData.ts         SPRITE_SHEETS, ENHANCER_MAP, JOKERS, JOKER_FACES, …
├── assets.ts                 Vite-bundled PNG imports
└── hooks/                    motely-wasm hooks (still using ensureMotelyReady — task #6)

examples/
└── screenshots2/             ← Real Balatro screenshots. Reference these before inventing.

CLAUDE.md                     ← Read this. Top of file. Always.
AGENTS.md                     ← And this. Source of truth for design rules.
```

---

## Last word

The user has built this design language carefully for a year. Treat their
words as ground truth, treat the screenshots as ground truth, treat your
own training as suspicious. Read before you write. Use the library you're
sitting on. Don't rebuild what's already there.

Good luck.
