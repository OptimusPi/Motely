# motely-wasm (NativeAOT-LLVM + Bootsharp)

This project compiles Motely to **browser WASM** with **Bootsharp** interop. Two complementary APIs are exported, both first-class:

- **`MotelyWasmHost`** — one-call host. `loadJaml` / `compileJummy` parse JAML; `startRandomSearchFromJaml(jaml, count)`, `startConfiguredSearchFromJaml(...)`, `startSeedListSearchFromJaml(...)` start a search in a single call. The host owns `_currentSearch`; `stopSearch()` cancels it.
- **`MotelyJamlSearchBuilder`** — fluent multi-step builder. Chain `loadJaml(jaml) → random(n) | sequential(...) | seedList(...) | aesthetic(a) | keywords(...) → run()` when you need fine-grained control over the search pipeline.

Both call into the same engine. Pick whichever shape matches your call site; neither is deprecated.

Progress and results go through **`SearchEvents`** (`onProgress`, `onResult`, `onComplete` — JS subscribes; C# notifies). Single-seed inspection lives on **`MotelyWasmHost`** (`singleGetBossForAnte`, `singleGetAnteFirstVoucher`, etc.) — these take `seed/deck/stake` on every call so JS does not have to hold a stateful per-instance handle.

The **`Program`** class here is only the **runtime bootstrap** (`Main` → `RunBootsharp()`). Do not confuse it with `Motely.CLI`.

**Bootsharp glue** (`JSExport` / `JSImport` / `JSPreferences`) is isolated in **`BootsharpInterop.cs`** so you can ignore it while working in C#. The public API surface is **`MotelyWasmHost`**, **`MotelyJamlSearchBuilder`**, **`MotelySingleSearchContext`** (seed inspection), and **`SearchEvents`** (progress/results).

After `dotnet publish` on this project, the npm package is emitted under `motely-wasm/` (and `motely-wasm-compat/` is built by the csproj target). **Monaco** is not part of these packages — use `tools/jaml-language/monaco` (`@motely/jaml-monaco`) for editor assets.

**Bootsharp interop rule (9.0.0+):** a `[JSExport]` interface method on `MotelyWasmHost` must NOT call another `[JSExport]` interface method on `this`. Mono WASM rejects the resulting managed→`[UnmanagedCallersOnly]` dispatch with `Fatal error. Invalid Program: attempted to call a UnmanagedCallersOnly method from managed code.` Inline shared logic into a `private static` helper (see `LoadJamlCore`) and call the helper from each public method.

**Sequential batch size:** `batchCharCount` applies only to **sequential** search (`startSequentialSearch*`, and `startConfiguredSearch` when the JAML has **no** `aesthetics`). Keyword/random/aesthetic/seed-list modes are **provider** searches and do **not** take `batchCharCount` (the engine uses fixed vector-width batches).

**Threads:** the browser host runs search with **one** worker (`threadCount` is not a parameter). Use the CLI/TUI for configurable parallelism.

**Seed list:** `startSeedListSearch` takes **`string[]` seeds** (trimmed, empties dropped), not a CSV string.

**Cancel:** `runSearch` / each `start*` returns **`IMotelySearch`** — call **`cancel()`** on that object. There is no host-level “current search” slot.
