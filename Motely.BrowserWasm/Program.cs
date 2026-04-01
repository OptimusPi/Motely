#nullable enable
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.Analysis;
using Motely.Filters;

[assembly: JSExport(typeof(Motely.BrowserWasm.IMotelyProgram))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]

namespace Motely.BrowserWasm;

public interface ISearchEvents
{
    void NotifyProgress(long seedsSearched, long matchingSeeds, long elapsedMs);
    void NotifyResult(string seed, int score, int[] tallyColumns);
    void NotifyComplete(string status, long seedsSearched, long matchingSeeds);
}

public interface IMotelyProgram
{
    string GetVersion();
    IMotelyAnalyzer CreateAnalyzer(string seed, MotelyDeck deck, MotelyStake stake);
    void StopSearch();
    void StartConfiguredSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch);
    void StartSequentialSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch);
    void StartRandomSearch(JamlConfig jaml, int randomSeedCount, int batchCharCount);
    void StartAestheticSearch(JamlConfig jaml, int aesthetic, int batchCharCount);
    void StartKeywordSearch(JamlConfig jaml, string keywordsCsv, string paddingChars, int batchCharCount);
    void StartSeedListSearch(JamlConfig jaml, string seedsCsv, int threadCount);
}

public class MotelyProgram(ISearchEvents events) : IMotelyProgram
{
    private readonly ISearchEvents _events = events;
    private IMotelySearch? _activeSearch;

    public string GetVersion() => VersionInfo.Version;

    public IMotelyAnalyzer CreateAnalyzer(string seed, MotelyDeck deck, MotelyStake stake) =>
        new MotelyAnalyzer(new MotelySeedRouterDesc(seed, deck, stake));

    public void StopSearch()
    {
        _activeSearch?.Cancel();
        _activeSearch?.Dispose();
        _activeSearch = null;
    }

    private (IMotelySearchSettings Settings, bool HasScoring) Plan(
        JamlConfig jaml, int batchCharCount, int threadCount = -1)
    {
        var plan = JamlSearchBuilder.CreatePlan(jaml);
        var settings = plan.Settings
            .WithDeck(jaml.Deck)
            .WithStake(jaml.Stake)
            .WithThreadCount(threadCount < 1 ? Environment.ProcessorCount : threadCount)
            .WithBatchCharacterCount(batchCharCount);
        return (settings, plan.ShouldClauseCount > 0);
    }

    public void StartConfiguredSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        var (settings, hasScoring) = Plan(jaml, batchCharCount);

        if (jaml.Aesthetics.Count > 0)
        {
            settings = settings.WithAestheticSearch(jaml.Aesthetics[0]);
        }
        else
        {
            settings = settings.WithSequentialSearch();
            if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
            if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);
        }

        WireAndRun(settings, hasScoring);
    }

    public void StartSequentialSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        if (jaml.Aesthetics.Count > 0)
            throw new InvalidOperationException(
                "This JAML declares aesthetics; use StartConfiguredSearch or StartAestheticSearch.");

        var (settings, hasScoring) = Plan(jaml, batchCharCount);
        settings = settings.WithSequentialSearch();
        if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

        WireAndRun(settings, hasScoring);
    }

    public void StartRandomSearch(JamlConfig jaml, int randomSeedCount, int batchCharCount)
    {
        var (settings, hasScoring) = Plan(jaml, batchCharCount);
        WireAndRun(settings.WithRandomSearch(Math.Max(1, randomSeedCount)), hasScoring);
    }

    public void StartAestheticSearch(JamlConfig jaml, int aesthetic, int batchCharCount)
    {
        if (aesthetic < 0 || aesthetic > (int)JamlAesthetic.Balatro)
            throw new ArgumentOutOfRangeException(nameof(aesthetic));

        var (settings, hasScoring) = Plan(jaml, batchCharCount);
        WireAndRun(settings.WithAestheticSearch((JamlAesthetic)aesthetic), hasScoring);
    }

    public void StartKeywordSearch(JamlConfig jaml, string keywordsCsv, string paddingChars, int batchCharCount)
    {
        var keywords = keywordsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static k => k.Trim().ToUpperInvariant())
            .Where(static k => k.Length > 0)
            .ToArray();

        if (keywords.Length == 0)
            throw new ArgumentException("At least one keyword is required.", nameof(keywordsCsv));

        char[]? pad = string.IsNullOrEmpty(paddingChars) ? null
            : paddingChars.ToUpperInvariant()
                .Where(static c => MotelyGlobals.SeedDigits.Contains(c))
                .Distinct()
                .ToArray();

        var (settings, hasScoring) = Plan(jaml, batchCharCount);
        var padded = MotelyGlobals.GeneratePaddedSeedsForKeywords(keywords, pad);
        WireAndRun(settings.WithProviderSearch(new MotelySeedListProvider(padded, padded.Count())), hasScoring);
    }

    public void StartSeedListSearch(JamlConfig jaml, string seedsCsv, int threadCount)
    {
        var seeds = seedsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (settings, _) = Plan(jaml, 4, threadCount);
        WireAndRun(settings.WithListSearch(seeds, seeds.Length), hasScoring: false);
    }

    private void WireAndRun(IMotelySearchSettings settings, bool hasScoring)
    {
        settings = settings.WithProgressCallback(p =>
            _events.NotifyProgress(p.SeedsSearched, p.MatchingSeeds, (long)p.ElapsedTime.TotalMilliseconds));

        if (hasScoring)
            settings = settings.WithScoredResultCallback(t =>
                _events.NotifyResult(t.Seed, t.Score, t.TallyColumns.ToArray()));
        else
            settings = settings.WithSeedMatchCallback(seed =>
                _events.NotifyResult(seed, 0, []));

        _activeSearch = settings.Start();
        _ = NotifyOnCompletionAsync(_activeSearch);
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
