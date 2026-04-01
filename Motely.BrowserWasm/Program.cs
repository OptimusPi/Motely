#nullable enable
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.Analysis;
using Motely.Filters;

[assembly: JSExport(typeof(Motely.BrowserWasm.IMotelyProgram))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]

namespace Motely.BrowserWasm;

/// <summary>C# → JS events (search progress, results, completion).
/// Bootsharp.Inject auto-implements this interface; Notify* methods become On* JS events.</summary>
public interface ISearchEvents
{
    void NotifyProgress(long seedsSearched, long matchingSeeds, long elapsedMs);
    void NotifyResult(string seed, int score);
    void NotifyComplete(string status, long seedsSearched, long matchingSeeds);
}

/// <summary>JS → C# exports. Returns real Motely types — Bootsharp marshals them
/// as interop instances so JS gets the full API (cancel, dispose, fluent builders, etc.).</summary>
public interface IMotelyProgram
{
    string GetVersion();
    string? ValidateJaml(string jamlContent);

    /// <summary>Creates a <see cref="MotelySeedRouterDesc"/> — the real Motely seed context,
    /// not the legacy text analyzer. Call Instance() on the result for full stream access.</summary>
    MotelySeedRouterDesc CreateSeedRouter(string seed, MotelyDeck deck, MotelyStake stake);

    IMotelySearch StartConfiguredSearch(string jamlContent, int threadCount, int batchCharCount, long startBatch, long endBatch);
    IMotelySearch StartSequentialSearch(string jamlContent, int threadCount, int batchCharCount, long startBatch, long endBatch);
    IMotelySearch StartRandomSearch(string jamlContent, int randomSeedCount, int threadCount, int batchCharCount);
    IMotelySearch StartAestheticSearch(string jamlContent, int aesthetic, int threadCount, int batchCharCount);
    IMotelySearch StartKeywordSearch(string jamlContent, string keywordsCsv, string paddingChars, int threadCount, int batchCharCount);
    IMotelySearch StartSeedListSearch(string jamlContent, string seedsCsv, int threadCount);
}

public class MotelyProgram(ISearchEvents events) : IMotelyProgram
{
    private readonly ISearchEvents _events = events;

    public string GetVersion() => VersionInfo.Version;

    public string? ValidateJaml(string jamlContent)
    {
        return JamlConfigLoader.TryLoad(jamlContent, out _, out var error)
            ? null
            : error ?? "JAML validation failed.";
    }

    public MotelySeedRouterDesc CreateSeedRouter(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return new MotelySeedRouterDesc(seed, deck, stake);
    }

    public IMotelySearch StartConfiguredSearch(
        string jamlContent, int threadCount, int batchCharCount, long startBatch, long endBatch)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount);

        bool usesSequential = config.Aesthetics.Count == 0;
        if (!usesSequential)
            settings = settings.WithAestheticSearch(config.Aesthetics[0]);
        else
            settings = settings.WithSequentialSearch();

        if (usesSequential)
        {
            if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
            if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);
        }

        return WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public IMotelySearch StartSequentialSearch(
        string jamlContent, int threadCount, int batchCharCount, long startBatch, long endBatch)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        if (config.Aesthetics.Count > 0)
            throw new InvalidOperationException(
                "This JAML declares top-level aesthetics; use StartConfiguredSearch or StartAestheticSearch.");

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount)
            .WithSequentialSearch();

        if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

        return WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public IMotelySearch StartRandomSearch(
        string jamlContent, int randomSeedCount, int threadCount, int batchCharCount)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount)
            .WithRandomSearch(Math.Max(1, randomSeedCount));

        return WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public IMotelySearch StartAestheticSearch(
        string jamlContent, int aesthetic, int threadCount, int batchCharCount)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        if (aesthetic < 0 || aesthetic > (int)JamlAesthetic.Balatro)
            throw new ArgumentOutOfRangeException(nameof(aesthetic));

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount)
            .WithAestheticSearch((JamlAesthetic)aesthetic);

        return WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public IMotelySearch StartKeywordSearch(
        string jamlContent, string keywordsCsv, string paddingChars, int threadCount, int batchCharCount)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        var keywords = keywordsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static k => k.Trim().ToUpperInvariant())
            .Where(static k => k.Length > 0)
            .ToArray();

        if (keywords.Length == 0)
            throw new ArgumentException("At least one keyword is required.", nameof(keywordsCsv));

        char[]? pad = null;
        if (!string.IsNullOrEmpty(paddingChars))
        {
            pad = paddingChars
                .ToUpperInvariant()
                .Where(static c => MotelyGlobals.SeedDigits.Contains(c))
                .Distinct()
                .ToArray();
        }

        var plan = JamlSearchBuilder.CreatePlan(config);
        var padded = MotelyGlobals.GeneratePaddedSeedsForKeywords(keywords, pad);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount)
            .WithProviderSearch(new MotelySeedListProvider(padded, padded.Count()));

        return WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public IMotelySearch StartSeedListSearch(string jamlContent, string seedsCsv, int threadCount)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        var seeds = seedsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithListSearch(seeds, seeds.Length);

        return WireAndStart(settings, false);
    }

    private IMotelySearch WireAndStart(IMotelySearchSettings settings, bool hasScoring)
    {
        settings = settings
            .WithProgressCallback(p => _events.NotifyProgress(
                p.SeedsSearched, p.MatchingSeeds, (long)p.ElapsedTime.TotalMilliseconds));

        if (hasScoring)
            settings = settings.WithScoredResultCallback(t => _events.NotifyResult(t.Seed, t.Score));
        else
            settings = settings.WithSeedMatchCallback(seed => _events.NotifyResult(seed, 0));

        var search = settings.Start();
        _ = NotifyOnCompletionAsync(search);
        return search;
    }

    private async Task NotifyOnCompletionAsync(IMotelySearch search)
    {
        try
        {
            await search.WaitForCompletionAsync();
            _events.NotifyComplete("completed", search.TotalSeedsSearched, search.MatchingSeeds);
        }
        catch (OperationCanceledException)
        {
            _events.NotifyComplete("cancelled", search.TotalSeedsSearched, search.MatchingSeeds);
        }
        catch (Exception ex)
        {
            _events.NotifyComplete($"error: {ex.Message}", search.TotalSeedsSearched, search.MatchingSeeds);
        }
    }
}

public static class Program
{
    public static void Main()
    {
        new ServiceCollection()
            .AddBootsharp()
            .AddSingleton<IMotelyProgram, MotelyProgram>()
            .BuildServiceProvider()
            .RunBootsharp();
    }
}
