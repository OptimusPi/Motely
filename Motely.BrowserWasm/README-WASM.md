# motely-wasm (NativeAOT-LLVM + Bootsharp)

This project compiles Motely to **browser WASM** with **Bootsharp** interop. The embeddable API is **`MotelyWasmHost`** (not a CLI): JavaScript imports it, calls `getVersion()`, `loadJaml`, `startSequentialSearch`, `startSequentialSearchBySearchIndex`, etc., and receives progress/results via **`SearchEvents`**.

The **`Program`** class here is only the **runtime bootstrap** (`Main` → `RunBootsharp()`). Do not confuse it with `Motely.CLI`.

After `dotnet publish` on this project, the npm package is emitted under `motely-wasm/` (and `motely-wasm-compat/` is built by the csproj target).

**Breaking change (vs older builds):** the exported host type was renamed from `MotelyProgram` to `MotelyWasmHost`.

**Sequential batch size:** `batchCharCount` applies only to **sequential** search (`startSequentialSearch*`, and `startConfiguredSearch` when the JAML has **no** `aesthetics`). Keyword/random/aesthetic/seed-list modes are **provider** searches and do **not** take `batchCharCount` (the engine uses fixed vector-width batches).
