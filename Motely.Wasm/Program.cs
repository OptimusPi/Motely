using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;

[assembly: JSExport(typeof(Motely.IMotelyWasm))]
[assembly: JSImport([
    typeof(Motely.IMotelyWasmEvents),
    typeof(Bootsharp.FileSystem.IFileMounter)
])]
[assembly: JSPreferences(Space = [
    @"^Motely\.Analysis\.(\S+)", "$1",
    @"^Motely\.(\S+)", "$1"
])]

GC.KeepAlive(typeof(Bootsharp.FileSystem.IFileMounter));

new ServiceCollection()
    .AddSingleton<Motely.IMotelyWasm, Motely.MotelyWasmHost>()
    .AddBootsharp()
    .BuildServiceProvider()
    .RunBootsharp();
