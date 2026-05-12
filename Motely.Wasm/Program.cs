using Bootsharp;
using System.Collections.Generic;
using System.Text.Json;
using Motely.Analysis;
using Motely.Filters;

public static partial class Program
{
    /// <summary>Run a JAML search synchronously and return matching seeds.
    /// If the JAML has a seeds: list, the search is constrained to it.
    /// Single-threaded — Motely.Wasm publishes with WasmEnableThreads=false.</summary>
    [Export]
    public static string[] Search(string jamlYaml)
    {
        if (!JamlConfigLoader.TryLoad(jamlYaml, out var config, out var error))
            throw new System.ArgumentException(error ?? "JAML parse failed");

        var plan = JamlSearchBuilder.CreatePlan(config);
        var hits = new List<string>();

        var settings = plan.Settings
            .WithThreadCount(1)
            .WithSeedMatchCallback(hits.Add);

        if (config.Seeds.Count > 0)
            settings = settings.WithListSearch(config.Seeds, config.Seeds.Count);

        using var search = settings.Start();
        return hits.ToArray();
    }

    /// <summary>Analyze a single seed under the deck/stake declared in the supplied JAML and return
    /// the canonical <see cref="SeedAnalysisDto"/> as JSON. Runs the analyzer unconditionally — the
    /// JAML is consumed for deck/stake context and for shop-item match highlights; the seed does not
    /// need to satisfy must/should clauses.</summary>
    [Export]
    public static string Analyze(string seed, string jamlYaml)
    {
        if (!JamlConfigLoader.TryLoad(jamlYaml, out var config, out var error) || config is null)
            throw new System.ArgumentException(error ?? "JAML parse failed");

        var normalized = seed.Trim().ToUpperInvariant().Replace('0', 'O');
        var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(normalized, config.Deck, config.Stake));
        var dto = SeedAnalysisDtoMapper.FromSeedAnalysis(normalized, config.Deck, config.Stake, analysis);
        dto = MotelyJamlyzerHighlights.Apply(config, dto);

        return JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto);
    }

    public static void Main() { }
}
