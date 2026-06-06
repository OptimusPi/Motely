using Motely.Filters;

namespace Motely.Analysis;

public sealed record class MotelyJamlyzerConfig(
    string Jaml,
    long StartBatch = 0,
    long EndBatch = 1,
    int BatchCharacterCount = 4,
    int ThreadCount = 1,
    bool IncludeSeedAnalysis = true
);

public sealed record class MotelyJamlyzerSeedListConfig(
    string Jaml,
    IReadOnlyList<string>? Seeds = null,
    bool IncludeSeedAnalysis = true
);

public sealed record class MotelyJamlyzerResult(
    string? Error,
    IReadOnlyList<MotelyJamlyzerSeedResult> Seeds,
    MotelyDeck? Deck = null,
    MotelyStake? Stake = null,
    string[]? TallyLabels = null,
    long TotalSeedsSearched = 0,
    long MatchingSeeds = 0,
    long CompletedBatchCount = 0
);

public sealed record class MotelyJamlyzerSeedResult(
    string Seed,
    int Score,
    int[] Tallies,
    MotelyLegacyTextAnalysis? Analysis = null
);

/// <summary>
/// Runs a JAML filter over a bounded sequential batch page and optionally attaches
/// the regular per-seed analyzer output for every match.
/// </summary>
public static class MotelyJamlyzer
{
    public static MotelyJamlyzerResult AnalyzeSeeds(MotelyJamlyzerSeedListConfig cfg)
    {
        if (!JamlConfigLoader.TryLoad(cfg.Jaml, out var config, out var error) || config is null)
            return new(error ?? "Invalid JAML.", []);
        return AnalyzeSeeds(config, cfg.Seeds, cfg.IncludeSeedAnalysis);
    }

    public static MotelyJamlyzerResult AnalyzeSeeds(
        JamlConfig config,
        IReadOnlyList<string>? seeds = null,
        bool includeSeedAnalysis = true
    )
    {
        try
        {
            var plan = JamlSearchBuilder.CreatePlan(config);
            var finalSeeds = seeds is { Count: > 0 } ? seeds : config.Seeds;
            if (finalSeeds.Count == 0)
            {
                return new(
                    "No seeds were provided. Pass seeds explicitly or add a top-level JAML seeds array.",
                    [],
                    config.Deck,
                    config.Stake
                );
            }

            var rows = new List<MotelyJamlyzerSeedResult>();
            var settings = plan
                .Settings.WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(1)
                .WithListSearch(finalSeeds.Select(NormalizeSeed), finalSeeds.Count)
                .WithScoredResultCallback(tally =>
                {
                    rows.Add(
                        new(
                            tally.Seed,
                            tally.Score,
                            tally.TallyValuesSpan.ToArray(),
                            includeSeedAnalysis ? BuildSeedAnalysis(tally.Seed, config) : null
                        )
                    );
                });

            using var search = settings.CreateSearch();
            RunSearchSynchronously(search);

            return new(
                null,
                rows,
                config.Deck,
                config.Stake,
                plan.TallyLabels,
                search.TotalSeedsSearched,
                search.MatchingSeeds,
                search.CompletedBatchCount
            );
        }
        catch (Exception ex)
        {
            return new(ex.ToString(), []);
        }
    }

    public static MotelyJamlyzerResult AnalyzeSeed(
        JamlConfig config,
        string seed,
        bool includeSeedAnalysis = true
    )
    {
        try
        {
            var plan = JamlSearchBuilder.CreatePlan(config);
            var normalizedSeed = NormalizeSeed(seed);
            MotelyJamlyzerSeedResult? result = null;

            var settings = plan
                .Settings.WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(1)
                .WithListSearch([normalizedSeed])
                .WithScoredResultCallback(tally =>
                {
                    result = new(
                        tally.Seed,
                        tally.Score,
                        tally.TallyValuesSpan.ToArray(),
                        includeSeedAnalysis ? BuildSeedAnalysis(tally.Seed, config) : null
                    );
                });

            using var search = settings.CreateSearch();
            RunSearchSynchronously(search);

            return new(
                null,
                result is null ? [] : [result],
                config.Deck,
                config.Stake,
                plan.TallyLabels,
                search.TotalSeedsSearched,
                search.MatchingSeeds,
                search.CompletedBatchCount
            );
        }
        catch (Exception ex)
        {
            return new(ex.ToString(), []);
        }
    }

    public static MotelyJamlyzerResult Analyze(MotelyJamlyzerConfig cfg)
    {
        ValidatePage(cfg);
        if (!JamlConfigLoader.TryLoad(cfg.Jaml, out var config, out var error) || config is null)
            return new(error ?? "Invalid JAML.", []);
        return Analyze(
            config,
            cfg.StartBatch,
            cfg.EndBatch,
            cfg.BatchCharacterCount,
            cfg.ThreadCount,
            cfg.IncludeSeedAnalysis
        );
    }

    public static MotelyJamlyzerResult Analyze(
        JamlConfig config,
        long startBatch,
        long endBatch,
        int batchCharacterCount,
        int threadCount,
        bool includeSeedAnalysis
    )
    {
        try
        {
            var plan = JamlSearchBuilder.CreatePlan(config);
            var rows = new List<MotelyJamlyzerSeedResult>();
            var rowsLock = new object();

            var settings = plan
                .Settings.WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(threadCount)
                .WithBatchCharacterCount(batchCharacterCount)
                .WithStartBatchIndex(startBatch)
                .WithEndBatchIndex(endBatch)
                .WithSequentialSearch()
                .WithScoredResultCallback(tally =>
                {
                    var row = new MotelyJamlyzerSeedResult(
                        tally.Seed,
                        tally.Score,
                        tally.TallyValuesSpan.ToArray()
                    );

                    lock (rowsLock)
                        rows.Add(row);
                });

            using var search = settings.CreateSearch();
            RunSearchSynchronously(search);

            IReadOnlyList<MotelyJamlyzerSeedResult> results = includeSeedAnalysis
                ? AttachSeedAnalysis(rows, config)
                : rows;

            return new(
                null,
                results,
                config.Deck,
                config.Stake,
                plan.TallyLabels,
                search.TotalSeedsSearched,
                search.MatchingSeeds,
                search.CompletedBatchCount
            );
        }
        catch (Exception ex)
        {
            return new(ex.ToString(), []);
        }
    }

    private static void ValidatePage(MotelyJamlyzerConfig cfg)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cfg.StartBatch);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cfg.EndBatch, cfg.StartBatch);
        ArgumentOutOfRangeException.ThrowIfLessThan(cfg.BatchCharacterCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cfg.BatchCharacterCount, 7);
        ArgumentOutOfRangeException.ThrowIfLessThan(cfg.ThreadCount, 1);
    }

    private static IReadOnlyList<MotelyJamlyzerSeedResult> AttachSeedAnalysis(
        IReadOnlyList<MotelyJamlyzerSeedResult> rows,
        JamlConfig config
    )
    {
        var analyzed = new MotelyJamlyzerSeedResult[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            analyzed[i] = row with { Analysis = BuildSeedAnalysis(row.Seed, config) };
        }

        return analyzed;
    }

    private static MotelyLegacyTextAnalysis BuildSeedAnalysis(string seed, JamlConfig config)
    {
        // The JAMLyzer highlight layer was removed in commit b3bc5477 ("Cleanup") — it
        // operated on the old MotelySeedAnalysis type, which the legacy-analyzer refactor
        // replaced with MotelyLegacyTextAnalysis. Analyze and return directly.
        return MotelyLegacyTextAnalyzer.Analyze(new(seed, config.Deck, config.Stake));
    }

    private static void RunSearchSynchronously(IMotelySearch search)
    {
        search.RunSearchUntilCompletion();
    }

    private static string NormalizeSeed(string seed) =>
        seed.Trim().ToUpperInvariant().Replace('0', 'O');
}
