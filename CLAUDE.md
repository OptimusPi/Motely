# CLAUDE.md

**Links only. Do not inline these files.** Read the one you need, when you need it.
Pasting their contents into this file is what blew up context and wasted the weekend.

## Open first

| Doc | When |
|-----|------|
| **[HARDOFF-MATRIX.md](HARDOFF-MATRIX.md)** | **every session** — one board: law, party language, open tickets, daily CLI door. Wins when matrices fight. |
| [JAML.md](JAML.md) | how to write a filter (human guide) |

## Bootsharp documentation — `D:\bootsharp\docs\guide\`

| # | Doc | Read it when |
|---|-----|--------------|
| 1 | [getting-started.md](D:\bootsharp\docs\guide\getting-started.md) | wiring Bootsharp into a project the first time |
| 2 | [index.md](D:\bootsharp\docs\guide\index.md) | orientation / what Bootsharp is |
| 3 | [declarations.md](D:\bootsharp\docs\guide\declarations.md) | **the head.** `.g.d.mts` TS declarations generated per C# namespace from `[Export]`, incl. XML docs, overloads, nullability |
| 4 | [serialization.md](D:\bootsharp\docs\guide\serialization.md) | **enums cross as numbers + name maps; `Dictionary`→ES6 `Map`; records auto-serialized, no `[MarshalAs]`** |
| 5 | [specialization.md](D:\bootsharp\docs\guide\specialization.md) | before changing interop shapes — cited by HARDOFF / A4 as load-bearing |
| 6 | [build-config.md](D:\bootsharp\docs\guide\build-config.md) | before touching `Motely.Wasm.csproj` |
| 7 | [llvm.md](D:\bootsharp\docs\guide\llvm.md) | NativeAOT-LLVM — cited by JamlCostModelSimdExtensions.cs:18 |
| 8 | [interop-modules.md](D:\bootsharp\docs\guide\interop-modules.md) | module layout / namespace→path mapping |
| 9 | [interop-instances.md](D:\bootsharp\docs\guide\interop-instances.md) | mutable types passed by reference |
| 10 | [renaming.md](D:\bootsharp\docs\guide\renaming.md) | controlling emitted JS node/module names |
| 11 | [sideloading.md](D:\bootsharp\docs\guide\sideloading.md) | shipping the bundle |
| 12 | [extensions/dependency-injection.md](D:\bootsharp\docs\guide\extensions\dependency-injection.md) | `Bootsharp.Inject` |
| 13 | [extensions/file-system.md](D:\bootsharp\docs\guide\extensions\file-system.md) | `Bootsharp.FileSystem` (sponsor feed, see nuget.config) |

Root: [D:\bootsharp\README.md](D:\bootsharp\README.md) · [D:\bootsharp\docs\index.md](D:\bootsharp\docs\index.md)
Samples: `D:\bootsharp\samples\` — `minimal`, `react`, `trimming`, `vscode`, `bench`

## Archives (history — not the open queue)

| Doc | Was |
|-----|-----|
| [CLAUDE-CAGE.md](CLAUDE-CAGE.md) | session cage → folded into HARDOFF §0–3 |
| [GROK-WORK-MATRIX.md](GROK-WORK-MATRIX.md) | G01–G36 detail |
| [WORK-ANY-MATRIX.md](WORK-ANY-MATRIX.md) | empty-list law writeup |
| [CLAUDE-BITES-MATRIX.md](CLAUDE-BITES-MATRIX.md) | E/U/H/X bites |
| [HANDOFF-CLAUDE.md](HANDOFF-CLAUDE.md) | S8 climb + long A4 |
| [WASM-WORK-MATRIX.md](WASM-WORK-MATRIX.md) | WASM waves |
| [HANDOFF-WASM-NOT-REAL.md](HANDOFF-WASM-NOT-REAL.md) | WASM audit essay |

## Operator

- CAPS is emphasis, not distress. Typos are speed. Do not shift register.
- The FilterDesc is the source of truth. Never hand-type a list the engine already knows.
- Say what was checked and what was not. Do not state conclusions the evidence does not reach.
- Product chrome: **Search Party · Find seeds · KEEP · NEXT · Seed view** — see HARDOFF §2.
