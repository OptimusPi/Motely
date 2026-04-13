#nullable enable
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using MotelyJaml;

namespace Motely.BrowserWasm;

public static class Program
{
    public static void Main()
    {
        try
        {
            new ServiceCollection()
                .AddBootsharp()
                .AddSingleton<MotelySingleSearchContext>()
                .AddSingleton<IMotelySingleSearchContext>(static sp => sp.GetRequiredService<MotelySingleSearchContext>())
                .AddSingleton<MotelyJamlSearchBuilder>()
                .AddSingleton<IMotelyJamlSearchBuilder>(static sp => sp.GetRequiredService<MotelyJamlSearchBuilder>())
                .AddSingleton<MotelyWasmHost>()
                .AddSingleton<IMotelyWasmHost>(static sp => sp.GetRequiredService<MotelyWasmHost>())
                .BuildServiceProvider()
                .RunBootsharp();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"BOOT CRASH: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
                Console.Error.WriteLine($"INNER: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            throw;
        }
    }
}
