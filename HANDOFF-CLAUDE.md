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
| Jimbo migrate queue | `TODO(jimbo-primitives)` still on IDE files (see P1–P5) |
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
4. Proof: `pnpm build` and/or Storybook story console clean when claiming UI done.
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

1. List every `style={{ ... }}` in the file.
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

### P1 — Migrate `JamlCodeEditor.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/JamlCodeEditor.tsx` |
| Detail | `HANDOFFS.md` Handoff 1 |
| Proof | `pnpm build`; IDE editor stories render; no `TODO(jimbo-primitives)` on this file |

**Done when:** migrate procedure complete for this file only.

---

### P2 — Migrate `JamlMapPreview.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/JamlMapPreview.tsx` |
| Detail | `HANDOFFS.md` Handoff 2 |
| Note | canvas/SVG/R3F → `JimboColorOption`; JSX → `--j-*` |

**Done when:** same as P1 for this file.

---

### P3 — Migrate `JamlIdeVisual.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/JamlIdeVisual.tsx` |
| Detail | `HANDOFFS.md` Handoff 3 |
| Note | drag/drop via `style={{ "--j-drag-x": … }}` + `.j-*` reader class |

**Done when:** same as P1 for this file.

---

### P4 — Migrate `JamlIde.tsx` → Jimbo shell

| Field | Content |
|--------|---------|
| File | `src/components/JamlIde.tsx` |
| Detail | `HANDOFFS.md` Handoff 4 |
| Depends | **P1–P3 first** so extracted primitives exist |
| Shape | compose `JimboApp` / `JimboLayout` / panels; no top-level JSX helpers |

**Done when:** same as P1 for this file.

---

### P5 — Migrate `JamlMapEditor.tsx` → Jimbo

| Field | Content |
|--------|---------|
| File | `src/components/jamlMap/JamlMapEditor.tsx` |
| Detail | `HANDOFFS.md` Handoff 5 |
| Depends | P1–P4 |
| Proof | `git grep 'TODO(jimbo-primitives)'` → **empty** |

**Done when:** last migrate TODO gone; `pnpm build` green.

---

### P6 — Stories: `JimboSwipeDeck` + `JimboLayout`

| Field | Content |
|--------|---------|
| Detail | `HANDOFFS.md` Handoff 6 |
| Template | `JimboPicker.stories.tsx` |
| SwipeDeck | default, one-card, empty, callback payload on screen |
| Layout | every named region filled so boundaries visible |

**Done when:** both in Storybook; clean console.

---

### P7 — Product gaps (from `TASKS.md` — pick one with Nate)

| ID | Verb | Notes |
|----|------|--------|
| P7a | In-app JAML authoring help | examples seed-finder / mcp — real hints from diagnostics, not a half-finished hunt |
| P7b | Unify vocab: `jaml-codemirror` + `jaml-lsp` | one module for `listItems`; stop double-fix drift |
| P7c | `JamlyzerView` ante-0 | today `1..n` only; ante 0 missing |
| P7d | Jamlyzer ante perf cliff | **Motely C#** (`analyzeSeeds` ante 39 hang) — track here, fix in MotelyJAML if Nate opens that lane |

**Done when:** that one ID ships with proof, or cancelled.

---

### P8 — Release 4.2.9 (operator gate)

| Step | Check |
|------|--------|
| 1 | P1–P6 merged |
| 2 | `pnpm build` |
| 3 | `head -c 40 dist/index.js` and `dist/ui.js` start with `"use client";` |
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
| **P1** | JamlCodeEditor → Jimbo |
| **P2** | JamlMapPreview → Jimbo |
| **P3** | JamlIdeVisual → Jimbo |
| **P4** | JamlIde shell → Jimbo |
| **P5** | JamlMapEditor → Jimbo (last migrate) |
| **P6** | SwipeDeck + Layout stories |
| **P7a–d** | product gap (name the letter) |
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
