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

Commission support: [@OptimusPi](https://github.com/OptimusPi/).
