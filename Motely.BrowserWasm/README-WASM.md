# motely-wasm (NativeAOT-LLVM + Bootsharp)

This project compiles Motely to **browser WASM** with **Bootsharp** interop. **`MotelyWasmHost`** is the high-level API (`loadJaml`, `compileJummy`, `startRandomSearch`, etc.). **`MotelyJamlSearchBuilder`** is the internal fluent builder used by `MotelyWasmHost` under the hood. Progress/results go through **`SearchEvents`**.

The **`Program`** class here is only the **runtime bootstrap** (`Main` → `RunBootsharp()`). Do not confuse it with `Motely.CLI`.

**Bootsharp glue** (`JSExport` / `JSImport` / `JSPreferences`) is isolated in **`BootsharpInterop.cs`** so you can ignore it while working in C#. **`MotelyWasmHost`** (high-level API), **`MotelySingleSearchContext`** (seed inspection), and **`SearchEvents`** (progress/results) are the public API surface. **`MotelyJamlSearchBuilder`** is the internal builder that `MotelyWasmHost` delegates to.

After `dotnet publish` on this project, the npm package is emitted under `motely-wasm/` (and `motely-wasm-compat/` is built by the csproj target). **Monaco** is not part of these packages — use `tools/jaml-language/monaco` (`@motely/jaml-monaco`) for editor assets.

**API note:** `MotelyWasmHost` is the primary API for consumers. It provides one-call methods like `startRandomSearchFromJaml(jaml, count)` that handle everything internally. `MotelyJamlSearchBuilder` is the underlying builder — use it directly only if you need fine-grained control over the search pipeline.

**Sequential batch size:** `batchCharCount` applies only to **sequential** search (`startSequentialSearch*`, and `startConfiguredSearch` when the JAML has **no** `aesthetics`). Keyword/random/aesthetic/seed-list modes are **provider** searches and do **not** take `batchCharCount` (the engine uses fixed vector-width batches).

**Threads:** the browser host runs search with **one** worker (`threadCount` is not a parameter). Use the CLI/TUI for configurable parallelism.

**Seed list:** `startSeedListSearch` takes **`string[]` seeds** (trimmed, empties dropped), not a CSV string.

**Cancel:** `runSearch` / each `start*` returns **`IMotelySearch`** — call **`cancel()`** on that object. There is no host-level “current search” slot.
