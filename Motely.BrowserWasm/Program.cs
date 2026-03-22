using System.Runtime.Versioning;
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.BrowserWasm;

[assembly: SupportedOSPlatform("browser")]
[assembly: JSExport(typeof(IMotelyWasmBackend))]
[assembly: JSImport(typeof(IMotelyJsUi))]
[assembly: JSPreferences(Space = ["^Motely\\.BrowserWasm\\.", "MotelyWasm."])]

new ServiceCollection()
    .AddBootsharp()
    .AddSingleton<IMotelyWasmBackend, MotelyWasmBackend>()
    .BuildServiceProvider()
    .RunBootsharp();
