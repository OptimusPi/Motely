# HANDOFF — jaml-ui

Written 2026-07-11. State of the "harvest the organs out of `examples/`
into `src/ui`/`src/components`, then delete the examples" effort.

## Standing rules for whoever picks this up

- **No raw HTML when a Jimbo/Jaml component exists.** Enforced by a
  pre-write hook (`.claude/hooks/check-design.mjs`) that blocks raw
  `<button>` etc. — use `JimboButton`, `JimboPanel`, `JimboTextArea` from
  `src/ui/`, or the CSS classes in `src/ui/jimbo.css` directly if no
  component wrapper exists yet for that pattern.
- **`bootsharp.boot()` is one-time and process-wide.** Anything using
  `motely-wasm` must call `bindJimmolateBridge()` (from `jaml-codemirror`)
  *before* `boot()`, once. `setJimmolatePredicate()` can be called any time
  after that to swap the actual predicate. Get this order wrong and search
  silently doesn't run.

## What's done, real, on disk

1. `.storybook/main.ts` — stories glob widened to `src/ui/**` and
   `src/components/**` (was previously only `src/json-render/**`, so any
   story outside that folder was invisible in Storybook).
2. `src/ui/jimbo.css` — `.j-btn__face` now has `color: var(--j-white)`
   directly, instead of relying on inheriting it from `.j-app`.
3. `src/components/SeedFinderApp.tsx` — harvested + Jimbo-ified port of
   `examples/seed-finder`'s app (client-side WASM search). Exported from
   `jaml-ui`'s `src/index.ts`.
4. `src/components/McpSeedFinderApp.tsx` — harvested + Jimbo-ified port of
   `examples/mcp-seed-finder`'s app (server-tool-driven search +
   `JammyMascot`). Exported from `jaml-ui`'s `src/index.ts`.
5. `src/json-render/stories/seed-finder.stories.tsx` — repointed to the
   new `src/components/SeedFinderApp.js` (no longer depends on `examples/`).
6. `src/json-render/stories/mcp-seed-finder.stories.tsx` — new story for
   `McpSeedFinderApp`, calls `bindJimmolateBridge()` before `boot()`.
7. `examples/mcp-seed-finder/src/SeedFinderApp.tsx` — restored from git
   history (it had been deleted from disk) and Jimbo-ified in place, though
   the canonical copy is now `src/components/McpSeedFinderApp.tsx`.

## Not done — pick up here

- **Never visually verified.** No one has run `pnpm run storybook` and
  looked at `ui/Primitives`, `Seed Finder / SeedFinderApp`, or
  `Seed Finder / McpSeedFinderApp` in a browser this session. Do that first.
- **`examples/mcp-seed-finder/src/mcp-app.tsx`** still imports its local
  `./SeedFinderApp` instead of `McpSeedFinderApp` from `jaml-ui` — repoint
  it, then the local `examples/mcp-seed-finder/src/SeedFinderApp.tsx` copy
  can be deleted (it's a duplicate, not a distinct component).
- **`jummy_validate` merged into `jaml_validate`** — JUMMY is JAML, not a
  separate format needing its own tool/conversion step. `jaml_validate` now
  auto-detects single-line (JUMMY clause) vs multi-line (full filter) input
  and calls `MotelyJaml.validateLine`/`validate` accordingly. Done in
  `examples/mcp-seed-finder/server.ts`.
- **MCP server additions still requested but not built**: a Jamlyzer
  `analyze_seed` tool, and a second/simpler search mode, on
  `examples/mcp-seed-finder/server.ts` (currently only has
  `find_balatro_seeds`, `jaml_validate`).
- **`examples/seed-finder-lite` and `examples/mcp-seed-finder`'s
  `server.ts`/`vite.config.ts`/etc.** haven't been audited for anything
  else worth harvesting before the `examples/` directory is deleted for
  good.
- No typecheck or lint has been run against any of this session's changes.
  Run `pnpm typecheck` and `pnpm lint` before trusting it compiles clean.
