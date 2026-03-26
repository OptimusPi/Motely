using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;

[assembly: JSImport(typeof(IMotelyUI))]

new ServiceCollection()
    .AddBootsharp()
    .BuildServiceProvider()
    .RunBootsharp();
