# AGENTS.md

Guidance for AI agents working in this repo. Read once, then act.

## Project

Motely — C# seed-finder for Balatro. `net10.0`. Multi-project solution:

- `Motely/` — core engine, JAML compiler, SIMD-vectorized search.
- `Motely.CLI/` — NativeAOT CLI (also writes JAML schema).
- `Motely.Wasm/` — Bootsharp / NativeAOT-LLVM browser build; publishes `motely-wasm` npm package. Single-threaded (`WasmEnableThreads=false`).
- `Motely.TUI/` — Terminal.Gui desktop UI.
- `Motely.Tests/` — xunit suite (420 tests).
- `Motely.DistributedWorker/` + `Motely.HelperAPI/` — pool worker + REST API.

JAML (Jimbo's Ante Markup Language) is the user-facing search config. Schema is generated, not hand-edited:

```
dotnet run --project Motely.CLI -- --write-jaml-schema
```

`Motely/Filters/Jaml/JamlConfig.cs` is authoritative. Downstream consumers (MCP server in `seedfinder.app`, `jaml-ui`, etc.) re-sync from the generated schema.

## Build & test

```
dotnet build
dotnet test
```

Currently 420/420 green. Don't ship a red.

## Behavior rules

1. **Read current code, not git history.** Log/blame is reference at best, lies at worst. Past AI work is in there. Don't anchor on it.
2. **Edits need consent.** Confirm before modifying files. Not kid gloves — not your machine.
3. **No softening.** Drop "let me check," "is this okay," "want me to," "I noticed maybe…" Confident asks or none.
4. **Don't narrate every step.** Brief updates at decision points or blockers. Otherwise work.
5. **Self-direct on small calls.** Pick a reasonable default. The user redirects if needed. Don't pelt with clarifying questions.
6. **No assumptions about the user.** Identity, condition, mental state — not your call to bring up.
7. **No `--no-verify`, no skipping hooks, no force-push to main.**

## Gotchas

- `Motely.Wasm` is single-threaded. Don't introduce threading there.
- YAML deserialization uses `Vecc.YamlDotNet.Analyzers.StaticGenerator` — no reflection-heavy patterns.
- `TrimmerRoots.xml` keeps reflection-reachable types alive for NativeAOT/trimmed builds.
- Version lives once in `Directory.Packages.props` as `<MotelyVersion>` — stamped into every assembly and `motely-wasm/package.json` at publish.
