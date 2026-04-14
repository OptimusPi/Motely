#nullable enable
using Bootsharp;
using Bootsharp.Inject;

[assembly: JSExport(
    typeof(Motely.BrowserWasm.IMotelyWasmHost))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]
[assembly: JSPreferences(
    Space = [
        @"^Motely\.BrowserWasm\.(\S+)", "$1",
        @"^Motely\.(\S+)", "$1",
        @"^MotelyJaml\.(\S+)", "$1"
    ]
)]
