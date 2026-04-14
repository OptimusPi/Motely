using Motely.Analysis;
using Motely.Filters;

namespace Motely;

public sealed class MotelyWasmImpl : IMotelyWasm
{
    private readonly IMotelyWasmEvents _events;
    private readonly object _sync = new();
    private IMotelySearch? _currentSearch;

    public MotelyWasmImpl(IMotelyWasmEvents events)
    {
        _events = events;
    }

    public string GetVersion() => VersionInfo.Version;

    public string ValidateJaml(string jaml)
    {
        if (JamlConfigLoader.TryLoad(jaml, out var config, out var error))
        {
            try { JamlSearchBuilder.EnsureRunnablePlan(config); }
            catch (Exception ex) { return ex.Message; }
            return "valid";
        }
        return error ?? "Invalid JAML.";
    }

    public string CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jaml, out var error))
            throw new InvalidOperationException(error ?? "Invalid Jummy.");
        return jaml;
    }

    public IMotelyWasmSearchContext CreateSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return new MotelyWasmSearchContext(seed, deck, stake);
    }

    public void StartRandomSearch(string jaml, int randomSeedCount)
    {
        var config = ParseJaml(jaml);
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithRandomSearch(Math.Max(1, randomSeedCount));
        RunSearch(settings);
    }

    public void StartSequentialSearch(string jaml, int batchCharCount,
        long startBatch, long endBatch)
    {
        var config = ParseJaml(jaml);
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithBatchCharacterCount(batchCharCount)
            .WithSequentialSearch();
        if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);
        RunSearch(settings);
    }

    public void StartSeedListSearch(string jaml, string[] seeds)
    {
        var config = ParseJaml(jaml);
        var trimmed = seeds
            .Select(static s => s.Trim())
            .Where(static s => s.Length > 0)
            .ToArray();
        if (trimmed.Length == 0)
            throw new ArgumentException("At least one non-empty seed is required.");
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithListSearch(trimmed, trimmed.Length);
        RunSearch(settings);
    }

    public void StartKeywordSearch(string jaml, string keywordsCsv,
        string paddingChars)
    {
        var config = ParseJaml(jaml);
        var keywords = keywordsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static k => k.Trim().ToUpperInvariant())
            .Where(static k => k.Length > 0)
            .ToArray();
        if (keywords.Length == 0)
            throw new ArgumentException("At least one keyword is required.");
        var provider = new MotelyKeywordSeedProvider(keywords,
            string.IsNullOrEmpty(paddingChars) ? null
                : paddingChars.ToUpperInvariant().Distinct().ToArray());
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithProviderSearch(provider);
        RunSearch(settings);
    }

    public void StopSearch()
    {
        lock (_sync) _currentSearch?.Cancel();
    }

    private JamlConfig ParseJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    private void RunSearch(IMotelySearchSettings settings)
    {
        settings = settings
            .WithProgressCallback(p =>
                _events.NotifyProgress(p.SeedsSearched, p.MatchingSeeds))
            .WithScoredResultCallback(t =>
                _events.NotifyResult(t.Seed, t.Score, t.TallyColumns.ToArray()));

        var search = settings.Start();

        lock (_sync)
        {
            _currentSearch?.Cancel();
            _currentSearch = search;
        }

        _ = WatchAsync(search);
    }

    private async Task WatchAsync(IMotelySearch search)
    {
        try
        {
            await search.WaitForCompletionAsync();
            _events.NotifyComplete("completed",
                search.TotalSeedsSearched, search.MatchingSeeds);
        }
        catch (OperationCanceledException)
        {
            _events.NotifyComplete("cancelled",
                search.TotalSeedsSearched, search.MatchingSeeds);
        }
        catch (Exception ex)
        {
            _events.NotifyComplete($"error: {ex.Message}",
                search.TotalSeedsSearched, search.MatchingSeeds);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_currentSearch, search))
                    _currentSearch = null;
            }
            search.Dispose();
        }
    }
}
