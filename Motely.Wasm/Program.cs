using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;

[assembly: Export(typeof(Motely.IMotelyWasm))]
[assembly: Import([
    typeof(Motely.IMotelyWasmEvents),
    typeof(Bootsharp.FileSystem.IFileMounter)
])]
[assembly: Preferences(Space = [
    @"^Motely\.Analysis\.(\S+)", "$1",
    @"^Motely\.(\S+)", "$1"
])]

new ServiceCollection()
    .AddSingleton<Motely.IMotelyWasm, Motely.MotelyWasmHost>()
    .AddBootsharp()
    .BuildServiceProvider()
    .RunBootsharp();
