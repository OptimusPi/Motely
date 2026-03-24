# RALPH TASK: Build motely-wasm seed search HTML

## Task
Create a single HTML file (`wwwroot/searcher.html`) that:
1. Boots motely-wasm from `/motely-framework/index.mjs`
2. Has a textarea for JAML code input (like Program.cs — fluid, dark, monospace)
3. Has a Search button
4. Has a single-column table that fills with found seeds as they come in
5. Shows progress (seeds searched, found, elapsed time)
6. Click a seed to copy it

## Rules
- Use ONLY the published npm package API (what `import { boot } from 'motely-wasm'` exposes)
- Do NOT look at Orchestration source code, node_modules internals, or generated .d.ts
- The Bootsharp convention: `import { boot, MotelyWasm } from './dist/index.mjs'`
- Events: `MotelyWasm.onProgress`, `MotelyWasm.onResult`, `MotelyWasm.onComplete`
- Functions: `MotelyWasm.validateJaml(jaml)`, `MotelyWasm.runSearch(jaml, threads, batchChars, start, end)`
- `MotelyWasm.getVersion()` for version display
- Events use `.subscribe(handler)` / `.unsubscribe(handler)` pattern

## Completion criteria
- File exists at `wwwroot/searcher.html`
- Boots WASM with secure context check
- JAML validation before search
- Live seed results streaming into table
- Progress bar/text
- Clean, compact, dark UI — no bloat
- Works when served by the .NET API at `/searcher.html`

## Iteration check
After each iteration, read the file back and verify it matches all criteria above.
