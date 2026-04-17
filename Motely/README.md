# Motely — core engine

Core C# seed-filter engine for **Balatro**. Compiles JAML (Jimbo's Ante Markup Language) filters to SIMD-vectorized search code targeting `net10.0`. Used by:

- **`Motely.CLI`** — NativeAOT native CLI.
- **`Motely.Wasm`** — Bootsharp / NativeAOT-LLVM browser build that publishes the **`motely-wasm`** npm package.
- **`Motely.TUI`** — Terminal.Gui desktop UI.
- **`Motely.DistributedWorker`** / **`Motely.HelperAPI`** — pool worker + REST API.
- **`Motely.Tests`** — xunit regression suite.

## Key types

| Type | Role |
|------|------|
| `MotelyVectorSearchContext` | SIMD vectorized search (8 seeds/cycle, AVX / AVX2 / AVX-512). |
| `MotelySingleSearchContext` | Single-seed analysis (partial classes per domain: bosses, tags, vouchers, shop, packs, cards, events). |
| `MotelyFilters.JamlConfig` + `JamlConfigLoader` | Parsed JAML document; authoritative shape and defaults. |
| `MotelyFilters.JamlSearchBuilder` | Turns a `JamlConfig` into a runnable `IMotelySearchSettings` plan. |
| `MotelyFilters.JamlScoring` | `must` / `should` / `mustNot` evaluation and tally production. |
| `FormatUtils` | Canonical display-name formatting for bosses / vouchers / tags / packs / items. |

## JAML schema

Generated, not hand-written. Regenerate from the CLI:

```powershell
dotnet run --project Motely.CLI -- --write-jaml-schema
```

Writes to `jaml.schema.json` at repo root. Language tooling (`tools/jaml-language/`) in this repo and external consumers (the MCP server in `seedfinder.app`, `jaml-ui`, etc.) consume the generated schema — re-sync downstream whenever `Motely/Filters/Jaml/JamlConfig.cs` changes.

## NativeAOT / trimming

- `TrimmerRoots.xml` keeps reflection-reachable types alive.
- YAML deserialization uses `Vecc.YamlDotNet.Analyzers.StaticGenerator` — do not add reflection-heavy patterns.
- All browser WASM interop is isolated in the `Motely.Wasm` project — see [`../Motely.Wasm/README.md`](../Motely.Wasm/README.md).

## Version

Defined once in repo-root `Directory.Packages.props` as `<MotelyVersion>` and stamped into every assembly by `Directory.Build.props` and into `motely-wasm/package.json` at publish time.
