# CLAUDE.md

Read these first, in this order:

1. **`AGENTS.md`** — project map, hard rules, what NOT to touch (PRNG files, generated artifacts).
2. **`BOOTSHARP.md`** — Bootsharp 0.8 reference compiled from upstream docs. Read before touching `Motely.Wasm/`.
3. **`AUDIT_BOOTSHARP.md`** — current state of the C# ↔ JS contract drift between this repo and `optimuspi/jaml-ui`. Open work tracked there.
4. **`ISSUES.md`** — honest list of known engine/test/doc issues with file:line citations.

## Companion repo

UI lives in **`optimuspi/jaml-ui`**. The Bootsharp boundary is the only place the two repos talk. Fix contract drift on this side first; do not paper over it in the UI.

## Project shape

Engine in `Motely/`, CLI in `Motely.CLI/`, tests in `Motely.Tests/`, browser host in `Motely.Wasm/` (publishes the `motely-wasm` npm package). See `AGENTS.md` for the full project table.

## Hard rules (verbatim from AGENTS.md)

- **No private paths in public files.** No `D:\…`, `X:\…`, local NuGet feeds, or personal drive layouts in `.csproj` / `.props` / `.config`.
- **Warnings are errors.** Fix the cause.
- **Browser-only stays browser-only.** `Bootsharp.FileSystem` lives in `Motely.Wasm`.
- **JAML is JAML.** Not YAML.
- **No facade wrappers.** Export the real Motely public surface from `Motely.Wasm`.
- **PRNG files are fragile.** `MotelySingleSearchContext.*.cs` / `MotelyVectorSearchContext.*.cs` — touch only when explicitly required.
- **Generated artifacts come from the generator.** Do not hand-edit `jaml.schema.json`.

## When in doubt

If you're about to add a method, type, or wrapper to bridge between `Motely.Wasm/Program.cs` and a UI call site — stop and read `AUDIT_BOOTSHARP.md` first. The contract decision is the work; the wrapper is the cleanup.
