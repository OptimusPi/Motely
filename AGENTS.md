# Motely — agent notes

Do not invent Bootsharp. Docs and source live at **`D:\bootsharp`**. Read `docs/guide/serialization.md` and `docs/guide/interop-instances.md` before touching `Motely.Wasm`. Web-searching bootsharp.com wastes a context window.

## Bootsharp marshal (law)

From those docs, not from Motely comments:

- **Records / structs / read-only collections** serialize **by value** (binary). JS sees plain objects / arrays / `Map`.
- **Classes and interfaces** are **interop instances** (by ref). Do not put `JamlConfig` or `IJamlClause` on `[Export]` — they are mutable/interface soup.
- **Tasks** of marshalled values are **Promises**. `Task<ScoreRun>` (record) is `Promise<ScoreRun>`. There is no need for a `takeRun` parking slot. That slot is a Motely invention. Old published `motely-wasm@25.0.3` already returned `Promise<Array<…>>`.
- Native in-memory marshal: numbers, bool, string, arrays/lists of some of those, and tasks of those. Everything else in an interop signature goes through Bootsharp serialization if it has immutable semantics.

## WASM head

- JAML **text** in. `ScoreRun` / `ScoredSeed` **records** out. That is the search API.
- `ParseResult` is a load verdict, not the filter. The filter does not cross; `IJamlClause` cannot.
- LSP exports (`diagnostics`, `hover`, `complete`, tokens) are editor extras. They are not the engine.
- Publish: `dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release` → `Motely.Wasm/bin/motely-wasm`. npm package `Motely.Wasm/package.json` version tracks `MotelyVersion` (`Directory.Build.props`). `files` is only `bin/motely-wasm`.
- Smoke: `Motely.Wasm/tests/smoke.mjs` (serve **repo root**, load `host/index.html`). Flagship: `JamlFilters/Whimsy_Dicetricks.jaml` score `TPZZOLBB` = 245.

## Other

- `JamlFilters/` is the operator’s filter folder, not a test fixture. Tests use `Motely.Tests/GoldenJamlFiles` (folder name leftover).
- Auto cutoff engages per **search batch**: raw matches this batch vs seeds this batch. Not milliseconds. Not a magic 2000/sec.
- One result path: scored. `seed,score` always. No `hasStructuredScores`.
