using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.BrowserWasm;
using Motely.Executors;
using Motely.Filters;

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

    [JSInvokable]
    public static string? ValidateJaml(string jamlContent) =>
        JamlConfigLoader.TryLoad(jamlContent, out _, out var error) ? null : error;

    [JSInvokable]
    public static string? ValidateRequest(MotelySearchRequest request) =>
        MotelySearchOrchestrator.ValidateRequest(request);
}
