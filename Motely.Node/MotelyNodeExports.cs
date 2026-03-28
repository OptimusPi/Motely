using Microsoft.JavaScript.NodeApi;
using Motely.Analysis;
using Motely.Filters;

namespace Motely.NodeInterop;

[JSExport]
public static class MotelyNodeExports
{
    public static string? ValidateJaml(string jamlContent) =>
        JamlConfigLoader.TryLoad(jamlContent, out _, out var error) ? null : error;

    public static string AnalyzeSeed(string seed, string deck, string stake) =>
        System.Text.Json.JsonSerializer.Serialize(MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake));

    public static string GetVersion() =>
        MotelyBuildVersion.For(typeof(MotelyNodeExports).Assembly);
}
