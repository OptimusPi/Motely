# jaml-ui handoff — new primitives drop-in

This folder is a **drop-in addition** to your `jaml-ui` repo. It adds the
component primitives we built in the Balatro Seed Curator project, plus the
matching CSS additions. **It does not touch existing components.**

## What's in here

```
handoff/jaml-ui/
└── src/ui/
    ├── jimbo.css           ← canonical jimbo.css (with tab-bounce and panel-sheen fixes)
    ├── jimbo-addons.css    ← CSS for the new primitives
    ├── layout.tsx          ← JimboStack, JimboRow, JimboDivider
    ├── display.tsx         ← JimboStatCallout, JimboStatusPill, JimboPriceTag, JimboSidewaysLabel
    ├── feedback.tsx        ← JimboToast, JimboTooltip, JimboErrorBlock, JimboProgressBar
    ├── controls.tsx        ← JimboToggle, JimboToggleRow
    ├── chrome.tsx          ← JimboSectionHeader, JimboMarquee
    ├── prompts.tsx         ← JimboConfirmPrompt
    └── index.ts            ← barrel re-export
```

## How to apply

1. **Copy** every file in `handoff/jaml-ui/src/ui/` into the same path in
   your jaml-ui repo. (Files use `.js` import extensions to match your
   existing convention — they work as-is with your build.)

2. **Replace** `src/ui/jimbo.css` if you want the three CSS fixes that came
   out of testing:
   - Tab indicator bounce: re-enabled (was wrongly stripped during debug)
   - Tab container: `padding-top: 6px; padding-bottom: 8px;` so the bounce
     and the button drop-shadow aren't clipped
   - Panel sheen `::before` overlays: **disabled** (`display: none`) — they
     were creating a hallucination-bright moving gradient that's not in
     real Balatro

   If you don't want those, leave your existing `jimbo.css` alone and just
   add `jimbo-addons.css` next to it.

3. **Import the addons CSS** in your app entry (wherever you import
   `jimbo.css` today):

   ```ts
   import './ui/jimbo.css'
   import './ui/jimbo-addons.css'  // ← add this
   ```

4. **Re-export from the package barrel.** Your `src/ui/index.ts` (or
   wherever you re-export from) should add:

   ```ts
   export * from './layout.js'
   export * from './display.js'
   export * from './feedback.js'
   export * from './controls.js'
   export * from './chrome.js'
   export * from './prompts.js'
   ```

5. **Run your build + tests.** Nothing in here imports anything that
   doesn't already exist in jaml-ui (only `panel.js`, `jimboText.js`).

## Conventions matched

- `'use client'` directive on every file (Next.js / RSC compatible)
- `JimboX` naming
- TypeScript with exported `Props` interfaces and tone/size string unions
- `.js` import extensions (ESM resolution)
- One domain per file, components grouped by purpose

## What I did NOT touch

- Any existing `Jimbo*.tsx` file
- Your build config, package.json, tsconfig
- Your stories or tests

## Conflicts to watch for

- **`JimboSectionHeader`** — if you already have a component by this name
  in the repo, rename one. The version in `chrome.tsx` is the **red strip
  + downward triangle** marker from real Balatro (the "active tab card"
  pattern). If you already use the name for something else, rename this
  one to `JimboSectionStrip` and update `chrome.tsx` + its export.

- **`JimboStack` / `JimboRow`** — these are generic layout helpers; if you
  already have flex utilities elsewhere, you may not want these. Safe to
  delete the file and remove from `index.ts`.

## Visual reference

Every component matches what's in `Balatro Seed Curator/ComponentShowcase.html`
and the three screen mocks (`JAML Browser.html`, `JAML Seed Finder.html`,
`JAMLyzer.html`). Open those for visual reference while integrating.
