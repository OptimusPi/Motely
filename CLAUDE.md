# CLAUDE.md

@AGENTS.md
@README.md

**Links only. Do not inline these files.** Read the one you need, when you need it.

## Build / test

- SDK pinned by `global.json` (10.0.x). Solution is `Motely.slnx`.
- `dotnet build`
- `dotnet test` — xunit + Verify. A `*.received.*` next to a `*.verified.*` is a snapshot diff, not a pass.
- `dotnet run --project Motely.CLI -- --jaml JamlFilters/AlwaysPass.jaml --collect 1`
- WASM: `dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release` — always `-c Release` (LLVM). `-c Debug` is Mono. See AGENTS.md.

## Bootsharp documentation — `D:\bootsharp\docs\guide\`

| Doc | Read it when |
|-----|--------------|
| [serialization.md](D:\bootsharp\docs\guide\serialization.md) | anything crosses the boundary: enums as numbers + name maps, `Dictionary`→`Map`, records by value |
| [specialization.md](D:\bootsharp\docs\guide\specialization.md) | before changing interop shapes; byref never crosses |
| [interop-modules.md](D:\bootsharp\docs\guide\interop-modules.md) | module layout / namespace→path mapping |
| [interop-instances.md](D:\bootsharp\docs\guide\interop-instances.md) | classes/interfaces passed by reference |
| [renaming.md](D:\bootsharp\docs\guide\renaming.md) | `[RenameModule]` / `[RenameNode]`, what JS sees |
| [declarations.md](D:\bootsharp\docs\guide\declarations.md) | generated `.g.d.mts` from `[Export]` |
| [build-config.md](D:\bootsharp\docs\guide\build-config.md) | before touching `Motely.Wasm.csproj` |
| [llvm.md](D:\bootsharp\docs\guide\llvm.md) | NativeAOT-LLVM publish |
| [sideloading.md](D:\bootsharp\docs\guide\sideloading.md) | shipping the bundle |
| [getting-started.md](D:\bootsharp\docs\guide\getting-started.md) | wiring Bootsharp into a project the first time |
| [extensions/dependency-injection.md](D:\bootsharp\docs\guide\extensions\dependency-injection.md) | `Bootsharp.Inject` |
| [extensions/file-system.md](D:\bootsharp\docs\guide\extensions\file-system.md) | `Bootsharp.FileSystem` (source at `D:\extra`) |

Root: [D:\bootsharp\README.md](D:\bootsharp\README.md) · Samples: `D:\bootsharp\samples\` — `minimal`, `react`, `trimming`, `vscode`, `bench`

## Operator

- CAPS is emphasis, not distress. Typos are speed. Do not shift register.
- The FilterDesc is the source of truth. Never hand-type a list the engine already knows.
- Say what was checked and what was not. Do not state conclusions the evidence does not reach.
- Decide small things yourself. Do not ask the operator about one sentence.
- If another Claude session is live on this machine (ListAgents), message it directly. Do not make the operator relay.
