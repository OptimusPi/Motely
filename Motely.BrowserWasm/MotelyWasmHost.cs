#nullable enable
using Motely;
using Motely.Analysis;
using Motely.Filters;

namespace Motely.BrowserWasm;

/// <summary>Callbacks from the WASM search engine to JavaScript (progress, results, completion).</summary>
public interface ISearchEvents
{
    void NotifyProgress(long seedsSearched, long matchingSeeds, long elapsedMs);
    void NotifyResult(string seed, int score, int[] tallyColumns);
    void NotifyComplete(string status, long seedsSearched, long matchingSeeds);
}

/// <summary>
/// NativeAOT-LLVM WASM surface: embeddable host API for Motely (not a CLI). JS calls this object; events flow via <see cref="ISearchEvents"/>.
/// </summary>
public interface IMotelyWasmHost
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
    IMotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    /// <summary>
    /// If <paramref name="jaml"/> declares <c>aesthetics</c>, runs aesthetic provider search ( <paramref name="batchCharCount"/> and batch range are ignored).
    /// Otherwise sequential search with the given batch settings.
    /// </summary>
    void StartConfiguredSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch);
    void StartConfiguredSearchBySearchIndex(
        JamlConfig jaml,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    void StartSequentialSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch);
    void StartSequentialSearchBySearchIndex(
        JamlConfig jaml,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    /// <summary>Random/provider search — does not use sequential <c>batchCharCount</c> (see core <c>MotelyProviderSearchPlan</c>).</summary>
    void StartRandomSearch(JamlConfig jaml, int randomSeedCount);
    void StartAestheticSearch(JamlConfig jaml, int aesthetic);
    void StartKeywordSearch(JamlConfig jaml, string keywordsCsv, string paddingChars);
    void StartSeedListSearch(JamlConfig jaml, string seedsCsv, int threadCount);
    /// <summary>Cancels the in-flight search started by any <c>Start*</c> method, if any.</summary>
    void StopSearch();
}

public sealed class MotelyWasmHost : IMotelyWasmHost
{
    private readonly ISearchEvents _events;
    private IMotelySearch? _activeSearch;
    private MotelySeedRouterDesc? _singleSeedRouter;

    public MotelyWasmHost(ISearchEvents events)
    {
        _events = events;
    }

    public string GetVersion() { return VersionInfo.Version; }

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
    public IMotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _singleSeedRouter?.Dispose();
        _singleSeedRouter = new MotelySeedRouterDesc(seed, deck, stake);
        return new MotelySingleSearchContextImpl(_singleSeedRouter);
    }

    /// <summary>Deck/stake/thread only — for provider/list/random/aesthetic/keyword modes (sequential batch size is not used).</summary>
    private static IMotelySearchSettings PlanProviderSearch(JamlConfig jaml, int threadCount = -1)
    {
        var built = JamlSearchBuilder.CreatePlan(jaml);
        return built.Settings
            .WithDeck(jaml.Deck)
            .WithStake(jaml.Stake)
            .WithThreadCount(threadCount < 1 ? 1 : threadCount);
    }

    /// <summary>Includes <see cref="IMotelySearchSettings.WithBatchCharacterCount"/> — only meaningful for sequential search.</summary>
    private static IMotelySearchSettings PlanSequentialSearch(
        JamlConfig jaml,
        int batchCharCount,
        int threadCount = -1)
    {
        return PlanProviderSearch(jaml, threadCount).WithBatchCharacterCount(batchCharCount);
    }

    public void StartConfiguredSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        IMotelySearchSettings settings;
        if (jaml.Aesthetics.Count > 0)
        {
            // Provider-style search; batchCharCount / batch range do not apply.
            settings = PlanProviderSearch(jaml).WithAestheticSearch(jaml.Aesthetics[0]);
        }
        else
        {
            settings = PlanSequentialSearch(jaml, batchCharCount).WithSequentialSearch();
            if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
            if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);
        }

        WireAndRun(settings);
    }

    public void StartConfiguredSearchBySearchIndex(
        JamlConfig jaml,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        if (jaml.Aesthetics.Count > 0)
            throw new InvalidOperationException(
                "JAML declares aesthetics; seed-index ranges apply only to sequential search. Use StartConfiguredSearch or StartAestheticSearch.");

        var (sb, ebExclusive) = SeedMath.SearchIndexRangeToBatchRange(
            startSeedSearchIndex,
            stopSeedSearchIndexInclusive,
            batchCharCount);
        StartConfiguredSearch(jaml, batchCharCount, sb, ebExclusive);
    }

    public void StartSequentialSearch(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        if (jaml.Aesthetics.Count > 0)
            throw new InvalidOperationException(
                "This JAML declares aesthetics; use StartConfiguredSearch or StartAestheticSearch.");

        var settings = PlanSequentialSearch(jaml, batchCharCount).WithSequentialSearch();
        if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

        WireAndRun(settings);
    }

    public void StartSequentialSearchBySearchIndex(
        JamlConfig jaml,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        if (jaml.Aesthetics.Count > 0)
            throw new InvalidOperationException(
                "This JAML declares aesthetics; use StartConfiguredSearch or StartAestheticSearch.");

        var (sb, ebExclusive) = SeedMath.SearchIndexRangeToBatchRange(
            startSeedSearchIndex,
            stopSeedSearchIndexInclusive,
            batchCharCount);
        StartSequentialSearch(jaml, batchCharCount, sb, ebExclusive);
    }

    public void StartRandomSearch(JamlConfig jaml, int randomSeedCount)
    {
        WireAndRun(
            PlanProviderSearch(jaml).WithRandomSearch(Math.Max(1, randomSeedCount)));
    }

    public void StartAestheticSearch(JamlConfig jaml, int aesthetic)
    {
        if (aesthetic < 0 || aesthetic > (int)JamlAesthetic.Balatro)
            throw new ArgumentOutOfRangeException(nameof(aesthetic));

        WireAndRun(
            PlanProviderSearch(jaml).WithAestheticSearch((JamlAesthetic)aesthetic));
    }

    public void StartKeywordSearch(JamlConfig jaml, string keywordsCsv, string paddingChars)
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
        long keywordSeedCount = MotelyGlobals.GetPaddedSeedCountForKeywordsLong(keywords, pad);
        WireAndRun(
            PlanProviderSearch(jaml)
                .WithProviderSearch(new MotelySeedListProvider(padded, keywordSeedCount)));
    }

    public void StartSeedListSearch(JamlConfig jaml, string seedsCsv, int threadCount)
    {
        var seeds = seedsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        WireAndRun(PlanProviderSearch(jaml, threadCount).WithListSearch(seeds, seeds.Length));
    }

    public void StopSearch() { _activeSearch?.Cancel(); }

    private void WireAndRun(IMotelySearchSettings settings)
    {
        _activeSearch?.Cancel();

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
