# jaml-ui — Design & Cleanup Backlog

Captured from a component-by-component Storybook walkthrough with pifreak. Every item has file:line evidence and the *why*. This is the durable copy of the harness task list.

**Core framing:** jaml-ui = the missing app that makes Motely real. MotelyJAML has the superior engine + the JAML filter format (30 readable lines compiling to 149 lines of hand-written SIMD), but shipped no app in 13 months, so the community still uses the slower legacy Immolate because it's *usable*. This backlog ships the app. Two machines, one language (JAML): **FILTER** = breadth, SIMD, millions/sec, match/no-match; **ANALYZER (Jimmolate)** = depth, one seed, every stream. Same JAML in.

**Canonical surface:** iPhone SE 5 portrait, **exactly 320×568, hard locked.**

---

## 📐 PINNED RULES (encode into CLAUDE.md/AGENTS.md + eslint-rules/jaml-design.js)

- **#2 PRIME DIRECTIVE — no re-making, no drifting.** jaml-ui is the single source of truth; build once, apps compose. Never re-implement/fork. Fix the canonical component, never a copy.
- **#3 100% JimboUI.** Every line of UI is a `Jimbo*` primitive or a jaml-ui component composed from them. Missing primitive → add a `Jimbo*` (with story), never hand-roll a one-off.
- **#17 EXACTLY 320×568, swirl OUTSIDE the lock.** App surface is precisely 320×568 edge-to-edge. The swirl background lives outside it, visible only around the surface on larger viewports. No swirl through the UI, no inset margin.
- **#20 A story is JUST the component.** Story render = `<Component {...realProps}/>` (+ at most the canonical 320×568 harness). No hand-rolled tabs/chrome/fake narrative scaffolding. **Fixing a story means fixing the COMPONENT, not faking it in the story.** (Temporary exception: scraping prior-bot slop.)
- **#24 Sprites/card art large + ALWAYS overflow.** Item art renders big and overflows its slot; never clipped/covered. Native card width = **71px** (`StandardCard.tsx:10`).
- **#37 REFERENCE: real Balatro design language** (from `examples/screenshots2/`). Buttons: chunky, red=normal, orange=Back/confirm, hard 80% bottom shadow, white m6x11. Back = orange, full-width, **compact**, bottom-hugging. Spinners: small red `[<] [>]` arrows + dark recessed value pill. Panels: opaque teal-grey, state via **border color** not opacity. Category lists: 2-column grid w/ count subtitle. Seed: `Seed: XXXXXXXX [Copy]` compact row. Tones: blue=info, gold=money, red=danger/score, orange=primary, grey/brown=inactive/disabled. **15 decks** confirmed.
- **#52 Color AND unorthodox shape are UX/identity.** Colorful chunky buttons = thumb targets (color+position = zero-friction tapping). Never flatten to grey/uniform. **Striding buttons (prev/next arrows) = full height of their row** (intentional, the exception to top-anchored text). Don't normalize weird-but-deliberate shapes.
- **#76 Interactive feedback changes appearance in place, never layout/position.** Reserve fixed footprint for the widest state. (Anti-shift — see family below.)
- **#16 Document the font system.** All text = `--j-font` (`'m6x11plus','m6x11',monospace`); code = `--j-font-code` (`'JetBrains Mono','Roboto Mono',monospace`). Currently undocumented anywhere.

> **Highest-leverage move:** `eslint-rules/jaml-design.js` already has `no-raw-button`/`no-emoji-jsx`/`no-uppercase-text`/`no-bold-style`. Add lint rules for these where detectable — rules that bite automatically beat prose nobody reads. (This is what erraticdeck lacked: a doc with no teeth.)

---

## 🛡️ PROTECTED — do NOT touch/delete

- **#49 Card3D (r3f) + Selectable Fan** — real Balatro 3D magnetic tilt. Good, just not wired yet. "Not used yet" ≠ dead.
- **#70 SeedMascot** — keep for later; comment out only if it breaks the build, never delete.
- **#72 JimboPanelSplitter** — good; intentionally NOT for single 320 portrait. Foundation for future multi-panel "god mode" (tile surfaces, splitters, swipe, N searchers; hook = `.j-app--fluid` `jimbo.css:1533`). Build the 320 unit first, tile later.
- **Also good (keep):** JimboTooltip ("perfect"), Toggle list, Info Cards (just +2px row gap #57), JimboCodeBlock, the IDE Default story, drag-zone hover highlight, the search results view.

---

## 🗑️ DELETE PILE

- ✅ **#5 CardList** — DONE (component + story + barrel + CSS removed).
- ✅ **#29 JamlWorkbench** — DONE (orphaned shell + dup toolbar story).
- ✅ **#55 JimboVerticalTabs** — DONE (kept horizontal JimboTabs).
- ✅ **#58 JimboStatGrid + JimboInset** — DONE.
- ✅ **#68 JimboTile** — DONE (redundant w/ JimboSprite).
- ✅ **#71 JimboFloating** — DONE (unused corner-anchor positioner).
- ✅ **#59 (part)** Share/"Shared!" demo variant removed (`Jimbo.stories.tsx`).
- ⏳ **#28 JamlMapPreview** — DELETE but requires rewiring to JamlMapEditor; do with the shell build.
- ⏳ **#8 showcase.tsx** — fake app-shell slop (invented hotFilters/recentFinds; Default missing JimboPanel; buttons have own bg + do nothing). Keep-or-kill decision pending.

---

## 🏗️ FOUNDATIONS (each unblocks many — do before the shell)

- **#18 ONE canonical 320×568 story harness** ⭐ KEYSTONE. Stories use arbitrary frames (296, 300×420, 400, 320×520, layout:'centered' + Storybook padding). Build one fullscreen decorator = the locked `.j-app` 320×568, swirl outside, zero padding. Kills margin/cutoff/off-center/swirl-inside across the whole library (#41, #69, parts of #19/#39/etc.). Do this ALONE after the parallel batch.
- **#13 Shadow tokens.** Real language = hard-offset zero-blur. Soft-blur outliers `jimbo.css:2064` (`0 8px 16px`), `:2040` (`0 3px 8px`) are the "huge ugly" ones. Button press shadow is `rgba(0,0,0,0.3)` = 30% in 5 spots (`:374,809,1200,2116,2465`) → should be **80%**. Tokenize once.
- **#65 Color sprawl.** ~67 inline color values (51 hex + 16 rgba) in TS/TSX, mostly slightly-off dupes of the 33 `--j-` tokens (`#e4b643`≈gold, `#429f79`≈green, `#ff4c40`≈red…). Consolidate to tokens (JimboColorOption JS constants only for R3F/canvas). Remove **teal-grey AND grey as button tones** (teal-grey stays a surface).
- **#54 Panel bottom-edge clip.** `.j-app` `overflow:hidden` (`:1528`) + `.j-app__scroll > .j-panel{flex:1 1 auto}` (`:1567`) slices the panel's bottom 3D lip (predicted at `:334`). Reserve bottom room ≥ `--j-panel-south-edge-width + --j-surface-shadow-depth`.

### Quick parallel batch (independent files — safe to dispatch together)
- **#15** Add Roboto Mono to `--j-font-code` (`jimbo.css:88`, one line).
- **#40** Remove grey line above footer: `.j-footer__bar` `border-top` (`jimbo.css:1256`).
- **#50** `line-height:1` clips m6x11 glyphs (`.j-text--display :175`, `--label :215`, `--micro :221`, footer `:1266`). Bump to ~1.1–1.2; sweep all `line-height:1`.
- **#16** Font docs (CLAUDE.md only).
- Rule encoding into `eslint-rules/jaml-design.js` + CLAUDE.md/AGENTS.md.

---

## ↔️ ANTI-SHIFT FAMILY (one rule #76, six bugs)

- **#12 JimboPanelSpinner** — sizes to content; cycling resizes the panel. Fixed footprint.
- **#36 JamlSpeedometer** — `toLocaleString()` variable width + `flexWrap:wrap` (`:67`) → reflows/whips on every WASM tick (1–5/s). Also accidental glass (`rgba bg + light border :71-72`); 100% inline styles (comment lies "uses j-stat-grid"). Fix: SI fixed-width numbers (`211.0 S/s`/`1.74 K/s`/`1.29 M/s`) via **JimboDualChip** (#64) fixed halves; opaque; de-inline. Place as a **tab in the IDE**.
- **#56 JimboFlankNav** — active item resizes on click, shifting edges. Fixed footprint.
- **#59 Copy button** — `Copy`→`Copied` label swap resizes. Feedback = icon→**green check** + seed text→**green "COPIED!"** ~1s then revert; button never changes. Also split CopyRowAndSelect story (`:333`, two components). Delete Share variant ✅.
- **#63 JimboSlider** — value chip rides the thumb (`left:${pct}%` `:53-61`); should be **static right-aligned**, click→type. Regression.
- **#67 JimboValueBadge** — swaps to `<input>` on edit → resizes (`:69-80`); font −1pt. **This IS the slider's value chip.** Fix once, reconcile edit UX (mini-modal vs inline) for both.

---

## 🔧 PER-COMPONENT FIXES

- **#1 CardFan** — kill fake narrative stories ("Opening hand" etc.) + JimboPanel/header scaffolding; default ~52-card fan. Fix off-center bug (left-of-center in 8-card story). Dead-app story didn't survive — author fresh.
- **#6 DeckSprite AllDecks** — clipped (raw `<div width:400>` > 320, raw div). Rebuild ≤320 w/ Jimbo grid. Use canonical `DECK_OPTIONS` (15 decks; story array had 13, missing Nebula/Zodiac).
- **#7 JamlGameCard Showcase story** — fake rarity ladder + raw divs. GameCard is pure UI (plain descriptor, NOT packed-int; decode lives one layer up). Show real props (editions/eternal/foil/playing). Jaml-vs-Jimbo naming: deferred.
- **#11 JamlAestheticSelector** — wrong control (numeric badge + `<`/`>` text). Use JimboStepper dots (6 modes) + chevron icons.
- **#21 JimboTabs** — active triangle bounces infinitely (`jimbo.css:582`). Bounce once then settle.
- **#22 JamlIdeVisual** — hand-rolled inline styles + hardcoded hex (`#4db5ff` etc.). Convert to Jimbo + tokens. ⭐ **QUALITY BAR** — visually "nearly awesome"; pixel-preserve the look.
- **#23 Drag ghost** — positioned by raw clientX/Y (`ui/hooks.ts:733`), no clamp → overflows 320. Clamp to surface. (Drop-zone hover highlight = correct, keep.)
- **#25 Category-TYPE menu** (`JamlMapEditor.tsx:58`) — fat flat bars, tiny icons, inline subtitle labels (violate tooltip rule), overflows. Redesign as 2-col Balatro grid, big sprites, tooltips.
- **#26 Back button** — too tall/huge in ALL panels+modals (category menu, JimboInputModal…). Shrink at shared `.j-back-btn` (`jimbo.css:308`): orange/full-width/**compact**/bottom-hugging.
- **#30 JamlIdeToolbar** — button top-spacing wrong (Balatro top-anchors text padding); tabs should feel like a controllable wizard flow; story floats (#18).
- **#32 Drop-zone flash** — keep flash (Balatro real), but NOT the source/hovered zone; flash other valid targets. `j-glow-pulse infinite :1330/1335/1340`. Visual: white edges, more-colorful fill, white m6x11 label ("Add to Must") — "fat" via pixel font size, not bold.
- **#33 JamlSeedInput yucky** — kill gold text (`:2341`), gold caret, gold focus border (`:2360`). Recessed dark inset, white/cream m6x11, normal/blocky-white caret; keep green data-valid.
- **#34 JamlSeedInput clips 8 chars** (FROGMANS→FROGMA). `width:calc(8ch+16px)` + border-box eats the buffer + `letter-spacing:.08em` (`:2334,2345,2349`). Widen to fit 8 chars + spacing + padding + borders.
- **#35 JamlSeedSpinner** — floating labels; copy doubles icon+text; add iOS copy bubble; fixed `MMMMMMMM` width; oversized; arrows are `<`/`>` glyph slabs → small chevrons.
- **#39 Magic sizes** — `size=100/64/48/54` are meaningless; use native **71** or clean scale.
- **#42 CategoryPicker** — drop search box + inline labels; name in JimboTooltip on hover (less code; frees space for #24 overflow). Keep grid + voucher pairing.
- **#43 JokerPicker** — invisible hand-rolled search (borrows seed-input 8ch-gold classes, `:118-124`); legendary-first ordering (`NON_LEGENDARY :78`). Remove search; order common→legendary.
- **#44 Legendary jokers render no face** — base layer only, face layer missing. Port from `x:/Blueprint/.../cards.tsx` (base + face-by-name compositing). `GameCard.tsx:283` `JOKER_FACES.find` likely excludes the 5 legendaries (row y:8). DATA DRIFT: joker list duplicated in `lib/const.ts:8` AND `sprites/spriteData.ts:56` — reconcile.
- **#45 Picker layout** — voucher 6-wide (no scroll), joker 5-wide; rarity separators Common→Uncommon→Rare→Legendary.
- **#46 MysterySlot** — remove dashed borders (Balatro has none; resolves #19 dashed mystery — real component styling, not Storybook); fix unclear surface.
- **#9 Pickers** — CategoryPicker already generic (7 configs); gaps = story shows only Vouchers; Joker special-cased (fold in or justify).
- **#51 Buttons** — top-anchored text on tall buttons (Balatro); striding=full-height exception (#52); fix "Large Orange (Back)"; grey tone = disabled only.
- **#53 JimboBadge** — broken to flat ("no edges" `:487/500`). Should be plateau: face color, NO drop shadow, dark-tone bottom edge (red→dark-red). Button=pushy (shadow); badge=grounded (edge).
- **#57 Info Cards** — +~2px row gap (touching).
- **#62 JimboStepper** — dots are indicator-only, NOT clickable nav (remove `j-stepper__hit` button variant). Pair with spinner arrows for navigation.
- **#74 JimboBackground** — drop gold "Background" story text; expose shader uniforms (`ui/hooks.ts:140` pixelFilter [Balatro default **740**; current 740×0.33≈244], spin/twist, swirl, colors) as React props; more stories across ranges. Ref `x:/Jammy` (junk).
- **#75 Sprites** — make HighQuality the DEFAULT (default is pixel-mashed).
- **#66 Text Input** — slightly over-engineered; fine for now (low priority simplify later).
- **#69 App Shell story** — stray pointless Search button; show real shell instead.

---

## 🔌 CONSOLIDATIONS (gated on #10)

- **#10 CORE — build a NEW UNIFIED shell** (decided). Absorbs JamlIde tabs+editor (#14), search wiring (#27), load-JAML (#47), speedometer tab (#36); JamlCurator/SeedFinder/Workbench(✅del)/Showcase merge or die; canonical map = JamlMapEditor. Spine: author → search → results+speedometer → analyze (Jimmolate). Build AFTER foundations + bridge.
- **#14 Two CodeMirror editors** — IDE uses `components/JamlCodeEditor` (no autocomplete); `ui/ide/JamlEditor` wires `jamlCompletion` but is orphaned. Consolidate to ONE that wires completion.
- **#28 JamlMapPreview** — delete, rewire to JamlMapEditor (with shell).
- **#44 joker faces / data drift** (see above).
- **#48 JamlMapEditor** — remove hacked-in opacity (`:188` 0.4, `:310` 0.7); draw sprites full opacity, only PNG alpha. State via border/highlight not dimming.
- **#19 JamlCurator/JamlMapEditor builder** — transparent (swirl bleeds through), overflows, empty skeleton. Opaque per #37; dashed = #46.

---

## 🚀 BIG BUILDS

- **#4 JimboDeckAndStakeSelector** — extract+rename from `RunConfigModal.tsx` (already 2 spinners + DECK/STAKE data). One combined component. Fixed footprint (#12). Source: DECK_OPTIONS/STAKE_OPTIONS + DESCRIPTIONS.
- **#61 JamlSearchSettingsModal** — consolidate scattered settings (worker count, seed count, deck/stake via #4, aesthetic via #11). Aesthetic forces single-worker (reflect in UI).
- **#60 Verify useSearchPool** (worker-threaded search) actually works (no dup/overlap; aesthetic=1 worker).
- **#27 Wire real search** — JamlIde.onSearch is a callback hole; WithSearch story fakes it. Wire JAML → useSearchPool → results+speedometer.
- **#47 SeedFinder** — proves search works (results great) but hand-rolled in story, **can't load a JAML (PRIORITY)**, inline speed instead of JamlSpeedometer. Folds into the unified shell.
- **#64 JimboDualChip** — too tall, font −1pt, trash story spacing; the vehicle for speedometer stats + finds with fixed-width halves.
- **#38 Jimmolate** — the COMPLETE single-seed analyzer (NOT a filter). Analyzer = "function with a shitload of params" → **parameterized by JAML** (no params-monster). One language (JAML), two modes: filter=breadth, analyze=depth. Engine = `MotelySingleSearchContext`. Must cover ALL streams (lucky mult/money, uncommon tag, …) — `analyzer.cl` is legacy/incomplete, layout-only ref. Instant via motely (vs Immolate ~5min GPU). Data: `MotelyJamlyzerResult`.
- **#31 CORE — JAML↔visual bridge** (BLOCKED on user's JAML POCO). No `parse(string)→model` / `serialize(model→string)` exists; `jamlParser.ts` only does highlighting → why visual+text can't sync. JAML string is source of truth. Align model to the forthcoming POCO + `jaml.schema.json`. Stress-test vs `x:/Immolate/filters/perkeo_observatory.cl` + real `JamlFilters/*.jaml` (voucher chains, antes, or:, sources.boosterPacks, edition, score, judgement, shop-slot ranges). Enables #73.
- **#73 Per-clause JAML preview** (feature) — hover/select a clause in visual editor → its JAML in JimboCodeBlock = `serialize(singleClause)`. Teaches JAML. Needs #31.

---

## SEQUENCING

1. ✅ Delete pile (done; build green).
2. Parallel batch: #15, #40, #50, #16, + rule encoding (eslint + CLAUDE.md).
3. #18 story harness (keystone, alone).
4. Foundations: #13, #65, #54.
5. Anti-shift family (#12/#36/#56/#59/#63/#67) + per-component fixes.
6. Consolidations + #4, #64.
7. **#31 bridge** (when POCO lands) → **#10 unified shell** + #14/#27/#28/#47.
8. #38 Jimmolate, #61 settings, #73 preview.

**Reference repos:** `x:/Immolate` (legacy OpenCL filters/analyzer), `x:/JammySeedFinder/src/MotelyJAML` (real JAML + native SIMD filter descs), `x:/Blueprint` (card rendering origin), `x:/Jammy` (junk; background params ref). `examples/screenshots2/` = real Balatro reference.
