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

## Pinned: Bootsharp docs (`D:bootsharpdocsguide`) — loaded every session

@D:/bootsharp/docs/index.md
@D:/bootsharp/docs/guide/index.md
@D:/bootsharp/docs/guide/getting-started.md
@D:/bootsharp/docs/guide/build-config.md
@D:/bootsharp/docs/guide/declarations.md
@D:/bootsharp/docs/guide/interop-modules.md
@D:/bootsharp/docs/guide/interop-instances.md
@D:/bootsharp/docs/guide/serialization.md
@D:/bootsharp/docs/guide/renaming.md
@D:/bootsharp/docs/guide/specialization.md
@D:/bootsharp/docs/guide/sideloading.md
@D:/bootsharp/docs/guide/llvm.md
@D:/bootsharp/docs/guide/extensions/dependency-injection.md
@D:/bootsharp/docs/guide/extensions/file-system.md

## Pinned: Bootsharp samples

@D:/bootsharp/samples/minimal/README.md
@D:/bootsharp/samples/react/README.md
@D:/bootsharp/samples/vscode/README.md
@D:/bootsharp/samples/trimming/README.md
@D:/bootsharp/samples/bench/readme.md

@D:/bootsharp/README.md

Source: `D:\bootsharp\src\cs\` (Bootsharp.Publish inspection/codegen, Bootsharp.Common specializations)

## Operator

- CAPS is emphasis, not distress. Typos are speed. Do not shift register.
- The FilterDesc is the source of truth. Never hand-type a list the engine already knows.
- Say what was checked and what was not. Do not state conclusions the evidence does not reach.
- Decide small things yourself. Do not ask the operator about one sentence.
- If another Claude session is live on this machine (ListAgents), message it directly. Do not make the operator relay.
