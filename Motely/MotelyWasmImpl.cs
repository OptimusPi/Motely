using Motely.Analysis;
using Motely.Filters;
using System.Collections.Concurrent;

namespace Motely;

public sealed class MotelyWasmImpl : IMotelyWasm
{
    public MotelyWasmImpl() { }

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

    public IMotelyWasmSearch StartRandomSearch(string jaml, int randomSeedCount)
    {
        var config = ParseJaml(jaml);
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithRandomSearch(Math.Max(1, randomSeedCount));
        return RunSearch(settings);
    }

    public IMotelyWasmSearch StartAestheticSearch(string jaml, JamlAesthetic aesthetic)
    {
        var config = ParseJaml(jaml);
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithAestheticSearch(aesthetic);
        return RunSearch(settings);
    }

    public IMotelyWasmSearch StartSequentialSearch(string jaml, int batchCharCount,
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
        return RunSearch(settings);
    }

    public async Task<MotelyWasmSearchBatchResult> RunSequentialSearchBatch(
        string jaml,
        int batchCharCount,
        long startBatch,
        long endBatch,
        int maxResults)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchCharCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxResults, 0);

        using var search = StartSequentialSearch(jaml, batchCharCount, startBatch, endBatch);
        var completion = await search.WaitForCompletion();
        var results = search.DrainResults(maxResults);
        return new(completion, results);
    }

    public IMotelyWasmSearch StartSeedListSearch(string jaml, string[] seeds)
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
        return RunSearch(settings);
    }

    public IMotelyWasmSearch StartKeywordSearch(string jaml, string keywordsCsv,
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
        return RunSearch(settings);
    }

    private JamlConfig ParseJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    private IMotelyWasmSearch RunSearch(IMotelySearchSettings settings)
    {
        var results = new ConcurrentQueue<MotelyWasmSearchResult>();
        settings = settings
            .WithSeedMatchCallback(seed => results.Enqueue(new(seed, 0, [])))
            .WithScoredResultCallback(t => results.Enqueue(new(t.Seed, t.Score, t.TallyColumns.ToArray())));

        var search = settings.Start();
        return new MotelyWasmSearch(search, results);
    }
}
