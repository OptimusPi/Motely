#nullable enable
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;

[assembly: JSExport(typeof(Motely.BrowserWasm.MotelyWasmHost), typeof(Motely.BrowserWasm.IMotelyWasmHost))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]
[assembly: JSPreferences(
    Space = [@"^Motely\.BrowserWasm\.(\S+)", "$1", @"^Motely\.(\S+)", "$1"]
)]

namespace Motely.BrowserWasm;

/// <summary>WASM runtime entry only — Bootsharp bootstrap. Application API is <see cref="MotelyWasmHost"/>.</summary>
public static class Program
{
    public static void Main()
    {
        new ServiceCollection()
            .AddBootsharp()
            .AddSingleton<IMotelyWasmHost, MotelyWasmHost>()
            .BuildServiceProvider()
            .RunBootsharp();
    }
}
