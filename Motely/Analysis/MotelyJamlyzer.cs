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

public sealed record class MotelyJamlyzerSeedAnalysisConfig(
    string Seed,
    string Jaml,
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
    MotelyLegacyTextAnalyzer? Analysis = null
);

/// <summary>
/// Runs a JAML filter over a bounded sequential batch page and optionally attaches
/// the regular per-seed analyzer output for every match.
/// </summary>
public static class MotelyJamlyzer
{
    public static MotelyJamlyzerResult AnalyzeSeeds(MotelyJamlyzerSeedListConfig cfg)
    {
        try
        {
            if (!JamlConfigLoader.TryLoad(cfg.Jaml, out var config, out var error) || config is null)
                return new(error ?? "Invalid JAML.", []);

            if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)
                return new("JAML has no clauses.", [], config.Deck, config.Stake);

            var seeds = cfg.Seeds is { Count: > 0 } ? cfg.Seeds : config.Seeds;
            if (seeds.Count == 0)
                return new("No seeds were provided. Pass seeds explicitly or add a top-level JAML seeds array.", [], config.Deck, config.Stake);

            var plan = JamlSearchBuilder.CreatePlan(config);
            var rows = new List<MotelyJamlyzerSeedResult>();

            var settings = plan.Settings
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(1)
                .WithListSearch(seeds.Select(NormalizeSeed), seeds.Count)
                .WithScoredResultCallback(tally =>
                {
                    rows.Add(
                        new(
                            tally.Seed,
                            tally.Score,
                            tally.TallyValuesSpan.ToArray(),
                            cfg.IncludeSeedAnalysis
                                ? MotelyJamlyzerHighlights.Apply(
                                    config,
                                    MotelySeedAnalyzer.Analyze(new(tally.Seed, config.Deck, config.Stake))
                                )
                                : null
                        )
                    );
                });

            using var search = settings.Start();
            search.AwaitCompletion();

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

    public static MotelyJamlyzerResult AnalyzeSeed(MotelyJamlyzerSeedAnalysisConfig cfg)
    {
        try
        {
            if (!JamlConfigLoader.TryLoad(cfg.Jaml, out var config, out var error) || config is null)
                return new(error ?? "Invalid JAML.", []);

            if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)
                return new("JAML has no clauses.", [], config.Deck, config.Stake);

            var normalizedSeed = NormalizeSeed(cfg.Seed);
            var plan = JamlSearchBuilder.CreatePlan(config);
            MotelyJamlyzerSeedResult? result = null;

            var settings = plan.Settings
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(1)
                .WithListSearch([normalizedSeed])
                .WithScoredResultCallback(tally =>
                {
                    result = new(
                        tally.Seed,
                        tally.Score,
                        tally.TallyValuesSpan.ToArray(),
                        cfg.IncludeSeedAnalysis
                            ? MotelyJamlyzerHighlights.Apply(
                                config,
                                MotelySeedAnalyzer.Analyze(new(tally.Seed, config.Deck, config.Stake))
                            )
                            : null
                    );
                });

            using var search = settings.Start();
            search.AwaitCompletion();

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
        try
        {
            ValidatePage(cfg);

            if (!JamlConfigLoader.TryLoad(cfg.Jaml, out var config, out var error) || config is null)
                return new(error ?? "Invalid JAML.", []);

            if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)
                return new("JAML has no clauses.", [], config.Deck, config.Stake);

            var plan = JamlSearchBuilder.CreatePlan(config);
            var rows = new List<MotelyJamlyzerSeedResult>();
            var rowsLock = new object();

            var settings = plan.Settings
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(cfg.ThreadCount)
                .WithBatchCharacterCount(cfg.BatchCharacterCount)
                .WithStartBatchIndex(cfg.StartBatch)
                .WithEndBatchIndex(cfg.EndBatch)
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

            using var search = settings.Start();
            search.AwaitCompletion();

            IReadOnlyList<MotelyJamlyzerSeedResult> results = cfg.IncludeSeedAnalysis
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
            analyzed[i] = row with
            {
                Analysis = MotelyJamlyzerHighlights.Apply(
                    config,
                    MotelySeedAnalyzer.Analyze(new(row.Seed, config.Deck, config.Stake))
                ),
            };
        }

        return analyzed;
    }

    private static string NormalizeSeed(string seed) =>
        seed.Trim().ToUpperInvariant().Replace('0', 'O');
}
