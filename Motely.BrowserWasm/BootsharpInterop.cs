#nullable enable
using Bootsharp;
using Bootsharp.Inject;

[assembly: JSExport(
    typeof(Motely.BrowserWasm.IMotelySingleSearchContext),
    typeof(Motely.Analysis.IMotelySingleSearchContextImpl),
    typeof(MotelyJaml.IMotelyJamlSearchBuilder),
    typeof(Motely.IMotelySearch))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]
[assembly: JSPreferences(
    Space = [
        @"^Motely\.BrowserWasm\.(\S+)", "$1",
        @"^Motely\.Analysis\.(\S+)", "$1",
        @"^Motely\.(\S+)", "$1",
        @"^MotelyJaml\.(\S+)", "$1"
    ]
)]
