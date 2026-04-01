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
    /// <summary>Fired periodically during search with cumulative counters.</summary>
    void NotifyProgress(long seedsSearched, long matchingSeeds, long elapsedMs);
    /// <summary>Fired for each matching seed. <paramref name="score"/> is 0 when no scoring provider is active.</summary>
    void NotifyResult(string seed, int score);
    /// <summary>Fired once when the search finishes or is cancelled.</summary>
    void NotifyComplete(string status, long seedsSearched, long matchingSeeds);
}

/// <summary>JS → C# exports (called from browser)</summary>
public interface IMotelyProgram
{
    string GetVersion();
    /// <summary>Returns <c>null</c> on success, or an error message describing the first problem.</summary>
    string? ValidateJaml(string jamlContent);

    MotelyAnalysisView AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake);
    MotelySeedRouterDesc CreateSeedRouter(string seed, MotelyDeck deck, MotelyStake stake);

    /// <summary>Cancel the currently running search (no-op if none).</summary>
    void StopSearch();

    /// <summary>Honor JAML as authored: sequential batching unless <c>aesthetics</c> selects a provider.</summary>
    void StartConfiguredSearch(
        string jamlContent,
        int threadCount,
        int batchCharCount,
        long startBatch,
        long endBatch
    );

    /// <summary>Sequential full seed space; JAML must not declare top-level <c>aesthetics</c>.</summary>
    void StartSequentialSearch(
        string jamlContent,
        int threadCount,
        int batchCharCount,
        long startBatch,
        long endBatch
    );

    void StartRandomSearch(
        string jamlContent,
        int randomSeedCount,
        int threadCount,
        int batchCharCount
    );

    /// <param name="aesthetic">Numeric <see cref="JamlAesthetic"/> value (0 = Palindrome, …).</param>
    void StartAestheticSearch(
        string jamlContent,
        int aesthetic,
        int threadCount,
        int batchCharCount
    );

    /// <summary>Comma-separated keywords (padded enumeration). Empty <paramref name="paddingChars"/> uses all seed chars.</summary>
    void StartKeywordSearch(
        string jamlContent,
        string keywordsCsv,
        string paddingChars,
        int threadCount,
        int batchCharCount
    );

    void StartSeedListSearch(string jamlContent, string seedsCsv, int threadCount);
}

public class MotelyProgram(ISearchEvents events) : IMotelyProgram
{
    private readonly ISearchEvents _events = events;
    private IMotelySearch? _activeSearch;

    public string GetVersion() => VersionInfo.Version;

    public string? ValidateJaml(string jamlContent)
    {
        return JamlConfigLoader.TryLoad(jamlContent, out _, out var error)
            ? null
            : error ?? "JAML validation failed.";
    }

    public MotelyAnalysisView AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake)
    {
        var result = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, deck, stake));
        return MotelyAnalysisView.From(result);
    }

    public MotelySeedRouterDesc CreateSeedRouter(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return new MotelySeedRouterDesc(seed, deck, stake);
    }

    public void StopSearch()
    {
        _activeSearch?.Cancel();
        _activeSearch?.Dispose();
        _activeSearch = null;
    }

    public void StartConfiguredSearch(
        string jamlContent,
        int threadCount,
        int batchCharCount,
        long startBatch,
        long endBatch
    )
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

        WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public void StartSequentialSearch(
        string jamlContent,
        int threadCount,
        int batchCharCount,
        long startBatch,
        long endBatch
    )
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            throw new InvalidOperationException($"Invalid JAML: {error}");

        if (config.Aesthetics.Count > 0)
        {
            throw new InvalidOperationException(
                "This JAML declares top-level aesthetics; use StartConfiguredSearch or StartAestheticSearch, or remove aesthetics from the document."
            );
        }

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Math.Max(1, threadCount))
            .WithBatchCharacterCount(batchCharCount)
            .WithSequentialSearch();

        if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

        WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public void StartRandomSearch(
        string jamlContent,
        int randomSeedCount,
        int threadCount,
        int batchCharCount
    )
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

        WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public void StartAestheticSearch(
        string jamlContent,
        int aesthetic,
        int threadCount,
        int batchCharCount
    )
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

        WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public void StartKeywordSearch(
        string jamlContent,
        string keywordsCsv,
        string paddingChars,
        int threadCount,
        int batchCharCount
    )
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

        WireAndStart(settings, plan.ShouldClauseCount > 0);
    }

    public void StartSeedListSearch(string jamlContent, string seedsCsv, int threadCount)
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

        WireAndStart(settings, false);
    }

    private void WireAndStart(IMotelySearchSettings settings, bool hasScoring)
    {
        StopSearch();

        settings = settings
            .WithProgressCallback(p => _events.NotifyProgress(
                p.SeedsSearched, p.MatchingSeeds, (long)p.ElapsedTime.TotalMilliseconds));

        if (hasScoring)
            settings = settings.WithScoredResultCallback(t => _events.NotifyResult(t.Seed, t.Score));
        else
            settings = settings.WithSeedMatchCallback(seed => _events.NotifyResult(seed, 0));

        var search = settings.Start();
        _activeSearch = search;
        _ = Task.Run(async () =>
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
        });
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
