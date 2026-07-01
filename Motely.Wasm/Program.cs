using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.Wasm;

[assembly: Export(typeof(IMotelyBackend))]

new ServiceCollection()
    .AddBootsharp()
    .AddSingleton<IMotelyBackend, MotelyBackend>()
    .BuildServiceProvider()
    .RunBootsharp();
