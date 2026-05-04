using Motely.Analysis;

namespace Motely.Tests;

public sealed class JamlyzerUnitTests
{
    private const string AnyAnteOneJokerJaml = """
        name: jamlyzer-smoke
        deck: Red
        stake: White
        must:
          - joker: Any
            antes: [1]
        """;

    private const string AnyAnteOneJokerWithSeedsJaml = """
        name: jamlyzer-smoke
        deck: Red
        stake: White
        seeds: [JAMMY, GPT55YA]
        must:
          - joker: Any
            antes: [1]
        """;

    [Fact]
    public void AnalyzeSeed_AppliesJamlBeforeAttachingAnalysis()
    {
        var result = MotelyJamlyzer.AnalyzeSeed(new("JAMMY", AnyAnteOneJokerJaml));

        Assert.Null(result.Error);
        Assert.Equal(MotelyDeck.Red, result.Deck);
        Assert.Equal(MotelyStake.White, result.Stake);
        var seed = Assert.Single(result.Seeds);
        Assert.Equal("JAMMY", seed.Seed);
        Assert.NotNull(seed.Analysis);
        Assert.Equal("JAMMY", seed.Analysis!.Seed);
        Assert.Equal("Red", seed.Analysis.Deck);
        Assert.Equal("White", seed.Analysis.Stake);
        Assert.Contains(seed.Analysis.Antes, ante => ante.Ante == 1);
    }

    [Fact]
    public void Analyze_ReturnsPageMetadata()
    {
        var result = MotelyJamlyzer.Analyze(
            new(
                AnyAnteOneJokerJaml,
                StartBatch: 0,
                EndBatch: 1,
                BatchCharacterCount: 1,
                IncludeSeedAnalysis: false
            )
        );

        Assert.Null(result.Error);
        Assert.True(result.TotalSeedsSearched > 0);
        Assert.Equal(1, result.CompletedBatchCount);
    }

    [Fact]
    public void AnalyzeSeeds_UsesTopLevelJamlSeedsWhenNoExplicitSeedsProvided()
    {
        var result = MotelyJamlyzer.AnalyzeSeeds(new(AnyAnteOneJokerWithSeedsJaml));

        Assert.Null(result.Error);
        Assert.Equal(2, result.TotalSeedsSearched);
        Assert.Equal(2, result.Seeds.Count);
        Assert.Contains(result.Seeds, seed => seed.Seed == "JAMMY");
        Assert.Contains(result.Seeds, seed => seed.Seed == "GPT55YA");
    }

    [Fact]
    public void AnalyzeSeed_MarksJamlMatchedItemsForPreviewCards()
    {
        var result = MotelyJamlyzer.AnalyzeSeed(new("ALEEB", AnyAnteOneJokerJaml));

        Assert.Null(result.Error);
        var seed = Assert.Single(result.Seeds);
        Assert.NotNull(seed.Analysis);
        var analysis = seed.Analysis!;
        var anteOne = Assert.Single(analysis.Antes, ante => ante.Ante == 1);

        Assert.True(
            anteOne.ShopQueue.Any(item => item.Matched)
                || anteOne.Packs.Any(pack => pack.Items.Any(item => item.Matched)),
            "Jamlyzer should mark at least one inspected ante-1 joker source item as matched."
        );
    }
}
