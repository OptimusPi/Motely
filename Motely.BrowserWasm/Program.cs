using System.Reflection;
using Bootsharp;
using Motely;
using Motely.Analysis;
using Motely.Filters;

[assembly: JSExport(typeof(Motely.BrowserWasm.IMotelyProgram))]

namespace Motely.BrowserWasm;

public interface IMotelyProgram
{
    string GetVersion();
    string? ValidateJaml(string jamlContent);
    MotelySeedAnalysis AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake);
    MotelySeedRouterDesc LoadSeed(string seed, MotelyDeck deck, MotelyStake stake);
}

public class MotelyProgram : IMotelyProgram
{
    public string GetVersion()
    {
        var asm = typeof(MotelyProgram).Assembly;
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    public string? ValidateJaml(string jamlContent)
    {
        return JamlConfigLoader.TryLoad(jamlContent, out _, out var error)
            ? null
            : error ?? "JAML validation failed.";
    }

    public MotelySeedAnalysis AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, deck, stake));
    }

    public MotelySeedRouterDesc LoadSeed(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return new MotelySeedRouterDesc(seed, deck, stake);
    }
}

public static class Program
{
    public static void Main() { }
}
