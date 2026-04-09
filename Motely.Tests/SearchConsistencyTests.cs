using System.Collections.Concurrent;
using Motely.Analysis;
using Motely.Filters;
using Xunit;
using Xunit.Abstractions;

namespace Motely.Tests;

/// <summary>
/// Integration tests that run actual JAML searches and verify consistent results.
/// These tests do NOT hardcode "known matching seeds" — seed results can shift
/// whenever the PRNG implementation is corrected (e.g. the FMA rounding fix).
/// Instead, we verify behavioral invariants:
///   • Non-matching seeds → 0 results
///   • Same filter → same results across thread counts
///   • Callback captures exactly the seeds the engine reports
/// </summary>
public class SearchConsistencyTests(ITestOutputHelper output)
{
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

    /// <summary>
    /// A simple JAML filter that should match *some* seeds in a sequential search.
    /// Uses a broad filter (any joker in antes 1-2) so we're likely to find hits.
    /// </summary>
    private const string SimpleJaml = """
        name: SimpleTest
        deck: Red
        stake: White
        must:
          - joker: Showman
            antes: [1,2]
            sources:
              shopItems: [0,1]
              boosterPacks: [0,1]
        """;

    private static MotelyJokerRarity GetJokerRarity(MotelyItem item) =>
        (MotelyJokerRarity)((int)item.Type & (int)MotelyJokerRarity.Legendary);

    private static (string Seed, int Ante, string JokerName, int ShopSlotIndex)?
        FindAnalyzedUncommonShopItem()
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
                for (int shopSlotIndex = 0; shopSlotIndex < ante.ShopQueue.Count; shopSlotIndex++)
                {
                    var item = ante.ShopQueue[shopSlotIndex];
                    if (item.TypeCategory != MotelyItemTypeCategory.Joker)
                        continue;

                    if (GetJokerRarity(item) == MotelyJokerRarity.Uncommon)
                        return (seed, ante.Ante, item.Type.ToString(), shopSlotIndex);
                }
            }
        }

        return null;
    }

    private static (string Seed, int Ante, string SmallBlindTag, string BigBlindTag)?
        FindAnalyzedAnteWithDistinctTags()
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
                if (ante.SmallBlindTag != ante.BigBlindTag)
                    return (
                        seed,
                        ante.Ante,
                        ante.SmallBlindTag.ToString(),
                        ante.BigBlindTag.ToString()
                    );
            }
        }

        return null;
    }

    private static (string Seed, string TagName, int[] Antes, int Occurrences)?
        FindAnalyzedRepeatedAnyTag(int minOccurrences)
    {
        foreach (var seed in AnalyzerSeedCandidates)
        {
            var analysis = MotelySeedAnalyzer.Analyze(
                new MotelySeedAnalysisConfig(seed, MotelyDeck.Red, MotelyStake.White)
            );

            if (!string.IsNullOrEmpty(analysis.Error))
                continue;

            var counts = new Dictionary<MotelyTag, int>();
            var antesByTag = new Dictionary<MotelyTag, HashSet<int>>();

            foreach (var ante in analysis.Antes)
            {
                Record(ante.SmallBlindTag, ante.Ante);
                Record(ante.BigBlindTag, ante.Ante);
            }

            foreach (var pair in counts)
            {
                if (pair.Value >= minOccurrences)
                    return (seed, pair.Key.ToString(), antesByTag[pair.Key].Order().ToArray(), pair.Value);
            }

            void Record(MotelyTag tag, int ante)
            {
                counts[tag] = counts.TryGetValue(tag, out var existing) ? existing + 1 : 1;
                if (!antesByTag.TryGetValue(tag, out var antes))
                {
                    antes = [];
                    antesByTag[tag] = antes;
                }
                antes.Add(ante);
            }
        }

        return null;
    }

    private static (string Seed, string VoucherName, int Ante)?
        FindAnalyzedSingleVoucherOccurrence()
    {
        foreach (var seed in AnalyzerSeedCandidates)
        {
            var analysis = MotelySeedAnalyzer.Analyze(
                new MotelySeedAnalysisConfig(seed, MotelyDeck.Red, MotelyStake.White)
            );

            if (!string.IsNullOrEmpty(analysis.Error))
                continue;

            var ante = analysis.Antes.FirstOrDefault();
            if (ante == null)
                continue;

            return (seed, ante.Voucher.ToString(), ante.Ante);
        }

        return null;
    }

    private static (string Seed, string TagName, int Ante)?
        FindAnalyzedSingleTagOccurrence()
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

    private static (string Seed, int Ante, string TarotName, int ShopSlotIndex)?
        FindAnalyzedTarotShopItem()
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
                for (int shopSlotIndex = 0; shopSlotIndex < ante.ShopQueue.Count; shopSlotIndex++)
                {
                    var item = ante.ShopQueue[shopSlotIndex];
                    if (item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                        return (seed, ante.Ante, item.Type.ToString(), shopSlotIndex);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Non-matching seeds should produce 0 matches.
    /// </summary>
    [Fact]
    public async Task NonMatchingSeeds_ProduceZeroMatches()
    {
        Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var config, out _));

        string[] badSeeds = ["AAAAAAAA", "BBBBBBBB", "CCCCCCCC"];
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch(badSeeds, badSeeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine($"MatchingSeeds: {search.MatchingSeeds}");
        Assert.Equal(0, search.MatchingSeeds);
    }

    /// <summary>
    /// Multi-threaded searches must produce identical match counts.
    /// We first discover how many matches exist with 1 thread,
    /// then verify 2 and 4 threads produce the same count.
    /// </summary>
    [Fact]
    public async Task MatchCount_ConsistentAcrossThreadCounts()
    {
        Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var config, out _));

        // Use a small sequential search (3-char batch = 35^3 = ~42K seeds)
        // to find a baseline match count
        var baseline = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2) // 35^2 = 1225 seeds — fast!
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1) // One batch
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search1 = baseline.Start();
        await search1.WaitForCompletionAsync();

        long baselineMatches = search1.MatchingSeeds;
        long baselineSearched = search1.TotalSeedsSearched;
        output.WriteLine(
            $"Baseline: searched={baselineSearched}, matched={baselineMatches}"
        );

        // Now verify 2-thread and 4-thread produce the same match count
        foreach (int threadCount in new[] { 2, 4 })
        {
            Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var cfg, out _));
            var settings = JamlSearchBuilder
                .CreateSettings(cfg!)
                .WithSequentialSearch()
                .WithBatchCharacterCount(2)
                .WithStartBatchIndex(0)
                .WithEndBatchIndex(1)
                .WithThreadCount(threadCount)
                .WithQuietMode(true);

            using var search = settings.Start();
            await search.WaitForCompletionAsync();

            output.WriteLine(
                $"threads={threadCount}: searched={search.TotalSeedsSearched}, matched={search.MatchingSeeds}"
            );

            Assert.Equal(baselineMatches, search.MatchingSeeds);
        }
    }

    /// <summary>
    /// Callback should capture seed strings for every match.
    /// We run a list search with a set of fake seeds and verify
    /// that the callback fires exactly for the matched seeds.
    /// </summary>
    [Fact]
    public async Task Callback_FiresForMatchedSeeds()
    {
        Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var config, out _));

        var capturedSeeds = new ConcurrentBag<string>();

        // Use sequential search on a tiny batch to find some seeds
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2)
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithSeedMatchCallback(line =>
            {
                // Parse seed from CSV line: SEED,SCORE,...
                var comma = line.IndexOf(',');
                if (comma > 0)
                    capturedSeeds.Add(line[..comma]);
                else
                    capturedSeeds.Add(line.Trim());
            });

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine($"Matched: {search.MatchingSeeds}, Captured: {capturedSeeds.Count}");

        // The callback should fire for every match
        Assert.Equal(search.MatchingSeeds, capturedSeeds.Count);
    }

    /// <summary>
    /// Verifies the JAML parser + search builder pipeline doesn't throw
    /// on a complex multi-clause filter.
    /// </summary>
    [Fact]
    public async Task ComplexFilter_RunsWithoutErrors()
    {
        var jaml = """
            name: Complex
            deck: Anaglyph
            stake: White
            must:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
              - legendaryJoker: Perkeo
                antes: [1,2,3,4,5,6]
                sources:
                  boosterPacks: [0,1,2,3,4,5]
            should:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
                score: 100
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2)
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine(
            $"ComplexFilter: searched={search.TotalSeedsSearched}, matched={search.MatchingSeeds}"
        );

        // Just verify it completes without exceptions and searches some seeds
        Assert.True(search.TotalSeedsSearched > 0, "Should have searched some seeds");
        Assert.True(search.IsCompleted, "Search should be completed");
    }

    [Fact]
    public async Task AnalyzerDerivedUncommonShopItemFilter_MatchesSameSeed()
    {
        var match = FindAnalyzedUncommonShopItem();
        Assert.True(match.HasValue, "Expected to find at least one analyzed uncommon shop joker");

        var derived = match!.Value;
        var jaml = $$"""
            name: AnalyzerDerivedUncommonShopItem
            deck: Red
            stake: White
            must:
              - uncommonJoker: {{derived.JokerName}}
                antes: [{{derived.Ante}}]
                sources:
                  shopItems: [{{derived.ShopSlotIndex}}]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine(
            $"Analyzer-derived uncommon shop item test: seed={derived.Seed}, ante={derived.Ante}, joker={derived.JokerName}, shopSlot={derived.ShopSlotIndex}, matched={search.MatchingSeeds}"
        );

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedTarotShopItemFilter_MatchesSameSeed()
    {
        var match = FindAnalyzedTarotShopItem();
        Assert.True(match.HasValue, "Expected to find at least one analyzed tarot shop item");

        var derived = match!.Value;
        var jaml = $$"""
            name: AnalyzerDerivedTarotShopItem
            deck: Red
            stake: White
            must:
              - tarot: {{derived.TarotName}}
                antes: [{{derived.Ante}}]
                sources:
                  shopItems: [{{derived.ShopSlotIndex}}]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine(
            $"Analyzer-derived tarot shop item test: seed={derived.Seed}, ante={derived.Ante}, tarot={derived.TarotName}, shopSlot={derived.ShopSlotIndex}, matched={search.MatchingSeeds}"
        );

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedTagShorthand_MatchesEitherBlind()
    {
        var match = FindAnalyzedAnteWithDistinctTags();
        Assert.True(match.HasValue, "Expected to find at least one analyzed ante with distinct blind tags");

        var derived = match!.Value;
        var jaml = $$"""
            name: AnalyzerDerivedAnyTag
            deck: Red
            stake: White
            must:
              - tag: {{derived.BigBlindTag}}
                antes: [{{derived.Ante}}]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedLogicalAndFilter_MatchesSameSeed()
    {
        var match = FindAnalyzedAnteWithDistinctTags();
        Assert.True(match.HasValue, "Expected to find at least one analyzed ante with distinct blind tags");

        var derived = match!.Value;
        var jaml = $$"""
            name: AnalyzerDerivedAnd
            deck: Red
            stake: White
            must:
              - and:
                  - smallBlindTag: {{derived.SmallBlindTag}}
                    antes: [{{derived.Ante}}]
                  - bigBlindTag: {{derived.BigBlindTag}}
                    antes: [{{derived.Ante}}]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedLogicalOrFilter_MatchesSameSeed()
    {
        var match = FindAnalyzedAnteWithDistinctTags();
        Assert.True(match.HasValue, "Expected to find at least one analyzed ante with distinct blind tags");

        var derived = match!.Value;
        var alternateTag = Enum
            .GetValues<MotelyTag>()
            .First(tag =>
                !string.Equals(tag.ToString(), derived.SmallBlindTag, StringComparison.Ordinal)
                && !string.Equals(tag.ToString(), derived.BigBlindTag, StringComparison.Ordinal)
            )
            .ToString();

        var jaml = $$"""
            name: AnalyzerDerivedOr
            deck: Red
            stake: White
            must:
              - or:
                  - smallBlindTag: {{alternateTag}}
                    antes: [{{derived.Ante}}]
                  - bigBlindTag: {{derived.BigBlindTag}}
                    antes: [{{derived.Ante}}]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedNestedAndFilter_MatchesSameSeed()
    {
        var match = FindAnalyzedAnteWithDistinctTags();
        Assert.True(match.HasValue, "Expected to find at least one analyzed ante with distinct blind tags");

        var derived = match!.Value;
        var jaml = $$"""
            name: AnalyzerDerivedNestedAnd
            deck: Red
            stake: White
            must:
              - label: Exact blind pair
                mode: sum
                score: 100
                and:
                  - smallBlindTag: {{derived.SmallBlindTag}}
                    antes: [{{derived.Ante}}]
                  - bigBlindTag: {{derived.BigBlindTag}}
                    antes: [{{derived.Ante}}]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedTagMinFilter_MatchesSameSeed()
    {
        var match = FindAnalyzedRepeatedAnyTag(2);
        Assert.True(match.HasValue, "Expected to find at least one analyzed seed with repeated tag occurrences");

        var derived = match!.Value;
        var antes = string.Join(", ", derived.Antes);
        var jaml = $$"""
            name: AnalyzerDerivedTagMin
            deck: Red
            stake: White
            must:
              - tag: {{derived.TagName}}
                antes: [{{antes}}]
                min: 2
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedTagMinFilter_RejectsSingleOccurrence()
    {
        var match = FindAnalyzedSingleTagOccurrence();
        Assert.True(match.HasValue, "Expected to find at least one analyzed seed with a tag occurrence");

        var derived = match!.Value;
        var jaml = $$"""
            name: AnalyzerDerivedTagMinReject
            deck: Red
            stake: White
            must:
              - tag: {{derived.TagName}}
                antes: [{{derived.Ante}}]
                min: 2
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(0, search.MatchingSeeds);
    }

    [Fact]
    public async Task AnalyzerDerivedVoucherMinFilter_RejectsSingleOccurrence()
    {
        var match = FindAnalyzedSingleVoucherOccurrence();
        Assert.True(match.HasValue, "Expected to find at least one analyzed seed with a voucher occurrence");

        var derived = match!.Value;
        var jaml = $$"""
            name: AnalyzerDerivedVoucherMinReject
            deck: Red
            stake: White
            must:
              - voucher: {{derived.VoucherName}}
                antes: [{{derived.Ante}}]
                min: 2
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([derived.Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(0, search.MatchingSeeds);
    }

    [Fact]
    public void Analyzer_FirstAnteFirstPack_IsNormalBuffoon()
    {
        foreach (var seed in AnalyzerSeedCandidates)
        {
            var analysis = MotelySeedAnalyzer.Analyze(
                new MotelySeedAnalysisConfig(seed, MotelyDeck.Red, MotelyStake.White)
            );

            Assert.True(string.IsNullOrEmpty(analysis.Error), $"Analyzer failed for {seed}: {analysis.Error}");
            Assert.NotEmpty(analysis.Antes);

            var ante = analysis.Antes[0];
            Assert.Equal(1, ante.Ante);
            Assert.NotEmpty(ante.Packs);

            var pack = ante.Packs[0];
            Assert.Equal(MotelyBoosterPack.Buffoon, pack.Type);
            Assert.Equal(MotelyBoosterPackType.Buffoon, pack.Type.GetPackType());
            Assert.Equal(MotelyBoosterPackSize.Normal, pack.Type.GetPackSize());
            Assert.Equal(2, pack.Items.Count);
        }
    }
}

