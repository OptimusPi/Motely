# Motely (engine)

SIMD + scalar Balatro seed search. JAML loads into typed `JamlConfig`; filters are FilterDescs.

| Concern | Where |
|---------|--------|
| Search / SIMD | `MotelySearch`, vector contexts |
| JAML grammar | `Filters/Jaml/` — FilterDesc owns wire; `JamlSchema` indexes |
| Seed providers | `SeedProviders/` (list, random, sequential, aesthetics) |
| PRNG streams | keyed streams; order within a key is law |

## Commands (from repo root)

```sh
dotnet build
dotnet test
dotnet run --project Motely.CLI -- --jaml <file>
dotnet run --project Motely.CLI -- --jaml <file> --collect 1
```

CLI, WASM, LSP, and vscode-jaml depend **inward** on this project. One grammar — no second authoring table in editors.
