# Motely

CPU Balatro seed search: AVX-512 lanes (8 seeds), JAML filters, CLI + WASM + LSP.

| Piece | Role |
|-------|------|
| `Motely` | Engine (SIMD search, JAML loader, FilterDescs) |
| `Motely.CLI` | Search entry; seed lake under `Seeds/` |
| `Motely.Lsp` | stdio JSON-RPC; answers from engine only |
| `vscode-jaml` | languageclient host only |
| `Motely.Wasm` / `motely-wasm` | same search surface for JS |

## Quick start

```sh
dotnet build
dotnet run --project Motely.CLI -- --jaml JamlFilters/AlwaysPass.jaml --collect 1
```

Filters are JAML text → `JamlConfig`. Clause families live on FilterDescs; `JamlSchema` is the generated index. Docs for WASM: `Motely.Wasm/README.md`.

Which seeds get searched (sequential, `--source`, `--keyword`, `--random`, `--aesthetic`, `--collect`, `--drown` the whole seed lake, `--replay` a JAML's own seeds) and which entry points expose each: `docs/SEED-INPUT-MODES.md`.

Commission support: [@OptimusPi](https://github.com/OptimusPi/).
