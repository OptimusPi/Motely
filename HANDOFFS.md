# jaml-ui handoffs

Work items to finish the package. Each is self-contained. Do them in order.
Read `CLAUDE.md` before writing code. Package manager is pnpm.

---

## Handoff 1 — Migrate `JamlCodeEditor.tsx` to Jimbo primitives

**File:** `src/components/JamlCodeEditor.tsx`

1. List every `style={{ ... }}` in the file.
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
two allowed style shapes; `pnpm build` passes; the IDE stories render in
Storybook.

---

## Handoff 2 — Migrate `JamlMapPreview.tsx`

**File:** `src/components/JamlMapPreview.tsx` — same procedure as Handoff 1.

Color sourcing on this screen: canvas/SVG/R3F draws use `JimboColorOption`;
JSX uses `--j-*` custom properties. Each element picks the one matching its
render surface.

**Done when:** same criteria as Handoff 1.

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

**File:** `src/components/jamlMap/JamlMapEditor.tsx` — largest, last. Reuse
the primitives extracted in 1-4.

**Done when:** same criteria as Handoff 1, and
`git grep 'TODO(jimbo-primitives)'` returns nothing.

---

## Handoff 6 — Stories for `JimboSwipeDeck` and `JimboLayout`

The two primitives in `src/ui/` still needing `.stories.tsx`.

1. Use `JimboPicker.stories.tsx` as the template.
2. `JimboSwipeDeck.stories.tsx`: default deck, one-card deck, empty deck, and
   a story that displays the swipe callback's payload on screen.
3. `JimboLayout.stories.tsx`: every named region filled with a placeholder
   panel so boundaries are visible.

**Done when:** both render in Storybook with a clean console.

---

## Handoff 7 — Release 4.2.9

After 1-6 are merged.

1. `pnpm build`.
2. Verify `dist/index.js` and `dist/ui.js` begin with `"use client";`
   (`head -c 40 dist/index.js`); `dist/motely.js` ships bannerless (see commit
   ee0bc9b). Adjust the `banner` option in `vite.config.ts` if needed and
   rebuild.
3. Bump to 4.2.9, commit
   `Release 4.2.9: jimbo-primitives migration complete`, `npm publish`.

**Done when:** published, and a Next.js app imports `JamlIde` and renders it
under SSR.
