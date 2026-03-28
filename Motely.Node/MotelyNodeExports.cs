using System.Diagnostics;
using Microsoft.JavaScript.NodeApi;
using Motely;
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

    /// <summary>
    /// Run a JAML search. Returns JSON array of matching seeds.
    /// </summary>
    public static string RunSearch(
        string jamlContent,
        int threadCount = 0,
        int batchCharCount = 3,
        long startBatch = 0,
        long endBatch = long.MaxValue,
        Action<long, long, long>? onProgress = null,
        Action<string, double>? onResult = null)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new ArgumentException($"Invalid JAML: {error}");

        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(threadCount > 0 ? threadCount : Environment.ProcessorCount)
            .WithBatchCharacterCount(batchCharCount)
            .WithSequentialSearch()
            .WithStartBatchIndex(startBatch)
            .WithEndBatchIndex(endBatch);

        return ExecuteSearch(settings, onProgress, onResult);
    }

    /// <summary>
    /// Run a JAML search over a specific list of seeds. Returns JSON array of matching seeds.
    /// </summary>
    public static string RunSeedListSearch(
        string jamlContent,
        string[] seeds,
        int threadCount = 0,
        Action<long, long, long>? onProgress = null,
        Action<string, double>? onResult = null)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new ArgumentException($"Invalid JAML: {error}");

        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(threadCount > 0 ? threadCount : Environment.ProcessorCount)
            .WithListSearch(seeds, seeds.Length);

        return ExecuteSearch(settings, onProgress, onResult);
    }

    /// <summary>
    /// Run a JAML search over random seeds. Returns JSON array of matching seeds.
    /// </summary>
    public static string RunRandomSearch(
        string jamlContent,
        int count,
        int threadCount = 0,
        Action<long, long, long>? onProgress = null,
        Action<string, double>? onResult = null)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new ArgumentException($"Invalid JAML: {error}");

        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(threadCount > 0 ? threadCount : Environment.ProcessorCount)
            .WithRandomSearch(count);

        return ExecuteSearch(settings, onProgress, onResult);
    }

    private static string ExecuteSearch(
        IMotelySearchSettings settings,
        Action<long, long, long>? onProgress,
        Action<string, double>? onResult)
    {
        var sw = Stopwatch.StartNew();
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();

        if (onProgress != null)
            settings.WithProgressCallback(p =>
                onProgress(p.SeedsSearched, p.MatchingSeeds, (long)p.ElapsedTime.TotalMilliseconds));

        settings.WithSeedMatchCallback(seed =>
        {
            results.Add(seed);
            onResult?.Invoke(seed, 0);
        });

        settings.WithScoredResultCallback(tally =>
        {
            results.Add(tally.Seed);
            onResult?.Invoke(tally.Seed, tally.Score);
        });

        using var search = settings.Start();
        search.AwaitCompletion();

        return System.Text.Json.JsonSerializer.Serialize(results.ToArray());
    }
}
