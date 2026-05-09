using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;

[assembly: Export(typeof(Motely.IMotelyWasmHost))]
[assembly: Import([
    typeof(Motely.IMotelyWasmEvents),
    typeof(Bootsharp.FileSystem.IFileMounter)
])]
[assembly: Preferences(Space = ["Motely.MotelyWasmHost", "Motely.MotelyWasm"])]

new ServiceCollection()
    .AddSingleton<Motely.IMotelyWasmHost, Motely.MotelyWasmHost>()
    .AddBootsharp()
    .BuildServiceProvider()
    .RunBootsharp();
