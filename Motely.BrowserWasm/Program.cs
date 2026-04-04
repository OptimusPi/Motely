#nullable enable
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.Analysis;
using Motely.Filters;

[assembly: JSExport(typeof(Motely.BrowserWasm.MotelyProgram), typeof(Motely.BrowserWasm.IMotelyProgram))]
[assembly: JSImport([typeof(Motely.BrowserWasm.ISearchEvents)])]
[assembly: JSPreferences(
    Space = [@"^Motely\.BrowserWasm\.(\S+)", "$1", @"^Motely\.(\S+)", "$1"]
)]

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
    /// <summary>
    /// Parses a JAML string into a <see cref="JamlConfig"/> ready for search.
    /// Throws <see cref="InvalidOperationException"/> with a descriptive message on failure.
    /// </summary>
    JamlConfig LoadJaml(string jaml);
    /// <summary>
    /// Compiles Jummy text to a <see cref="JamlConfig"/> ready for search.
    /// Throws <see cref="InvalidOperationException"/> with a descriptive message on failure.
    /// </summary>
    JamlConfig CompileJummy(string jummy);
    MotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
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
    private MotelySeedRouterDesc? _singleSeedRouter;

    public string GetVersion() => VersionInfo.Version;

    public JamlConfig LoadJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return config;
    }

    public JamlConfig CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");

        if (!JamlConfigLoader.TryLoad(jamlYaml, out var config, out var loadErr))
            throw new InvalidOperationException(loadErr ?? "Invalid JAML after Jummy compile.");

        return config;
    }
    /// <summary>
    /// Single-seed context from a minimal list-search; prior router is disposed.
    /// </summary>
    public MotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _singleSeedRouter?.Dispose();
        _singleSeedRouter = new MotelySeedRouterDesc(seed, deck, stake);
        return _singleSeedRouter.Instance();
    }

    private IMotelySearchSettings Plan(JamlConfig jaml, int batchCharCount, int threadCount = -1)
    {
        var built = JamlSearchBuilder.CreatePlan(jaml);
        return built.Settings
            .WithDeck(jaml.Deck)
            .WithStake(jaml.Stake)
            .WithThreadCount(threadCount < 1 ? 1 : threadCount)
            .WithBatchCharacterCount(batchCharCount);
    }

    public void StartConfiguredSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        var settings = Plan(jaml, batchCharCount);

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

        WireAndRun(settings);
    }

    public void StartSequentialSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        if (jaml.Aesthetics.Count > 0)
            throw new InvalidOperationException(
                "This JAML declares aesthetics; use StartConfiguredSearch or StartAestheticSearch.");

        var settings = Plan(jaml, batchCharCount).WithSequentialSearch();
        if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

        WireAndRun(settings);
    }

    public void StartRandomSearch(JamlConfig jaml, int randomSeedCount, int batchCharCount)
    {
        WireAndRun(Plan(jaml, batchCharCount).WithRandomSearch(Math.Max(1, randomSeedCount)));
    }

    public void StartAestheticSearch(JamlConfig jaml, int aesthetic, int batchCharCount)
    {
        if (aesthetic < 0 || aesthetic > (int)JamlAesthetic.Balatro)
            throw new ArgumentOutOfRangeException(nameof(aesthetic));

        WireAndRun(Plan(jaml, batchCharCount).WithAestheticSearch((JamlAesthetic)aesthetic));
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

        var padded = MotelyGlobals.GeneratePaddedSeedsForKeywords(keywords, pad);
        WireAndRun(
            Plan(jaml, batchCharCount).WithProviderSearch(new MotelySeedListProvider(padded, padded.Count())));
    }

    public void StartSeedListSearch(JamlConfig jaml, string seedsCsv, int threadCount)
    {
        var seeds = seedsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        WireAndRun(Plan(jaml, 4, threadCount).WithListSearch(seeds, seeds.Length));
    }

    private void WireAndRun(IMotelySearchSettings settings)
    {
        settings = settings.WithProgressCallback(p =>
            _events.NotifyProgress(p.SeedsSearched, p.MatchingSeeds, p.ElapsedMilliseconds));

        settings = settings.WithScoredResultCallback(t =>
            _events.NotifyResult(t.Seed, t.Score, t.TallyColumns.ToArray()));

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
