using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;

[assembly: JSExport(typeof(Motely.IMotelyWasm))]
[assembly: JSPreferences(Space = [
    @"^Motely\.Analysis\.(\S+)", "$1",
    @"^Motely\.(\S+)", "$1"
])]

new ServiceCollection()
    .AddSingleton<Motely.IMotelyWasm, Motely.MotelyWasmImpl>()
    .AddBootsharp()
    .BuildServiceProvider()
    .RunBootsharp();
