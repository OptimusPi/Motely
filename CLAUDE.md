# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Package manager is `pnpm` (lockfile is `pnpm-lock.yaml`).

- `pnpm build` — Vite library build, emits `dist/` with five entry bundles + `dist/ui/jimbo.css` + `.d.ts` via `vite-plugin-dts`.
- `pnpm dev` — `vite build --watch` (this is a library, not an app — there is no dev server for the library itself).
- `pnpm typecheck` — `tsc --noEmit --pretty false`.
- `pnpm lint` — ESLint over the repo. Custom design-rule plugin lives in `eslint-rules/jaml-design.js`. Global rules (`no-raw-button`, `no-emoji-jsx`, `no-uppercase-text`, `no-bold-style`) apply everywhere. `src/components/**` gets three additional rules: `no-inline-style`, `no-token-in-jsx-style`, `no-inline-component` (excludes stories and the four sprite-sheet renderers). `src/ui/**` turns off `no-raw-button` since those ARE the primitives.
- `pnpm storybook` — Storybook dev server on `:3141`. Stories are the primary visual dev surface.
- `pnpm build-storybook` / `pnpm serve:storybook` — build static Storybook, then serve on `:3141` with CORS (used by MCP/iframe consumers).
- Tests run via `vitest` driven by `@storybook/addon-vitest`: stories double as tests, executed in headless Chromium through `@vitest/browser-playwright` (see `vitest.config.ts`). To run a single story-as-test, use `pnpm vitest run -t "<Story Title>"` or filter by file path. There are no separate `*.test.*` files.
- `examples/mcp-seed-finder/` is the canonical end-to-end consumer (MCP App; boots motely-wasm, renders `SeedFinderApp`, runs real searches): `cd examples/mcp-seed-finder && pnpm install && pnpm dev`. The older `examples/seed-finder/` was removed in `ff64157` — CLAUDE.md used to point there. A `pnpm demo` script exists in `package.json` but the `demo/` directory has not been created; running it will fail.

## Architecture

This is a multi-entry React component library with five subpath exports, all bundled by Vite in library mode (`vite.config.ts`):

| Entry | Source | Purpose |
| ----- | ------ | ------- |
| `jaml-ui` | `src/index.ts` | Game card components, JAML IDE, motely-bound hooks. Side-effect-imports `jimbo.css`. |
| `jaml-ui/ui` | `src/ui.ts` | Jimbo design system primitives (panels, buttons, modals, tokens). Side-effect-imports `jimbo.css`. |
| `jaml-ui/core` | `src/core.ts` | Pure helpers — sprite metadata, asset URL resolution, canvas `Layer`. **No React, no motely-wasm.** Safe for Next.js server components. |
| `jaml-ui/motely` | `src/motely.ts` | Re-exports `bootsharp`/`Motely` from `motely-wasm` plus item-decode helpers and the `useJamlLibrary` hook. |
| `jaml-ui/r3f` | `src/r3f.ts` | 3D card via React Three Fiber. r3f stack is an optional peer. |

Every entry point is a barrel — the public API is exactly what these five files re-export. Add a new public component by exporting from the relevant barrel; if it isn't re-exported there, it isn't part of the public surface.

All hooks and components carry `"use client"` at the top of the file for Next.js RSC compatibility. Add it to any new hook or component.

### `src/lib/` — pure utilities

`src/lib/` is a pure-utility layer with no React and no motely-wasm: JAML parsing (`jamlParser.ts`, `jamlSchema.ts`, `jamlCompletion.ts`), card/TTS display helpers, constants, and shared types. Anything that must be safe for server components or workers lives here. The `src/hooks/` layer builds on top of it to expose React-specific state.

### Externalized peers

`vite.config.ts` externalizes `react`, `react-dom`, `three`, `@react-three/*`, `react-icons`, `motely-wasm`, and `@rewaffle/bootsharp-file-system`. Consumers are expected to resolve these. Storybook (`.storybook/main.ts`) strips the `dts` plugin and forces `motely-wasm` to bundle so stories work; it also serves `node_modules/motely-wasm/bin` at `/motely-wasm/bin/`.

### Asset bundling

Vite bundles the sprite PNGs and other static assets via the imports in `src/assets.ts` — every `JAML_ASSET_FILES` entry is a real `import x from "../assets/x.png"`, and `resolveJamlAssetUrl()` returns the bundled URL. Consumers do nothing. There is no base URL to wire up.

### Search hooks

`useSearch` (`src/hooks/useSearch.ts`) runs the WASM search on the main thread — suitable for low-volume runs. `useSearchPool` (`src/hooks/useSearchPool.ts`) shards work across Web Workers (up to `navigator.hardwareConcurrency`, capped at 8) for throughput-intensive searches. Key constraint: **aesthetic mode always forces a single worker** in `useSearchPool` because the aesthetic enumerator is shared state inside one WASM runtime; multiple workers would restart and produce duplicates.

Both hooks call `ensureMotelyReady()` (from `src/lib/motely/runtime.ts`, also exported from `jaml-ui/motely`) before any WASM call. Workers load via Vite's `?worker` lazy import and receive serialised `PoolStartMessage` objects.

### motely-wasm runtime contract

`motely-wasm` is Bootsharp-generated and must be booted once before any `Motely.*` call. The canonical pattern — copied from the bootsharp react sample and `motely-wasm/README.md` — is top-level await in the consumer's entry point:

```ts
import bootsharp, { Motely } from "motely-wasm";
import { createRoot } from "react-dom/client";

await bootsharp.boot("/motely-wasm/bin");
createRoot(document.getElementById("root")!).render(<App />);
```

By the time any component mounts, the runtime is up. Consumers are responsible for making `bin/` reachable at that URL (Storybook does this via `staticDirs`; consuming apps must do the equivalent).

Hooks like `useSearch`, `useAnalyzer`, and `useJamlLibrary` also inline a Standby-guard internally (see `src/hooks/useSearch.ts`: `bootsharp.getStatus() === bootsharp.BootStatus.Standby` → boot) so they work whether or not the consumer did the top-level await. **Don't add JS wrappers around motely-wasm** — import and call it directly. There is no `MotelyProvider` / `useMotelyRuntime` indirection layer; do not reintroduce one.

### Updating motely-wasm

`motely-wasm` is a published npm package. Bump it with `pnpm update motely-wasm` (respects the `^` range) or raise the range and `pnpm install`.

Bootsharp codegen sometimes relocates generated exports across subpaths. If `pnpm typecheck` reports "no exported member" after a bump, the symbol moved — locate it under `node_modules/motely-wasm/dist/generated/` and update the import. Example (18.2.x): `MotelyDeck`/`MotelyStake` → `motely-wasm/motely/enums`, `JamlAesthetic` → `motely-wasm/motely/filters/jaml`.

### CSS / styling

`dist/ui/jimbo.css` is the design-system stylesheet, emitted by Vite as a single asset (`cssCodeSplit: false`, custom `assetFileNames`). `src/index.ts` and `src/ui.ts` import it as a side effect, so any consumer importing from `jaml-ui` or `jaml-ui/ui` automatically gets the CSS. `sideEffects` in `package.json` is configured to preserve this through tree-shaking.

For DOM components, always use CSS custom properties (`--j-red`, `--j-darkest`, etc.) defined in `jimbo.css`. Use the JS constants in `src/ui/tokens.ts` (`JimboColorOption`) only in contexts that cannot use CSS — R3F/Three.js, canvas drawing, inline SVG fills, or imperative animation APIs. Do not use the JS constants in JSX styles.

### Fonts

Two font tokens, both defined in `jimbo.css` (`:root`):

- **`--j-font: 'm6x11plus', 'm6x11', monospace`** — the UI font for everything player-facing (the Balatro pixel font). All `.j-text--*` size classes and every `Jimbo*` component render in this. This is the default; use it for all UI text.
- **`--j-font-code: 'JetBrains Mono', 'Roboto Mono', monospace`** — the coding font, used only by `JamlCodeEditor` and `.j-code-block` for JAML source. The fallback chain is OS-native so code still reads as code if the Google Fonts stylesheet fails to load.

Never hardcode a `font-family`; reference one of these two tokens. `m6x11`/`m6x11plus` are bitmap pixel fonts — keep `line-height` ≥ ~1.1 (never `1`), or ascenders/descenders clip.

## Design rules

Hard constraints for any UI work in this repo:

- Never use ALL CAPS.
- Never use bold / heavy font-weight.
- Never put grey text on a grey background. `tone="grey"` text on any `--j-darkest` / `--j-dark-grey` / `--j-teal-grey` / `--j-surface-inset` surface is grey-on-grey.
- **Every component is a `Jimbo*` component.** No raw `<button>`, no inline anonymous components in consumer screens. Missing primitive? Add a `Jimbo*` to `src/ui/` with a story.
- **No emoji as icons.** Use `react-icons` (`react-icons/fi` preferred).
- **Item names go in `JimboTooltip`, not inline labels.** Sprite + tooltip-on-hover. Players recognize the art; the 320px surface can't afford permanent inline labels.
- Canonical surface is **320×568, HARD LOCKED — the MCP Apps inline-iframe target.** The lock isn't iPhone-SE nostalgia: 320×568 is the canvas size MCP Apps gives an embedded View inside Claude (and the same shape suits any tight inline iframe widget). With a fixed canvas you *compose* the surface — grid tracks, named regions — instead of *guess-and-check* flexbox flow. Layout primitives (`JimboStack`, `JimboRow`) are grid-based for this reason. The `.j-app` shell is fixed at 320×568 — no scroll, no stretch, no reflow. We design for 320×568 first; we widen only after the 320 experience is right. If your component doesn't fit, redesign the component, don't relax the lock.
- "Juice" comes from CSS animations (`.j-font-dance-char`, `scale(1.05) translateY(-2px)`, etc.) — not JS wrappers.
- No visible scrollbars. Use magnetic scroll snapping.

## Types come from motely-wasm — no schema drift

**Do not declare game-domain types locally. Import them from `motely-wasm`.** Every Balatro enum (`MotelyDeck`, `MotelyStake`, `MotelyTag`, `MotelyVoucher`, `MotelyBoosterPack`, `MotelyBossBlind`, `MotelyJokerRarity`, `MotelyItemEdition`, `MotelyItemEnhancement`, `MotelyItemSeal`, `MotelyItemType`, `MotelyItemTypeCategory`) already exists at `motely-wasm/motely/enums`. The JAML filter aesthetic enum lives at `motely-wasm/motely/filters/jaml` (`JamlAesthetic`). Analysis result shapes (`MotelyJamlyzerResult`, `MotelySeedAnalysis`, etc.) live at `motely-wasm/motely/analysis`.

If you find yourself writing `type DeckType = 'red' | 'blue' | …` or `type JokerRarity = 'common' | 'uncommon' | …` — stop. The enum already exists; you're forking the schema. Each fork is a drift bomb that fires when motely-wasm adds a value (e.g. a new deck) and your union silently doesn't match. Import the enum and use its members.

**What stays local:** jaml-ui-specific data formats that motely-wasm has no equivalent for — JAMZ archive types (`JamzFile`, `JamzHeader`, `JamzSeedData`, `RelevantEvent`, `EventSource`), `RitualConfig`, and component-prop interfaces. These describe wire formats and UI surfaces unique to this library and are not part of motely-wasm's domain.

**Source of truth for "does this exist in motely-wasm?":** grep `node_modules/motely-wasm/dist/generated/motely/**/*.g.d.mts` before declaring anything that smells like game state. If the symbol moved between minor bumps (see "Updating motely-wasm" above), update the import — don't fork.

## Test integrity

Stories double as tests in this repo. The cheap way to "fix" a failure is to weaken the story instead of the code — **do not do this.** Tracking the broader pattern: anthropics/claude-code#319.

- If a story-as-test fails, the default assumption is the **code** is wrong. Fix the implementation. Only touch the story if the expectation itself was incorrect — and say so in the commit message.
- Never add `.skip`, `.todo`, `xit`, `xdescribe`, or strip a `play` function to make a flake go quiet. If something is flaky, report it; do not silence it.
- Never downgrade a strict matcher (`toBe`, `toEqual`, `toMatchObject`) to a permissive one (`toBeTruthy`, `toBeDefined`, `not.toThrow`) without an explicit reason.
- Never write an assertion that accepts an error message as expected output. A feature that errors is a bug to report, not an output to assert on.
- Never swallow exceptions with empty `catch {}` blocks to make a play function pass.

The PreToolUse hook in `.claude/hooks/check-test-integrity.mjs` blocks these patterns at edit time. The pre-commit guard in `.claude/hooks/guard-tests-precommit.mjs` blocks commits that touch `*.stories.tsx` without a paired source change (activate with `git config core.hooksPath .githooks`). Both can be bypassed (`git commit --no-verify`) when genuinely needed — but the default is enforcement.

## Component placement convention — HARD RULE

**Everything we ship — every consumer site, every production app, every demo — composes from `jaml-ui` and `jaml-ui/ui` only. We do not reinvent primitives in consumer repos.**

This is the rule because the same component has been re-invented 11+ times across consumer repos. Each reinvention diverges from the design system, breaks the 320×568 lock, or skips a Jimbo* primitive. Stop reinventing.

**The composition contract:**

- **Every site, every consumer, every screen is composed from JimboUI primitives only.** Period. Consumers do not write styled `<div>`s, inline `style={{}}`, or anonymous mini-components.
- **A real Jimbo* primitive is a React component with a typed prop API and CSS classes in `jimbo.css` — not a className helper, not a styled `<div>` shortcut.** Composable, story-covered, exported from the barrel.
- **A component that touches JAML / MotelyJAML / motely-wasm goes in `jaml-ui` (src/index.ts).** A pure design-system primitive goes in `jaml-ui/ui` (src/ui.ts). See "Which barrel? — the motely-wasm test" above.
- **Components in `jaml-ui` are themselves composed of `jaml-ui/ui` primitives only.** No raw HTML, no inline styles, no shortcut div wrappers. If you need a new layout or visual primitive while writing a `jaml-ui` component, the answer is: add it to `jaml-ui/ui` first (with a story), then use it.

**Workflow when you think you need a new primitive:**

1. **Search `src/ui/` first.** Glob `src/ui/Jimbo*.tsx` and read the candidates. The primitive almost certainly exists — `JimboButton`, `JimboPanel`, `JimboModal`, `JimboBadge`, `JimboTooltip`, `JimboInfoCard`, `JimboStatGrid`, `JimboInset`, `JimboStack`, `JimboRow`, `JimboInputModal`, `JimboSelect`, `JimboStepper`, `JimboSpinner`, `JimboSlider`, `JimboToggleList`, `JimboTabs`, `JimboCopyRow`, `JimboFlankNav`, `JimboCodeBlock`, `JimboText`, etc. Storybook is the visual index.
2. **If you think it doesn't exist — ASK before inventing.** Describe what's missing, name what you'd call it, and wait. Most "missing" primitives are existing ones used wrong, or two existing primitives composed. Inventing a new Jimbo* primitive is a design decision, not an autonomous coding decision.
3. **If it does exist but doesn't quite fit,** extend the existing primitive (new variant, new prop) rather than copying it. Still worth confirming the extension before writing it.
4. **Only after confirmation, add to `src/ui/`** with a Storybook story and a barrel export. Then use it in the consumer.

**What this rules out in any consumer file:**

- Raw `<button>`, `<input>`, `<select>`, `<textarea>` — use `JimboIconButton`/`JimboButton`, `JimboTextInput`, `JimboSelect`, etc.
- Inline `style={{ ... }}` props — use Jimbo CSS classes (`.j-*`) or a Jimbo primitive.
- Anonymous inline React components defined inside screens.
- Tailwind/CSS-modules/styled-components — this design system uses CSS custom properties in `jimbo.css` only.

**Two sharp edges that have bitten this codebase repeatedly:**

- **`JimboButton` vs `JimboIconButton` — two different intents.** `JimboButton` (`src/ui/panel.tsx`) is a full-width/labelled DOM button that renders `JimboText` inside a `.j-btn` slab — for primary actions and CTA buttons. `JimboIconButton` (`src/ui/JimboIconButton.tsx`) is a compact square icon-only button with inline-style hover state — for row actions, remove-x, toolbar slots. Props differ: `JimboButton` takes `tone` from the Balatro palette (`orange`, `red`, `blue`, etc.) and `fullWidth`; `JimboIconButton` takes `tone="default" | "destructive"`, `size="xs" | "sm" | "md"`, and `onMouseDown`/`onTouchStart` passthrough for drag-stop semantics.
- **`size` prop collision when extending `<input>`.** `React.InputHTMLAttributes<HTMLInputElement>` already declares `size?: number` (native HTML attr). If your new primitive uses `size` for its own scale (`'xs' | 'sm' | 'md'`), you must `Omit<..., 'size'>` or typecheck fails with `Type 'string' is not assignable to type 'number'`. `JimboInlineEdit` does this; `JimboTextInput` doesn't define `size` so doesn't need to.

**Which barrel?** See "Which barrel? — the motely-wasm test" above. Pure design = `src/ui.ts`. Touches motely-wasm = `src/index.ts`.

This rule applies to *this* repo and every consumer repo. When you open a consumer repo (seed-finder, etc.), its CLAUDE.md should restate this rule and link back here.

### Which barrel? — the motely-wasm test

The deciding rule for picking between `src/ui.ts` and `src/index.ts`:

- **`src/ui.ts` (`jaml-ui/ui`) — Jimbo primitives only.** Pure design system: panels, buttons, modals, badges, tooltips, layout. No motely-wasm, no JAML parsing, no search/analyzer state. If a designer could use it on any project, it goes here.
- **`src/index.ts` (`jaml-ui`) — higher-level components that touch motely-wasm.** Anything that reads/writes JAML, runs a wasm search, or drives the analyzer belongs here. These compose Jimbo primitives from `src/ui.ts`.

When in doubt, ask: *does this component import from `motely-wasm` (directly or transitively through a hook like `useSearch` / `useAnalyzer` / `useJamlLibrary`)?* If yes → `src/index.ts`. If no → `src/ui.ts`.
