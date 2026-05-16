# Bootsharp reference

Live `@` references into the local Bootsharp source/docs. **Do not copy** — we build alpha locally and these move.

## What we compile

Motely.Wasm builds with `RuntimeIdentifier=browser-wasm` and the Bootsharp toolchain emits a **NativeAOT-LLVM** WebAssembly module plus generated JS bindings. The output is a plain ES module — it runs anywhere with WASM + an ES-module loader: **browsers, Node, Bun, Deno, Cloudflare/Vercel/Netlify edge workers**. No Emscripten glue, no DOM assumptions. See `@D:\bootsharp\docs\guide\llvm.md` for the toolchain story.

## Relevant samples

- `@D:\bootsharp\samples\minimal\README.md` — smallest end-to-end: a `cs/` project, `index.html`, and `main.mjs`. Mirrors how `Motely.Wasm/test-sanity.mjs` and `test-browser.html` boot the package.
- `@D:\bootsharp\samples\react\README.md` — full app shape: Vite + React frontend, `backend/` C# project, `test/` harness. Reference when wiring Bootsharp into a real UI (closest analog to BalatroSeedOracle / jaml-ui consumers).

## Agent docs (D:\extra\bootsharp)
@D:\extra\bootsharp\AGENTS.md
@D:\extra\bootsharp\CLAUDE.md

## Guide (D:\bootsharp\docs)
@D:\bootsharp\docs\index.md
@D:\bootsharp\docs\guide\index.md
@D:\bootsharp\docs\guide\getting-started.md
@D:\bootsharp\docs\guide\build-config.md
@D:\bootsharp\docs\guide\declarations.md
@D:\bootsharp\docs\guide\events.md
@D:\bootsharp\docs\guide\interop-instances.md
@D:\bootsharp\docs\guide\interop-modules.md
@D:\bootsharp\docs\guide\llvm.md
@D:\bootsharp\docs\guide\namespaces.md
@D:\bootsharp\docs\guide\nullability.md
@D:\bootsharp\docs\guide\preferences.md
@D:\bootsharp\docs\guide\serialization.md
@D:\bootsharp\docs\guide\extensions\dependency-injection.md
@D:\bootsharp\docs\guide\extensions\file-system.md
