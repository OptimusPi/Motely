#nullable enable
using Bootsharp;
using Bootsharp.Inject;

[assembly: JSExport(
    typeof(Motely.BrowserWasm.IMotelySingleSearchContext),
    typeof(Motely.Analysis.IMotelySingleSearchContextImpl),
    typeof(MotelyJaml.IMotelyJamlSearchBuilder),
    typeof(Motely.BrowserWasm.IMotelySearchSession))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]
// JSPreferences strips C# namespace prefixes from generated JS bindings.
// Order matters: more specific rules first.
//   Motely.Analysis.X  →  X     (so JS sees `IMotelySingleSearchContextImpl`,
//                                 not `Analysis.IMotelySingleSearchContextImpl`).
//   Motely.BrowserWasm.X → X
//   Motely.X           →  X
//   MotelyJaml.X       →  X
// Keeping `Motely.Analysis` explicit (not relying on the generic `Motely.*` rule)
// because pifreak confirmed this layout previously produced a clean, working
// build of the WASM bindings.
[assembly: JSPreferences(
    Space = [
        @"^Motely\.Analysis\.(\S+)", "$1",
        @"^Motely\.BrowserWasm\.(\S+)", "$1",
        @"^Motely\.Analysis\.(\S+)", "$1",
        @"^Motely\.(\S+)", "$1",
        @"^MotelyJaml\.(\S+)", "$1"
    ]
)]
