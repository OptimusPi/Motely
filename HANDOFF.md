# HANDOFF — `jaml-ui` for the next agent

Read this before you act. It replaces the pre-2026-07 version of this file,
which described a design system (`src/ui/` Jimbo* components, the
`JamlMapEditor` visual builder, `JamlIde`/`JamlEditor`, r3f card table,
`apps/balatro-seed-app`) that **no longer exists in this repo.** That
architecture was deleted wholesale — 269 files, -25106/+16376 lines — in
commit `92cc8c2` ("kimi revival", 2026-07-03), and the repo rebuilt around
`src/json-render/` instead. If you go looking for `JimboButton`,
`JimboSlider`, `JamlMapEditor`, `CategoryPicker`, `JokerPicker`, or the old
`JamlIde`, you will not find them. They are gone on purpose, not hidden or
broken. Don't resurrect them, don't file tasks against them, and don't trust
`git blame`-adjacent docs that still describe them as current (that's what
happened here — `HANDOFF.md`/`TASKS.md` survived the rewrite and kept
describing the old world untouched, which is exactly the confusion this
rewrite fixes).

## What's actually here now

Per `CLAUDE.md` (read it, it's the maintained source of truth):

```
src/
├── json-render/          Zero-dependency JSON-to-React engine (React-only dep)
│   ├── engine.tsx         Core renderer
│   ├── catalog.ts         Balatro component catalog registration
│   ├── registry.tsx       Component registry
│   ├── builders/          JSON tree builders
│   ├── components/        domain.tsx, game.tsx, layout.tsx, mascot.tsx, reference.tsx
│   ├── knowledge/         Reference data fed to builders
│   └── stories/           Storybook stories for all of the above
├── components/            GameCard, StandardCard, DeckSprite, JamlyzerView, cardEnums
├── decode/                motelyItemDecoder/Formats/Sprite — WASM item → sprite mapping
├── render/                CanvasRenderer, Layer (pure, no React), useJamlCardRenderer
├── sprites/                spriteData.ts (SPRITE_SHEETS, ENHANCER_MAP, JOKERS, JOKER_FACES),
│                          spriteMapper.ts
├── lib/motely/            motely-wasm glue
├── ui/                    Just CSS now: jimbo.css, jimbo-tokens.css, tokens.ts
│                          (the Jimbo *component* library that used to live here is gone —
│                          only the design tokens/stylesheet survived)
├── motely.ts              re-exports bootsharp from motely-wasm
├── core.ts / index.ts / ui.ts   subpath export barrels (see package.json "exports")
└── assets.ts              Vite-bundled PNG imports

examples/
├── mcp-seed-finder/       The shipping MCP App (SeedFinderApp.tsx, mcp-app.tsx, main.tsx)
└── seed-finder/           Plain web variant of the same app

packages/
├── jaml-codemirror/       CodeMirror 6 language support (diagnostics + completions).
│                          private:false, real vite build, imported by both example apps.
├── jaml-lang/             VS Code extension shell (grammar, language-configuration,
│                          `extension.ts` LSP client). Confusingly named — in MotelyJAML
│                          (the engine repo) the *extension* is called `jaml-lsp` and the
│                          *language core* is `jaml-lang`. Here it's inverted. Don't assume
│                          parity between the two repos' `jaml-lang`/`jaml-lsp` just because
│                          the names match — they're independent implementations.
└── jaml-lsp/              The actual LSP server (vscode-languageserver, completions,
                           hover, definitions, diagnostics). Bundled into jaml-lang's
                           extension via esbuild (`jaml-lang`'s `bundle` script pulls in
                           `../jaml-lsp/src/server.ts` directly). Depends on `motely-wasm`
                           to fetch live vocabulary at runtime via `loadVocabulary()`.
```

`examples/mcp-seed-finder` and `examples/seed-finder` depend on `jaml-codemirror`
+ `jaml-ui` (workspace-local). Neither depends on `packages/jaml-lang` or
`packages/jaml-lsp` — those two only ship as the standalone VS Code extension,
which is a separate deliverable from the web/MCP app, not dead weight.

## Facts still worth knowing (carried over, verified still true)

### Sprite cell facts in `assets/Enhancers.png` (7 columns × 5 rows)

| Cell | What's there |
| ---- | --- |
| (0, 0) | **Red deck-back** (NOT a blank base, NOT the Ace) |
| (1, 0) | **Plain blank card body.** The "card base" layer for unenhanced StandardCards. |
| (2, 0) | Gold seal art |
| Rows 2–3 | The rest of the deck-backs (Blue, Yellow, Green, Black, Magic, Nebula, Ghost, Abandoned, Checkered, Erratic, Painted, Anaglyph, Plasma, Zodiac, Challenge). Full map in `src/components/DeckSprite.tsx::DECK_SPRITE_POS`. |
| Row 1 | The 8 enhancements. See `src/sprites/spriteData.ts::ENHANCER_MAP`. |
| (4–6, 4) | Purple / Red / Blue seals. |

The standard playing-card faces are in `assets/8BitDeck.png` (13×4 = 52 face
cells; no deck-backs there).

### motely-wasm boot is INLINED, not wrapped

There is no `ensureMotelyReady`, no `MotelyProvider`, no `useMotelyRuntime`.
The pattern is a top-level `await bootsharp.boot()` in the consumer entry
point (see `examples/mcp-seed-finder/src/main.tsx:14`). Don't reintroduce a
wrapper — it's been tried and reverted before.

## Known real gaps (from a 2026-07-06 audit)

- **No in-app JAML authoring help.** Grepping `src/json-render` and
  `examples/mcp-seed-finder` for authoring/tips/help/docs turns up one
  decorative "Help" button label in a mascot story with nothing behind it.
  If "make JAML authoring more helpful" is the ask, this is the actual empty
  surface to build into — there's no prior half-built version to restore.
- **Two independent vocab implementations.** `packages/jaml-codemirror/src/vocab.ts`
  and `packages/jaml-lsp/src/vocab.ts` each independently call
  `MotelyJaml.listItems(kind, "")` instead of sharing one source. They've
  already drifted once — both needed the same "`Any` wildcard never
  autocompleted" fix, applied separately, in commit `4e88c2f`. Next drift
  won't announce itself; consider a shared vocab module both packages import.
- **`MotelyJamlyzer.analyzeSeeds` has a real perf cliff, not fixed here.**
  Computes every ante any clause references, synchronously. Ante 8 ≈ 3ms;
  ante 39 hangs the tab past 30s. `analyzeSeedsPaged`/`resumeSeeds` hit the
  same wall. The real fix belongs in Motely's C# analyzer, not this repo.
- **`JamlyzerView`** (exported from jaml-ui's main index) caps `maxAnte=8`
  and never generates an ante-0 button. seedfinder.app's hand-rolled X-ray
  view does ante-0 correctly and is used instead of this component there —
  worth fixing if `JamlyzerView` gets reused elsewhere.

## Fixed 2026-07-06

- 5 lint errors across `packages/jaml-codemirror`, `packages/jaml-lsp`, and
  a stray `@storybook/react` import in `src/json-render/stories/jamlyzer.stories.tsx`
  (should've been `@storybook/react-vite`, matching every other story).
- `pnpm typecheck` only ever checked `src/`. Added a `typecheck` script to
  each of `packages/jaml-codemirror`, `packages/jaml-lang`, `packages/jaml-lsp`,
  plus a root `pnpm typecheck:all` that runs all of them via `pnpm -r`.

## Last word

Treat `CLAUDE.md` as ground truth for current architecture, not this file's
history section. Read before you write. The design-system rewrite already
happened — don't relitigate it, build on top of what's here now.
