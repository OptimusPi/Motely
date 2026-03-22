using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.BrowserWasm;

[assembly: JSPreferences(Space = ["^Motely\\.BrowserWasm\\.", "MotelyWasm."])]

[assembly: JSExport(typeof(Motely.BrowserWasm.IMotelyBrowserApi), typeof(Motely.BrowserWasm.IMotelySingleSearchContext))]

public static partial class Program
{
    public static void Main()
    {
        new ServiceCollection()
            .AddBootsharp()
            .AddSingleton<IMotelyBrowserApi, MotelyBrowserApi>()
            .BuildServiceProvider()
            .RunBootsharp();
    }
}
