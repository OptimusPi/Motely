# Motely.Wasm

The Motely engine compiled for the browser. Plain .NET `browser-wasm` with in-box
`[JSExport]` interop — no Bootsharp, no sponsor-feed packages, nothing that can't build
from nuget.org plus the `wasm-tools` workload.

## Build

```sh
dotnet workload install wasm-tools   # once
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release
```

The self-contained app lands in `bin/Release/net10.0/browser-wasm/AppBundle/` — serve that
directory statically and the page is live. `host/main.mjs` boots the runtime and exposes the
API as `globalThis.motely`, firing a `motely-ready` event when it's callable.

## API

Strings in, JSON strings out. Shapes live in `WasmDtos.cs`.

| Export | Returns |
| --- | --- |
| `Version()` | engine informational version |
| `ParseJaml(text)` | `{ok, error?, name, deck, stake, must, should, mustNot}` |
| `Vocabulary()` | every engine enum by name — decks, stakes, jokers, vouchers, tarot/spectral/planet cards, bosses, tags, editions, enhancements, seals |
| `Diagnostics(text)` | `JamlLanguageService.Diagnose` squiggles with spans and codes |
| `Hover(text, line, ch)` | markdown for the word under the cursor, or `null` |
| `Complete(text, line, ch)` | completion candidates with kinds and replace spans |
| `ScoreSeeds(jaml, seeds[])` | *(async)* list-mode search: must clauses gate, should clauses rank; results best-first with per-should tallies |

Parsing, vocabulary and the language brain are the same C# the CLI and LSP use —
`JamlConfigLoader`, the engine enums, `Motely.Lsp.Core` — so the browser can never drift
from the engine's grammar.

## Smoke test

```sh
cd Motely.Wasm/tests && npm install
npx playwright install chromium   # once, if no browser is preinstalled
node smoke.mjs ../bin/Release/net10.0/browser-wasm/AppBundle
```

Chromium resolution order: `CHROMIUM_PATH` env var → `PLAYWRIGHT_BROWSERS_PATH`
(default `/opt/pw-browsers`) → playwright's own browser cache.

Serves the bundle, loads it in headless Chromium, exercises every export, and asserts the
page's self-reported verdict.
