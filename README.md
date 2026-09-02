# Motely

CPU Balatro seed search: AVX-512 lanes (8 seeds), JAML filters, CLI + WASM + LSP.

| Piece | Role |
|-------|------|
| `Motely` | Engine (SIMD search, JAML loader, FilterDescs) |
| `Motely.CLI` | Search entry; seed lake = DuckLake with catalog `ducklake.sqlite` at the root, data under `Seeds/` |
| `Motely.Lsp` | stdio JSON-RPC; answers from engine only |
| `vscode-jaml` | languageclient host only |
| `Motely.Wasm` / `motely-wasm` | Bootsharp hosts Search + Analyze. Motely.dll has zero Bootsharp. Publish `-c Release` (LLVM, not Mono). |

## Quick start

```sh
dotnet build
dotnet run --project Motely.CLI -- --jaml JamlFilters/AlwaysPass.jaml --collect 1
```

## Run everything (Aspire)

`aspire run` (or `dotnet run --project Motely.AppHost`) starts the long-running pieces behind one dashboard (logs, traces, metrics); nothing needs Docker:

| resource | what | where |
|---|---|---|
| `helper-api` | `Motely.HelperAPI` — `/health`, `/api/validate`, `/api/search` (Launchpad) | http://localhost:3141 |
| `distributed-worker` | `MotelyWorker` in Search Party mode against seedfinder.app — set the `party-id` parameter, then start it | explicit start |
| `jaml-ui` | Storybook of the sibling `../jaml-ui` checkout, only when it exists | http://localhost:6006 |

Parameters (dashboard → Parameters, or `Parameters:<name>` in `Motely.AppHost/appsettings.Development.json`): `seedfinder-pool-url` (blank by default, so helper-api's in-process pool worker stays idle), `party-id`, `party-server`. Every resource gets the repo's own `JamlFilters/` and `Seeds/` (the seed lake — DuckLake, SQLite catalog `ducklake.sqlite` at the repo root, Parquet under `Seeds/`) rather than its project directory's. `Motely.Tests/AppHostModelTests.cs` pins this composition without starting it.

Filters are JAML text → `JamlConfig`. Clause families live on FilterDescs; `JamlSchema` is the generated index. Docs for WASM: `Motely.Wasm/README.md`.

Which seeds get searched (sequential, `--source`, `--keyword`, `--random`, `--aesthetic`, `--collect`, `--drown` the whole seed lake, `--replay` a JAML's own seeds) and which entry points expose each: `docs/SEED-INPUT-MODES.md`.

Commission support: [@OptimusPi](https://github.com/OptimusPi/).
