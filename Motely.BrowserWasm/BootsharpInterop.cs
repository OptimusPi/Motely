#nullable enable
using Bootsharp;
using Bootsharp.Inject;

[assembly: JSExport(
    typeof(Motely.BrowserWasm.MotelySingleSearchContext), typeof(Motely.BrowserWasm.IMotelySingleSearchContext),
    typeof(Motely.Analysis.MotelySingleSearchContextImpl), typeof(Motely.Analysis.IMotelySingleSearchContextImpl),
    typeof(Motely.BrowserWasm.MotelyWasmHost), typeof(Motely.BrowserWasm.IMotelyWasmHost),
    typeof(MotelyJaml.MotelyJamlSearchBuilder), typeof(MotelyJaml.IMotelyJamlSearchBuilder),
    typeof(Motely.BrowserWasm.MotelySearchSession), typeof(Motely.BrowserWasm.IMotelySearchSession))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]
[assembly: JSPreferences(
    Space = [
        @"^Motely\.BrowserWasm\.(\S+)", "$1",
        @"^Motely\.(\S+)", "$1",
        @"^MotelyJaml\.(\S+)", "$1"
    ]
)]
