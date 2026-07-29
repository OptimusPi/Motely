# jaml-ui

JAML is **Jimbo's Ante Markup Language** — a DSL for Balatro seed filters. This package is
its UI: a video-game interface that ships **as an MCP app first**, rendered inside a host
iframe we do not control.

That last sentence is the reason for every design rule below. Read the rules before you
write UI code.

## Package manager

Package manager is **pnpm** (see `pnpm-lock.yaml`). Never run
`npm install <pkg>` to add/remove a dependency — it generates a stray `package-lock.json`
that conflicts with the committed pnpm lockfile. Use:

- `pnpm add -D <pkg>`
- `pnpm remove <pkg>`
- `npm run build` / `npm publish` are fine — those just run existing package.json scripts.

## Build

Build entries (`src/index.ts`, `src/ui.ts`, `src/motely.ts`) declare `"use client"`, but
Vite's library build strips module-level directives when bundling — verify `dist/*.js`
still starts with `"use client";` after any vite.config.ts change (see the `banner`
option in `rollupOptions.output`), or Next.js's RSC compiler will silently treat these as
Server Components and crash on any hook use.

## Design rules

These are enforced by `.claude/hooks/check-design.mjs`, a `PreToolUse` hook that **blocks
the write** (exit 2) before it lands. It is not a linter you can argue with after the
fact. `eslint-rules/jaml-design.js` mirrors some of these as a CI backstop.

**Do not disable a rule to make your edit go through.** If a rule blocks you, the rule is
the requirement and your approach is the thing that changes. Adding
`/* eslint-disable jaml-design/... */`, `@ts-ignore`, or `@ts-expect-error` to get past a
design rule is itself a violation — it converts a caught problem into a shipped one. If
you believe a rule is genuinely wrong for a case, stop and say so in your response rather
than routing around it.

### 1. No flex. Anywhere in `src/`.

`display: flex` and `display: inline-flex` are forbidden. So are `flex-direction`,
`flex-wrap`, `flex-grow`, `flex-shrink`, `flex-basis`, and the `flex` shorthand. This
applies to CSS files, `style={{}}` objects, and any generated style string.

**Why:** MCP host iframes size flex content differently per host. Flex is content-driven,
so the same markup reflows into a different layout depending on where it is embedded. This
UI must render identically in every host. Grid and absolute positioning are deterministic;
flex is not.

**Instead:** use `display: grid` or absolute positioning for all layout. `gap` is fine
inside a grid. If you are reaching for flex to center something, use
`grid` + `place-items: center`. If you are reaching for it to lay out a row of cards, use
`grid-auto-flow: column` with explicit track sizes.

### 2. No raw form/interactive elements outside `src/ui/`

No `<button>`, `<input>`, `<select>`, `<textarea>`. Use a `Jimbo*` primitive from
`src/ui/` (`JimboButton`, `JimboTextInput`, …). If the primitive you need does not exist,
add it to `src/ui/` with a story — do not inline a raw element "just this once".

### 3. No emoji in UI

Use `react-icons` (`react-icons/fi` preferred).

### 4. No ALL CAPS text

Jimbo design does not shout. Use normal case. Acronyms (`JAML`, `JSON`, `SIMD`) are fine.

### 5. No bold

No `fontWeight: bold` / `bolder` / `700`+. Jimbo design uses normal weight.

### 6. No inline `style={{}}` outside `src/ui/`

Compose a `Jimbo*` primitive or use a `.j-*` class. The **only** allowed shape is
assigning CSS custom properties — `style={{ "--j-card-width": `${w}px` }}` — which is how
`StandardCard` / `GameCard` / `DeckSprite` parameterize the sprite sheet. Passing a style
through (`style={style}`) is the caller's business and is fine.

### 7. `JimboColorOption` is for canvas / R3F / SVG only

Those surfaces cannot read CSS variables. In JSX, use the matching `--j-*` custom
property instead — putting `JimboColorOption` in a `style={{}}` duplicates the tokens and
breaks theming.

### 8. Helper components belong in `src/ui/`

No top-level `function Foo()` returning JSX inside a consumer screen. If a piece of UI is
reusable enough to extract, it is a Jimbo primitive with a story. Inline helpers are how
design drift starts.

## Design tokens

Tokens live in `src/ui/jimbo.css` as `--j-*` custom properties. Before adding one, check
whether an existing token covers it. A token earns its place by being **re-themed or
re-used**; a custom property that is set once and read once is just a variable with extra
steps, and it makes the stylesheet harder to hold in your head. Prefer a literal value
over a single-use token.
