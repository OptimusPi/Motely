using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;

[assembly: Export(typeof(Motely.IMotelyWasm))]
[assembly: Import([
    typeof(Motely.IMotelyWasmEvents),
    typeof(Bootsharp.FileSystem.IFileMounter)
])]

new ServiceCollection()
    .AddSingleton<Motely.IMotelyWasm, Motely.MotelyWasmHost>()
    .AddBootsharp()
    .BuildServiceProvider()
    .RunBootsharp();
