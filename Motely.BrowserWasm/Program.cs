using System.Runtime.Versioning;
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.BrowserWasm.Interop;

[assembly: SupportedOSPlatform("browser")]
[assembly: JSExport(typeof(IMotelyWasmBackend))]
[assembly: JSImport(typeof(IMotelyJsUi))]
[assembly: JSPreferences(Space = ["^Motely\\.BrowserWasm\\.Interop\\.", "MotelyWasm."])]

new ServiceCollection()
    .AddBootsharp()
    .AddSingleton<IMotelyWasmBackend, MotelyWasmBackend>()
    .BuildServiceProvider()
    .RunBootsharp();
