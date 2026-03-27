using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.Core;

[assembly: JSExport(typeof(IMotelyWasm))]
[assembly: JSImport(typeof(IMotelyUI))]

new ServiceCollection()
    .AddBootsharp()
    .AddSingleton<IMotelyWasm, MotelyWasm>()
    .BuildServiceProvider()
    .RunBootsharp();
