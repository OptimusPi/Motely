# Handoff — Claude work board (jaml-ui)

**Operator:** Nate  
**Map author:** Grok (paired with MotelyJAML board)  
**Executor:** Claude — **code tool only.** Tables, diffs, proof runs. No friend mode. No honey-soup.

**Law:** `CLAUDE.md` (design rules + pnpm). Enforcement: `.claude/hooks/check-design.mjs` + `eslint-rules/jaml-design.js`.  
**Do not** disable a design rule to land an edit. If blocked, change the approach or stop and report.

**Package:** `jaml-ui` **4.2.8** · sibling of MotelyJAML (same parent folder as `MotelyJAML` / `MotelyJAML` clone).

**Paths:** use the **workspace root** Claude already has open. Absolute paths differ by machine (`~/Documents/GitHub/jaml-ui` vs `D:\jaml-ui` vs …). Do not treat a foreign absolute path as a bug — resolve relative to this repo.

**Related (Motely):** Motely engine owns JAML grammar / search / WASM.  
**Motely.JsonRender is deleted** — do **not** recreate a C# HTML/jamlui renderer in Motely. This package is the UI skin.

**Board state:**

| Fact | Value |
|------|--------|
| Git | `master` clean w/ origin (as of map write) |
| Design system | **Jimbo** — `src/ui/jimbo.css`, `jimbo-tokens.css`, `Jimbo*` primitives |
| Old design | deleted `92cc8c2` (“kimi revival”) — do not revive |
| Jimbo migrate queue | defined by **per-file eslint baselines** on the phase map (repo total **430** at audit) — the `TODO(jimbo-primitives)` markers cover only P2–P5's 127 errors; P5b–P5d carry the other big files |
| Done | **P1** (`JamlCodeEditor` → `JimboCodeSurface`) · **P6** (SwipeDeck + Layout stories already shipped) |
| Detailed steps | also in `HANDOFFS.md` (this file = partner game + order + anti-soup) |

---

## How to play (partner game)

| Seat | Job |
|------|-----|
| **Nate** | picks phase token / says go / veto / publish go |
| **Claude** | **one phase per turn**; finish verb; handoff table; stop |
| **Grok** | wrote the map |

**Claude loop each phase:**

1. Read this file + `CLAUDE.md` design rules.
2. Do **only** the phase Nate named.
3. Package manager = **pnpm** (never `npm install <pkg>` that writes package-lock).
4. Proof: `npx eslint <phase file>` → **0 errors** (board lists each file's baseline), plus `pnpm build` and/or Storybook story console clean when claiming UI done. Build passing alone proves nothing — the build is green at 430 design errors.
5. End with:

| Field | Content |
|--------|---------|
| Doing | one verb |
| Where | path |
| Result | fact |
| Next | phase id or stop |

6. **No** “got it / absolutely / love that.” Ship artifact or sit still.

---

## Design hard rules (cheat sheet — full text in CLAUDE.md)

| # | Rule |
|---|------|
| 1 | **No flex** anywhere in `src/` — grid or absolute only |
| 2 | No raw `<button>`/`<input>`/… outside `src/ui/` — use `Jimbo*` |
| 3 | No emoji in UI — `react-icons/fi` |
| 4 | No ALL CAPS (acronyms OK) |
| 5 | No bold / font-weight 700+ |
| 6 | No `style={{}}` outside `src/ui/` except `--j-*` assignment or `style={style}` pass-through |
| 7 | `JimboColorOption` = canvas/R3F/SVG only; JSX uses `--j-*` |
| 8 | JSX helpers → `src/ui/` primitive + story |

**Migrate procedure (P1–P5 every file):**

1. List every `style={{ ... }}` **and every raw HTML tag** (`<div>`, `<span>`, …) in the file — `no-raw-html` is 287 of the repo's 430 errors; styles alone are ~⅓ of the real work.
2. Replace with `Jimbo*` primitives (`JimboPanel`, `JimboInset`, `JimboInnerPanel`, `JimboSectionHeader`, `jimboText`, …).
3. Else add `.j-*` in `jimbo.css` with existing `--j-*` tokens; layout = grid/absolute.
4. Reusable fragment → new `Jimbo*` + `.stories.tsx`.
5. Only allowed inline styles: custom props + pass-through.
6. Delete `TODO(jimbo-primitives)` header when done.
7. `pnpm build` green.

---

## Phase map

### P0 — Sanity

| Step | Check |
|------|--------|
| 1 | `git status` |
| 2 | `pnpm install` (if needed) |
| 3 | `pnpm build` |
| 4 | `pnpm typecheck` / `pnpm typecheck:all` if present |
| 5 | Optional: `pnpm storybook` (port 3141) — only if UI phase needs eyes |

**Done when:** build green table. No drive-by refactors.

---

### P0b — Fix `JimboIconButton` inline-flex (design system breaks rule 1)

| Field | Content |
|--------|---------|
| File | `src/ui/JimboIconButton.tsx` — line ~71, `display: "inline-flex"` |
| Baseline | **1** eslint error |
| Why first | rule 1 has no exemptions; nobody can rewrite a block touching this file without fixing it, so it blocks the primitives layer |
| Fix | grid (`display: "inline-grid"` + `place-items: center`) per rule 1's own guidance |

**Done when:** `npx eslint src/ui/JimboIconButton.tsx` → 0 · `pnpm build` green.

---

### P1 — Migrate `JamlCodeEditor.tsx` → Jimbo — **done**

| Field | Content |
|--------|---------|
| File | `src/components/JamlCodeEditor.tsx` |
| Detail | `HANDOFFS.md` Handoff 1 |
| Shipped | new `JimboCodeSurface` primitive + story; `.j-code-surface` class; `TODO` + `eslint-disable` header removed; raw `<div>` gone |
| Proof | eslint 0 problems **in that file** (was 1 × `no-raw-html`; repo-wide is not 0) · `pnpm build` · `pnpm typecheck:all` · `pnpm build-storybook` all green |
| Kept | `JimboColorOption` inside `EditorView.theme(...)` — JS-generated stylesheet (canvas class), not JSX; `.cm-activeLine` needs hex+alpha concat |

---

### P2 — Migrate `JamlMapPreview.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/JamlMapPreview.tsx` |
| Baseline | **18** eslint errors |
| Detail | `HANDOFFS.md` Handoff 2 |
| Note | canvas/SVG/R3F → `JimboColorOption`; JSX → `--j-*` |
| Warning | do **not** invent a ZoneRail API here — that extraction is P2.5, shared with P3 |

**Done when:** `npx eslint src/components/JamlMapPreview.tsx` → 0 · `pnpm build` green.

---

### P2.5 — Extract shared `JimboZoneRail` (before P3)

| Field | Content |
|--------|---------|
| Problem | `JamlMapPreview.tsx:115` and `JamlIdeVisual.tsx:257` each define a `ZoneRail` — different props, different chrome — plus parallel `ZONES` / `ZONE_META` color maps |
| Ship | one `JimboZoneRail` primitive in `src/ui/` + story, and **one** zone-meta token map both consumers read |
| Why its own phase | otherwise P2 silently sets the API for P3, or two near-twins ship (rule 8 sends both to one primitive) |

**Done when:** both files import the shared primitive · story renders · `pnpm build` green.

---

### P3 — Migrate `JamlIdeVisual.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/JamlIdeVisual.tsx` |
| Baseline | **43** eslint errors |
| Detail | `HANDOFFS.md` Handoff 3 |
| Depends | **P2.5** (consumes `JimboZoneRail`) |
| Note | drag/drop via `style={{ "--j-drag-x": … }}` + `.j-*` reader class |

**Done when:** `npx eslint src/components/JamlIdeVisual.tsx` → 0 · `pnpm build` green.

---

### P4 — Migrate `JamlIde.tsx` → Jimbo shell

| Field | Content |
|--------|---------|
| File | `src/components/JamlIde.tsx` |
| Baseline | **36** eslint errors |
| Detail | `HANDOFFS.md` Handoff 4 |
| Depends | **P1–P3 first** so extracted primitives exist (real dep: imports MapPreview, IdeVisual, CodeEditor) |
| Shape | compose `JimboApp` / `JimboLayout` / panels; no top-level JSX helpers |

**Done when:** `npx eslint src/components/JamlIde.tsx` → 0 · `pnpm build` green.

---

### P5 — Migrate `JamlMapEditor.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/jamlMap/JamlMapEditor.tsx` |
| Baseline | **30** eslint errors |
| Detail | `HANDOFFS.md` Handoff 5 |
| Depends | P1–P4 **soft** — primitive reuse only. `JamlMapEditor` imports none of them (only `jamlMap/*` + `src/ui/*`); it can run first or in parallel |
| Proof | `npx eslint src/components/jamlMap/JamlMapEditor.tsx` → 0 · plus `git grep 'TODO(jimbo-primitives)'` → **empty** (marker cleanup, **not** the completion gate — the markers cover only 127 of 430 errors) |

**Done when:** eslint 0 for this file; last migrate TODO gone; `pnpm build` green.

---

### P5b — Migrate `JamlyzerView.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/JamlyzerView.tsx` |
| Baseline | **110** eslint errors — the single worst file in the repo, bigger than P2+P3+P5 combined; it was missing from the original board |
| Extra | ship `JamlyzerView` ante-0 fix here only if Nate merges P7c into this phase — otherwise pure migrate |

**Done when:** `npx eslint src/components/JamlyzerView.tsx` → 0 · `pnpm build` green.

---

### P5c — Migrate `JamlyzerBulk.tsx` + `Jamlyzer.tsx` → Jimbo

| Field | Content |
|--------|---------|
| Files | `src/components/JamlyzerBulk.tsx` (**41**) · `src/components/Jamlyzer.tsx` (**27**) |
| Baseline | **68** eslint errors combined |
| Depends | P5b soft — shares Jamlyzer chrome; reuse whatever P5b extracts |

**Done when:** `npx eslint` → 0 for both files · `pnpm build` green.

---

### P5d — Migrate `src/json-render/components/*` → Jimbo

| Field | Content |
|--------|---------|
| Files | `domain.tsx` (**32**) · `reference.tsx` (**22**) · `mascot.tsx` (**10**) · `layout.tsx` (**6**) |
| Baseline | **70** eslint errors combined |
| Note | these are the json-render engine's own components — the package's headline feature renders through them |

**Done when:** `npx eslint src/json-render/components/` → 0 · `pnpm build` green.

---

### P6 — Stories: `JimboSwipeDeck` + `JimboLayout` — **done**

| Field | Content |
|--------|---------|
| Detail | `HANDOFFS.md` Handoff 6 |
| SwipeDeck | `Default` · `OneCard` · `Empty` · `DecisionLog` (callback payload on screen) — spec met |
| Layout | `Stack` · `StackGaps` · `Row` · `RowAlign` · `RowWrap` · `Composed`. The map said “every named region” — `JimboLayout` has no named regions, it is stack/row, so the stories cover the real API instead |
| Proof | `pnpm build-storybook` completes |

---

### P7 — Product gaps (from `TASKS.md` — pick one with Nate)

| ID | Verb | Notes |
|----|------|--------|
| P7a | In-app JAML authoring help | examples seed-finder / mcp — real hints from diagnostics, not a half-finished hunt |
| P7b | Unify vocab: `jaml-codemirror` + `jaml-lsp` | one module for `listItems`; stop double-fix drift |
| P7c | `JamlyzerView` ante-0 | today `1..n` only; ante 0 missing |
| P7d | Jamlyzer ante perf cliff | **track only — not pickable.** Motely C# (`analyzeSeeds` ante 39 hang) lives in MotelyJAML, and P9 says do not cross. Nate opens that lane in that repo or it stays a note |

**Done when:** that one ID ships with proof, or cancelled.

---

### P8 — Release 4.2.9 (operator gate)

| Step | Check |
|------|--------|
| 1 | P1–P6 **committed** (audit found P1's code and both handoff docs sitting uncommitted while the board said "clean") |
| 2 | `pnpm build` |
| 3 | banner check, cross-platform: `node -e "const fs=require('fs');for(const f of['dist/index.js','dist/ui.js'])if(!fs.readFileSync(f,'utf8').startsWith('\"use client\";'))throw new Error(f+' missing banner')"` |
| 4 | `dist/motely.js` bannerless (see commit ee0bc9b) |
| 5 | Bump **4.2.9**, commit message present-tense |
| 6 | `npm publish` **only if Nate says go** |

**Done when:** published + Nate confirms Next import of `JamlIde` under SSR, or publish deferred.

---

### P9 — Motely boundary (do not cross)

| Do | Don't |
|----|--------|
| Consume `motely-wasm` / engine types | Reinvent JAML grammar in TS |
| Keep Jimbo as only design system | Rebuild Motely.JsonRender |
| Fix UI bugs in this repo | “Salvage” by inventing parallel Motely UI project |

---

## Anti-soup checklist

Any **yes** → rewrite before send:

- [ ] Empty praise / apology essay / friend mode?
- [ ] Flex / raw button / emoji / ALL CAPS / bold / banned inline style?
- [ ] `eslint-disable` to dodge design rules?
- [ ] Claimed Storybook/build green without running?
- [ ] Recreated Motely.JsonRender or second design system?

Burn line:  
> Stop the honey-soup. Table or a real diff — no `soup()`.

---

## Operator quick-pick

| Token | Claude starts |
|-------|----------------|
| **P0** | sanity build |
| **P0b** | JimboIconButton inline-flex fix (1 line, unblocks primitives layer) |
| ~~P1~~ | ~~JamlCodeEditor → Jimbo~~ — done |
| **P2** | JamlMapPreview → Jimbo (18) |
| **P2.5** | extract shared JimboZoneRail (before P3) |
| **P3** | JamlIdeVisual → Jimbo (43) |
| **P4** | JamlIde shell → Jimbo (36) |
| **P5** | JamlMapEditor → Jimbo (30) — soft deps, can run any time |
| **P5b** | JamlyzerView → Jimbo (110, biggest file) |
| **P5c** | JamlyzerBulk + Jamlyzer → Jimbo (68) |
| **P5d** | json-render/components → Jimbo (70) |
| ~~P6~~ | ~~SwipeDeck + Layout stories~~ — done |
| **P7a–c** | product gap (name the letter; P7d = track only, not pickable) |
| **P8** | release 4.2.9 (needs go) |
| **B5** | stop |

---

## Bye handoff (Grok → Claude)

| Field | Content |
|--------|---------|
| Map | this file |
| Detail steps | `HANDOFFS.md` |
| Law | `CLAUDE.md` |
| Style | Jimbo only |
| Dead | Motely.JsonRender; pre-kimi design system |
| Alive | `Jimbo*` + IDE migrate queue + 4.2.8 package |
| Game | Nate picks phase · Claude executes one · tables only |

**Nate → Claude:**  
`Read HANDOFF-CLAUDE.md and do P0.`  
(or `P1` … `P8` with go)

**Claude:** finish that verb. Hand off. Stop.

bye handoff 2 — not friends, just the board.
