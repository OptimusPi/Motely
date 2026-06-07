# jaml-ui

React component library for Balatro / JAML seed-finder apps: game-card rendering,
the Jimbo design system, sprite metadata, the JAML editor/IDE, and optional
Motely (seed-search) helpers. Published to npm as `jaml-ui`; consumed by
`seedfinder.app` and other heads.

## HARD RULES — non-negotiable (read first, every time)

These are the author's standing requirements. They are not suggestions and they
override convenience. Do not relitigate them.

1. **Fixed size. NEVER fluid or responsive.** iPhone SE portrait = **320×568
   total**, and that total is TWO stacked fixed pieces, not one:
   - app shell `.j-app` = **320×540** (`--j-app-w` / `--j-app-h`)
   - `JimboBalatroFooter` below it = **320×28** (`--j-footer-h`)
   - 540 + 28 = 568. **The shell is 540, NOT 568.** 568 is shell + footer.
   No fluid widths, no media queries, no reflow, ever. Everything is built as
   **fixed, composable panels**.
2. **Jimbo-UI primitives ONLY — no raw HTML/React tags in components.** No bare
   `<div>`, `<span>`, `<button>`, etc. in feature components. Compose from `Jimbo*`
   primitives. `JimboBox` (`ui/jimboLayout`) is the one sanctioned neutral
   structural element; raw DOM tags live **only inside leaf primitives**.
3. **No inline styles, no raw hex.** Style through `j-*` token classes and the
   `--j-*` CSS variables only. (Legacy files carry `TODO(jimbo-primitives)`.)
4. **NO FLEX. Not anywhere, not for anything.** Not centering, not layout, not
   "just this once," not "but it's not for centering." The canvas is fixed — use
   `margin:auto` and grid. If you typed `display:flex` (or `flex`, `inline-flex`,
   `flex-direction`, …) you broke the rule. There is no centering exception
   because there is no exception. NO FLEX.
5. **Single thread.** Default to one worker/thread unless explicitly told otherwise.
6. **RULE #1 — never add anything the author didn't ask for.** No invented tools,
   primitives, abstractions, dependencies, or "helpful" extras without explicit
   consent. Don't steamroll. Don't doctor stories/output to *fake* "done." Confirm
   before any irreversible or outward-facing action (publish, force-push, deletes).

## Commands (pnpm — `pnpm-lock.yaml` is the lockfile)

- `pnpm build` — Vite library build → `dist/` (the published artifact).
- `pnpm dev` — `vite build --watch`.
- `pnpm typecheck` — `tsc --noEmit`.
- `pnpm lint` — ESLint (repo-local rules live in `eslint-rules/`).
- `pnpm storybook` — Storybook on port 3141; `pnpm demo` runs the `demo/` app.

## Package surface (the public API — keep it honest)

Subpath exports:

- `jaml-ui` — main: game cards, the JAML IDE, search hooks.
- `jaml-ui/ui` — Jimbo design system (buttons, panels, tokens).
- `jaml-ui/core` — sprite metadata + assets; pure (no React, no motely-wasm).
- `jaml-ui/motely` — re-exports from `motely-wasm` + packed-item decoders.

`jimbo.css` is the stylesheet (side-effect import); `fonts.css` ships the fonts.

## Integration facts

- **`motely-wasm` is a peer dependency** (`>=19.4.0`) — the AOT/SIMD seed engine.
  The consuming app boots it once; jaml-ui uses the booted engine. As of 19.4.0
  the engine API is split across subpath exports: the old `Motely` namespace is
  now `Program` (imported from `motely-wasm/motely/wasm`), enums live under
  `motely-wasm/motely/enums`, types under `motely-wasm/motely`, and JAML/aesthetic
  types under `motely-wasm/motely/filters/jaml`.
- **Validation is delegated to the engine, not done here:** call
  `Motely.parseJaml(jaml)` (the `Program` namespace) — it throws on invalid JAML,
  otherwise returns the parsed config. jaml-ui ships no JSON-schema validator of
  its own. (19.4.0 removed the old `validateJaml` string API.)
- **YAML parsing:** `js-yaml` for full parses; CodeMirror `@codemirror/lang-yaml`
  for editor highlighting; lightweight line parsers in `src/utils/` for the
  visual preview (kept dependency-free on purpose).

## JAML model

The editor binds to a flat visual model (`JamlVisualFilter` / `JamlVisualClause`
/ `JamlZone`, exported from `JamlIde`): `must` / `should` / `mustnot` zones, each
clause a `{ type, value, ...modifiers }`. Note the visual zone key is lowercase
`mustnot`, while serialized JAML uses `mustNot`.

The authoritative grammar lives upstream in MotelyJAML
(`jaml-lang/src/authoring.ts` + `vocab.generated.ts`). Don't invent enum
values here — pull them from `motely-wasm`.

## Known gaps / gotchas

- Don't hand-roll Balatro item names or sprites — use the `core` exports
  (`JOKERS`, `VOUCHERS`, `TAGS`, sprite maps) so names stay in sync with the engine.
- **`src/lib/motely/motelyCompatEnums.ts`** vendors the item/joker enums
  (`MotelyItemEdition`/`Seal`/`Enhancement`, `MotelyStandardcardRank`/`Suit`,
  `MotelyJoker*`) verbatim from motely-wasm 19.1.1, because 19.4.0 *removed* them.
  The decoder relies on their exact numeric values for the packed-item bit layout.
  Delete the shim and re-import from the engine if motely-wasm re-exposes them.
