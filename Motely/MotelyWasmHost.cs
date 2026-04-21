using Motely.Filters;

namespace Motely;

/// <summary>
/// Single <c>[JSExport]</c> facade for the browser. All search modes pin
/// <c>WithThreadCount(1)</c> — browser WASM is single-threaded. Scored results stream out
/// through <see cref="IMotelyWasmEvents"/> (Bootsharp <c>[JSImport]</c>).
/// </summary>
public sealed class MotelyWasmHost : IMotelyWasm
{
    private readonly IMotelyWasmEvents _events;

    public MotelyWasmHost(IMotelyWasmEvents events)
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

    public IMotelyWasmSearch StartRandomSearch(string jaml, int randomSeedCount)
    {
        return StartSearch(jaml, settings => settings.WithRandomSearch(Math.Max(1, randomSeedCount)));
    }

    public IMotelyWasmSearch StartAestheticSearch(string jaml, JamlAesthetic aesthetic)
    {
        return StartSearch(jaml, settings => settings.WithAestheticSearch(aesthetic));
    }

    public IMotelyWasmSearch StartSequentialSearch(string jaml, int batchCharCount, long startBatch, long endBatch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchCharCount, 1, nameof(batchCharCount));
        return StartSearch(jaml, settings =>
        {
            var s = settings
                .WithBatchCharacterCount(batchCharCount)
                .WithSequentialSearch();
            if (startBatch > 0) s = s.WithStartBatchIndex(startBatch);
            if (endBatch > 0) s = s.WithEndBatchIndex(endBatch);
            return s;
        });
    }

    public async Task<MotelyWasmSearchBatchResult> RunSequentialSearchBatch(
        string jaml,
        int batchCharCount,
        long startBatch,
        long endBatch,
        int maxResults
    )
    {
        var results = new List<MotelyWasmSearchResult>();
        var search = StartSequentialSearch(
            jaml,
            batchCharCount,
            startBatch,
            endBatch,
            result => {
                if (results.Count < Math.Max(0, maxResults))
                    results.Add(result);
            }
        );
        try
        {
            var completion = await search.WaitForCompletion();
            return new(completion, [.. results]);
        }
        finally
        {
            search.Dispose();
        }
    }

    public IMotelyWasmSearch StartSeedListSearch(string jaml, string[] seeds)
    {
        var trimmed = (seeds ?? Array.Empty<string>())
            .Select(static s => s.Trim())
            .Where(static s => s.Length > 0)
            .ToArray();
        if (trimmed.Length == 0)
            throw new ArgumentException("StartSeedListSearch requires at least one non-empty seed.", nameof(seeds));
        return StartSearch(jaml, settings => settings.WithListSearch(trimmed, trimmed.Length));
    }

    public IMotelyWasmSearch StartKeywordSearch(string jaml, string keywordsCsv, string paddingChars)
    {
        var normalized = (keywordsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static k => k.ToUpperInvariant())
            .Where(static k => k.Length > 0)
            .ToArray();
        if (normalized.Length == 0)
            throw new ArgumentException("StartKeywordSearch requires at least one keyword.", nameof(keywordsCsv));
        var padding = string.IsNullOrEmpty(paddingChars)
            ? null
            : paddingChars.ToUpperInvariant().Distinct().ToArray();
        var provider = new MotelyKeywordSeedProvider(normalized, padding);
        return StartSearch(jaml, settings => settings.WithProviderSearch(provider));
    }

    private IMotelyWasmSearch StartSequentialSearch(
        string jaml,
        int batchCharCount,
        long startBatch,
        long endBatch,
        Action<MotelyWasmSearchResult>? onResult
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchCharCount, 1, nameof(batchCharCount));
        return StartSearch(jaml, settings =>
        {
            var s = settings
                .WithBatchCharacterCount(batchCharCount)
                .WithSequentialSearch();
            if (startBatch > 0) s = s.WithStartBatchIndex(startBatch);
            if (endBatch > 0) s = s.WithEndBatchIndex(endBatch);
            return s;
        }, onResult);
    }

    private IMotelyWasmSearch StartSearch(
        string jaml,
        Func<IMotelySearchSettings, IMotelySearchSettings> configureMode,
        Action<MotelyWasmSearchResult>? onResult = null
    )
    {
        var config = ParseJaml(jaml);
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1);

        settings = configureMode(settings);

        var events = _events;
        settings = settings.WithScoredResultCallback(tally =>
        {
            var tallyColumns = tally.TallyColumns.ToArray();
            onResult?.Invoke(new(tally.Seed, tally.Score, tallyColumns));
            events.NotifyResult(tally.Seed, tally.Score, tallyColumns);
        });

        var search = settings.Start();
        return new MotelyWasmSearch(search);
    }

    public string[] GetTallyLabels(string jaml) =>
        JamlSearchBuilder.CreatePlan(ParseJaml(jaml)).TallyLabels;

    private static JamlConfig ParseJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }
}
