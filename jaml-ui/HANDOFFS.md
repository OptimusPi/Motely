# jaml-ui handoffs

Work items to finish the package. Each is self-contained. Do them in order.
Read `CLAUDE.md` before writing code. Package manager is pnpm.

---

## Handoff 1 — Migrate `JamlCodeEditor.tsx` to Jimbo primitives — done

**File:** `src/components/JamlCodeEditor.tsx`

Shipped: `src/ui/JimboCodeSurface.tsx` + `.stories.tsx` (mount point for a
JS-managed editor view, `minHeight` → `--j-code-surface-min-height`), the
`.j-code-surface` class in `jimbo.css`, and removal of the inline style, the
raw `<div>`, and the `TODO` / `eslint-disable` header. `JimboColorOption` stays
in `EditorView.theme(...)` — that is a JS-generated stylesheet, not JSX.

Procedure below kept as the template for Handoffs 2-5.

1. List every `style={{ ... }}` **and every raw HTML tag** in the file.
   `no-raw-html` is the dominant cost (287 of 430 repo errors) — a file with
   zero inline styles can still carry dozens of raw `<div>`s to migrate.
2. Replace each styled element with an existing primitive from `src/ui/` —
   check `JimboPanel`, `JimboInset`, `JimboInnerPanel`, `JimboSectionHeader`,
   `jimboText` first.
3. Where a primitive doesn't fit, move the declarations into a `.j-*` class in
   `src/ui/jimbo.css`, using existing `--j-*` tokens for color and spacing.
   Layout is grid or absolute positioning.
4. Where a fragment is reusable across screens, extract it as a new `Jimbo*`
   primitive in `src/ui/` with a `.stories.tsx` file.
5. Keep exactly two inline-style shapes: custom-property assignment
   (`style={{ "--j-x": value }}`) and pass-through (`style={style}`).
6. Delete the `TODO(jimbo-primitives)` header comment when done.

**Done when:** the file consists of Jimbo primitives, `.j-*` classes, and the
two allowed style shapes; `npx eslint <file>` reports **0 problems for that
file** (state the scope — repo-wide is a different number); `pnpm build`
passes; the IDE stories render in Storybook.

---

## Handoff 2 — Migrate `JamlMapPreview.tsx`

**File:** `src/components/JamlMapPreview.tsx` — same procedure as Handoff 1.

Color sourcing on this screen: canvas/SVG/R3F draws use `JimboColorOption`;
JSX uses `--j-*` custom properties. Each element picks the one matching its
render surface.

**Done when:** same criteria as Handoff 1 (baseline: 18 errors). Do not design
a ZoneRail API inside this file — that is Handoff 2.5.

---

## Handoff 2.5 — Extract shared `JimboZoneRail`

`JamlMapPreview.tsx:115` and `JamlIdeVisual.tsx:257` both define a `ZoneRail`
with different props and chrome, plus parallel `ZONES` / `ZONE_META` color
maps. Rule 8 sends both to one primitive.

1. Read both ZoneRail implementations and list the union of what they render.
2. Ship one `JimboZoneRail` in `src/ui/` with a `.stories.tsx`, and one
   zone-meta map sourced from `--j-*` tokens.
3. Point both consumers at it; delete the local copies and color maps.

**Done when:** both files import `JimboZoneRail`; the story renders;
`pnpm build` passes. Must land **before** Handoff 3 starts.

---

## Handoff 3 — Migrate `JamlIdeVisual.tsx`

**File:** `src/components/JamlIdeVisual.tsx` — same procedure as Handoff 1.

Drag ghosts and drop zones position via custom properties:
`style={{ "--j-drag-x": `${x}px` }}` paired with a `.j-*` class that reads the
variable.

**Done when:** same criteria as Handoff 1.

---

## Handoff 4 — Migrate `JamlIde.tsx`

**File:** `src/components/JamlIde.tsx` — the shell composing editor, visual
builder, and preview. Do after Handoffs 1-3 so their extracted primitives are
available; this file becomes composition of `JimboApp` / `JimboLayout` /
panels. Every piece of JSX-returning helper logic lives in `src/ui/` as a
primitive with a story.

**Done when:** same criteria as Handoff 1.

---

## Handoff 5 — Migrate `JamlMapEditor.tsx`

**File:** `src/components/jamlMap/JamlMapEditor.tsx` (baseline: 30 errors).
Dependency on 1-4 is **soft** — this file imports none of them (only
`jamlMap/*` and `src/ui/*`), so it can run first or in parallel; just reuse
primitives from 1-4 where they already exist.

**Done when:** same criteria as Handoff 1, and
`git grep 'TODO(jimbo-primitives)'` returns nothing. The empty grep is marker
cleanup, **not** proof the migration is complete — the markers sit on 4 files
worth 127 errors while the repo holds 430; Handoffs 5b-5d carry the rest.

---

## Handoff 5b — Migrate `JamlyzerView.tsx`

**File:** `src/components/JamlyzerView.tsx` — same procedure as Handoff 1.
Baseline: **110 errors**, the single largest file in the repo; it was absent
from the original queue.

**Done when:** same criteria as Handoff 1.

---

## Handoff 5c — Migrate `JamlyzerBulk.tsx` + `Jamlyzer.tsx`

**Files:** `src/components/JamlyzerBulk.tsx` (41) and
`src/components/Jamlyzer.tsx` (27) — same procedure as Handoff 1. Reuse
whatever Jamlyzer chrome Handoff 5b extracts.

**Done when:** same criteria as Handoff 1 for both files.

---

## Handoff 5d — Migrate `src/json-render/components/*`

**Files:** `domain.tsx` (32), `reference.tsx` (22), `mascot.tsx` (10),
`layout.tsx` (6) — same procedure as Handoff 1. These are the json-render
engine's own components.

**Done when:** `npx eslint src/json-render/components/` reports 0 problems;
`pnpm build` passes.

---

## Handoff 6 — Stories for `JimboSwipeDeck` and `JimboLayout` — done

Both `.stories.tsx` files exist and build. `JimboSwipeDeck` covers default,
one-card, empty, and a decision-log story showing the callback payload.
`JimboLayout` has no named regions — it is stack/row — so its stories cover
that API (`Stack`, `StackGaps`, `Row`, `RowAlign`, `RowWrap`, `Composed`)
rather than the region grid item 3 below describes.

1. Use `JimboPicker.stories.tsx` as the template.
2. `JimboSwipeDeck.stories.tsx`: default deck, one-card deck, empty deck, and
   a story that displays the swipe callback's payload on screen.
3. `JimboLayout.stories.tsx`: every named region filled with a placeholder
   panel so boundaries are visible.

**Done when:** both render in Storybook with a clean console.

---

## Handoff 7 — Release 4.2.9

After 1-6 are **committed** (not just sitting in the working tree — the audit
caught finished work uncommitted while the board claimed clean).

1. `pnpm build`.
2. Verify `dist/index.js` and `dist/ui.js` begin with `"use client";` —
   cross-platform check (this board runs on Windows too, no `head`):
   `node -e "const fs=require('fs');for(const f of['dist/index.js','dist/ui.js'])if(!fs.readFileSync(f,'utf8').startsWith('\"use client\";'))throw new Error(f+' missing banner')"`
   `dist/motely.js` ships bannerless (see commit ee0bc9b). Adjust the `banner`
   option in `vite.config.ts` if needed and rebuild.
3. Bump to 4.2.9, commit
   `Release 4.2.9: jimbo-primitives migration complete`, `npm publish`.

**Done when:** published, and a Next.js app imports `JamlIde` and renders it
under SSR.
