using Motely.Analysis;
using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Regression tests for every JAML filter file in Motely.Tests/filters.
/// Each filter must: parse without errors, compile into a search plan, and successfully search ≥1 seed.
/// New filters added to the data folder are automatically picked up — no code change needed.
/// </summary>
public class V0FilterRegressionTests
{
    private static readonly string FilterDataPath = Path.Combine(AppContext.BaseDirectory, "filters");
    private static readonly string[] LogicProbeSeeds = BuildLogicProbeSeeds(256);
    private static readonly string[] AnalyzerSeedCandidates =
    [
        "AAAAAAAA",
        "BBBBBBBB",
        "CCCCCCCC",
        "DDDDDDDD",
        "EEEEEEEE",
        "FFFFFFFF",
        "GGGGGGGG",
        "HHHHHHHH",
    ];

    public sealed record FilterFileCase(string FileName, string FullPath)
    {
        public override string ToString() => FileName;
    }

    private static List<FilterFileCase> GetFilterFiles()
    {
        var files = new List<FilterFileCase>();
        if (!Directory.Exists(FilterDataPath))
            return files;

        foreach (string file in Directory.GetFiles(FilterDataPath, "*.jaml").OrderBy(f => f))
        {
            files.Add(new FilterFileCase(Path.GetFileName(file), file));
        }

        return files;
    }

    private static JamlConfig ParseFilterOrFail(FilterFileCase filter)
    {
        bool parsed = JamlConfigLoader.TryLoadFromFile(filter.FullPath, out var config, out var error);
        Assert.True(parsed, $"[{filter.FileName}] JAML parse failed: {error}");
        Assert.NotNull(config);
        return config!;
    }

    private static string[] BuildLogicProbeSeeds(int count)
    {
        var seeds = new string[count];
        int digitCount = SeedDigits.Length;

        for (int i = 0; i < count; i++)
        {
            long value = i;
            var chars = new char[MaxSeedLength];
            for (int j = 0; j < chars.Length; j++)
            {
                chars[j] = SeedDigits[(int)(value % digitCount)];
                value /= digitCount;
            }
            seeds[i] = new string(chars);
        }

        return seeds;
    }

    private static (long SeedsSearched, long MatchingSeeds, string[] MatchedSeeds) RunListSearch(
        JamlConfig config,
        string[] seeds,
        bool captureMatches = false
    )
    {
        var matchedSeeds = captureMatches ? new HashSet<string>(StringComparer.Ordinal) : null;

        var settings = JamlSearchBuilder
            .CreateSettings(config)
            .WithListSearch(seeds, seeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true);

        if (captureMatches)
        {
            settings = settings
                .WithSeedMatchCallback(line =>
                {
                    int comma = line.IndexOf(',');
                    string seed = comma > 0 ? line[..comma] : line.Trim();
                    if (!string.IsNullOrWhiteSpace(seed))
                        matchedSeeds!.Add(seed);
                })
                .WithScoredResultCallback(tally =>
                {
                    if (!string.IsNullOrWhiteSpace(tally.Seed))
                        matchedSeeds!.Add(tally.Seed);
                });
        }

        using var search = settings.Start();
        search.AwaitCompletion();
        return (
            search.TotalSeedsSearched,
            search.MatchingSeeds,
            matchedSeeds?.ToArray() ?? []
        );
    }

    private static (string Seed, string TagName, int Ante)? FindAnalyzedSingleTagOccurrence()
    {
        foreach (var seed in AnalyzerSeedCandidates)
        {
            var analysis = MotelySeedAnalyzer.Analyze(
                new MotelySeedAnalysisConfig(seed, MotelyDeck.Red, MotelyStake.White)
            );

            if (!string.IsNullOrEmpty(analysis.Error))
                continue;

            foreach (var ante in analysis.Antes)
            {
                var targetTag = ante.BigBlindTag != ante.SmallBlindTag
                    ? ante.BigBlindTag
                    : ante.SmallBlindTag;

                return (seed, targetTag.ToString(), ante.Ante);
            }
        }

        return null;
    }

    [Fact]
    public void Filter_ParsesAndRuns()
    {
        foreach (var filter in GetFilterFiles())
        {
            var config = ParseFilterOrFail(filter);

            var settings = JamlSearchBuilder
                .CreateSettings(config)
                .WithSequentialSearch()
                .WithBatchCharacterCount(2)
                .WithStartBatchIndex(0)
                .WithEndBatchIndex(1)
                .WithThreadCount(1)
                .WithQuietMode(true);

            using var search = settings.Start();
            search.AwaitCompletion();

            Assert.True(search.IsCompleted, $"[{filter.FileName}] Search did not complete");
            Assert.True(search.TotalSeedsSearched > 0, $"[{filter.FileName}] No seeds were searched");
        }
    }

    [Fact]
    public void Filter_HasRequiredMetadata()
    {
        foreach (var filter in GetFilterFiles())
        {
            var config = ParseFilterOrFail(filter);

            Assert.False(
                string.IsNullOrWhiteSpace(config.Name),
                $"[{filter.FileName}] Missing 'name' field"
            );
            Assert.True(
                config.Must.HasAnyClauses,
                $"[{filter.FileName}] 'must' section is empty — every filter needs at least one must clause"
            );
        }
    }

    [Fact]
    public void Filter_DeckAndStakeAreValid()
    {
        foreach (var filter in GetFilterFiles())
        {
            var config = ParseFilterOrFail(filter);

            // Deck must be a known value (not defaulted to an invalid enum)
            Assert.True(
                Enum.IsDefined(config.Deck),
                $"[{filter.FileName}] Invalid or unrecognised deck: '{config.Deck}'"
            );
            Assert.True(
                Enum.IsDefined(config.Stake),
                $"[{filter.FileName}] Invalid or unrecognised stake: '{config.Stake}'"
            );
        }
    }

    [Fact]
    public void Filter_MustClauses_AreSelectiveAgainstProbeSeedSet()
    {
        foreach (var filter in GetFilterFiles())
        {
            var config = ParseFilterOrFail(filter);
            Assert.True(config.Must.HasAnyClauses, $"[{filter.FileName}] Expected at least one must clause");

            var filtered = RunListSearch(config, LogicProbeSeeds);

            Assert.Equal(LogicProbeSeeds.Length, filtered.SeedsSearched);
            Assert.True(
                filtered.MatchingSeeds < filtered.SeedsSearched,
                $"[{filter.FileName}] matched every probe seed; expected must clauses to filter at least one"
            );
        }
    }

    [Fact]
    public void MustAndMustNot_SameTag_RejectsSeed()
    {
        var match = FindAnalyzedSingleTagOccurrence();
        Assert.True(match.HasValue, "Expected to find at least one analyzed seed with a tag occurrence");

        var derived = match!.Value;
        var jaml = $$"""
            name: MustMustNotSameTag
            deck: Red
            stake: White
            must:
              - tag: {{derived.TagName}}
                antes: [{{derived.Ante}}]
            mustNot:
              - tag: {{derived.TagName}}
                antes: [{{derived.Ante}}]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var result = RunListSearch(config!, [derived.Seed]);
        Assert.Equal(1, result.SeedsSearched);
        Assert.Equal(0, result.MatchingSeeds);
    }

    [Fact]
    public void FilterDataFolder_IsNotEmpty()
    {
        Assert.True(
            Directory.Exists(FilterDataPath),
            $"Filter data folder not found: {FilterDataPath}"
        );
        var files = Directory.GetFiles(FilterDataPath, "*.jaml");
        Assert.True(files.Length > 0, "No .jaml files found in data folder");
    }
}
