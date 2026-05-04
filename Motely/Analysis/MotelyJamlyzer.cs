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
    SeedAnalysisDto? Analysis = null
);

internal sealed record class MotelyJamlyzerResolvedConfig(
    JamlConfig Config,
    JamlSearchPlan Plan,
    IReadOnlyList<string>? Seeds = null
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
            var resolved = TryResolveSeedListConfig(cfg, out var errorResult);
            if (resolved is null)
                return errorResult!;

            var config = resolved.Config;
            var plan = resolved.Plan;
            var seeds = resolved.Seeds!;
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
                                ? BuildSeedAnalysisDto(tally.Seed, config)
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
            var resolved = TryResolveConfig(cfg.Jaml, out var errorResult);
            if (resolved is null)
                return errorResult!;

            var normalizedSeed = NormalizeSeed(cfg.Seed);
            var config = resolved.Config;
            var plan = resolved.Plan;
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
                            ? BuildSeedAnalysisDto(tally.Seed, config)
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

            var resolved = TryResolveConfig(cfg.Jaml, out var errorResult);
            if (resolved is null)
                return errorResult!;

            var config = resolved.Config;
            var plan = resolved.Plan;
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
                Analysis = BuildSeedAnalysisDto(row.Seed, config),
            };
        }

        return analyzed;
    }

    private static MotelyJamlyzerResolvedConfig? TryResolveSeedListConfig(
        MotelyJamlyzerSeedListConfig cfg,
        out MotelyJamlyzerResult? errorResult
    )
    {
        errorResult = null;

        var resolved = TryResolveConfig(cfg.Jaml, out errorResult);
        if (resolved is null)
            return null;

        var seeds = cfg.Seeds is { Count: > 0 } ? cfg.Seeds : resolved.Config.Seeds;
        if (seeds.Count == 0)
        {
            errorResult = new(
                "No seeds were provided. Pass seeds explicitly or add a top-level JAML seeds array.",
                [],
                resolved.Config.Deck,
                resolved.Config.Stake
            );
            return null;
        }

        return resolved with { Seeds = seeds };
    }

    private static MotelyJamlyzerResolvedConfig? TryResolveConfig(
        string jaml,
        out MotelyJamlyzerResult? errorResult
    )
    {
        errorResult = null;

        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error) || config is null)
        {
            errorResult = new(error ?? "Invalid JAML.", []);
            return null;
        }

        if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)
        {
            errorResult = new("JAML has no clauses.", [], config.Deck, config.Stake);
            return null;
        }

        return new(config, JamlSearchBuilder.CreatePlan(config));
    }

    private static SeedAnalysisDto BuildSeedAnalysisDto(string seed, JamlConfig config)
    {
        var analysis = MotelySeedAnalyzer.Analyze(new(seed, config.Deck, config.Stake));
        var dto = SeedAnalysisDtoMapper.FromSeedAnalysis(seed, config.Deck, config.Stake, analysis);
        return MotelyJamlyzerHighlights.Apply(config, dto);
    }

    private static string NormalizeSeed(string seed) =>
        seed.Trim().ToUpperInvariant().Replace('0', 'O');
}
