# motely-wasm (NativeAOT-LLVM + Bootsharp)

This project compiles Motely to **browser WASM** with **Bootsharp** interop. Prefer **`createPlan` → chain on `settings` (same as `JamlSearchBuilder` / `IMotelySearchSettings` in C#) → `runSearch`**; the `start*` helpers are optional shortcuts. Progress/results go through **`SearchEvents`**.

The **`Program`** class here is only the **runtime bootstrap** (`Main` → `RunBootsharp()`). Do not confuse it with `Motely.CLI`.

**Bootsharp glue** (`JSExport` / `JSImport` / `JSPreferences`) is isolated in **`BootsharpInterop.cs`** so you can ignore it while working in C#. **`MotelyJamlSearchBuilder`** (search), **`MotelySingleSearchContext`** (seed inspection), and **`SearchEvents`** (progress/results) are the real API surface.

After `dotnet publish` on this project, the npm package is emitted under `motely-wasm/` (and `motely-wasm-compat/` is built by the csproj target).

**Breaking change (v7.0.4):** the old `MotelyWasmHost` namespace is gone. Use `MotelyJamlSearchBuilder` (builder pattern: `loadJaml` → `random`/`sequential`/etc → `run`), `MotelySingleSearchContext.open(seed, deck, stake)`, and `SearchEvents`.

**Sequential batch size:** `batchCharCount` applies only to **sequential** search (`startSequentialSearch*`, and `startConfiguredSearch` when the JAML has **no** `aesthetics`). Keyword/random/aesthetic/seed-list modes are **provider** searches and do **not** take `batchCharCount` (the engine uses fixed vector-width batches).

**Threads:** the browser host runs search with **one** worker (`threadCount` is not a parameter). Use the CLI/TUI for configurable parallelism.

**Seed list:** `startSeedListSearch` takes **`string[]` seeds** (trimmed, empties dropped), not a CSV string.

**Cancel:** `runSearch` / each `start*` returns **`IMotelySearch`** — call **`cancel()`** on that object. There is no host-level “current search” slot.
