# jaml-ui

React component library for Balatro / JAML seed-finder apps: game-card rendering,
the Jimbo design system, sprite metadata, the JAML editor/IDE, and optional
Motely (seed-search) helpers. Published to npm as `jaml-ui`; consumed by
`seedfinder.app` and other heads.

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
- `jaml-ui/r3f` — 3D card via React Three Fiber.

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

- **`package.json` `files` lists `jaml.schema.json`, but that file isn't in the
  repo** — referenced and never shipped. Either generate it from the MotelyJAML
  schema or drop it from `files`.
- Don't hand-roll Balatro item names or sprites — use the `core` exports
  (`JOKERS`, `VOUCHERS`, `TAGS`, sprite maps) so names stay in sync with the engine.
- **`src/lib/motely/motelyCompatEnums.ts`** vendors the item/joker enums
  (`MotelyItemEdition`/`Seal`/`Enhancement`, `MotelyStandardcardRank`/`Suit`,
  `MotelyJoker*`) verbatim from motely-wasm 19.1.1, because 19.4.0 *removed* them.
  The decoder relies on their exact numeric values for the packed-item bit layout.
  Delete the shim and re-import from the engine if motely-wasm re-exposes them.
