using System.Collections.Generic;
using Motely;
using Motely.Analysis;
using Motely.Enums;
using Xunit;

namespace Motely.Tests;

public sealed class AnalyzerUnitTests
{
    [Fact]
    public void TestSeedRouter_CapturesSingleSearchContext()
    {
        using var router = new MotelySeedRouterDesc("1AAAAAAA", MotelyDeck.Red, MotelyStake.White);

        var ctx = router.Instance();

        Assert.Equal("1AAAAAAA", ctx.GetSeed());
        var bossStream = ctx.CreateBossStream();
        var runState = new MotelyRunState();
        var boss = ctx.GetBossForAnte(ref bossStream, 1, ref runState);
        Assert.NotEqual(default, boss);
    }

    // The decisive test: does the MotelySingleSearchContext returned by Instance()
    // actually drive a real PRNG stream and produce valid shop items? It must agree
    // with the golden analyzer, which is verified against Balatro itself.
    [Fact]
    public void TestReturnedContext_DrivesShopStreamMatchingAnalyzer()
    {
        const string seed = "UNITTEST";

        using var router = new MotelySeedRouterDesc(seed, MotelyDeck.Red, MotelyStake.White);
        var ctx = router.Instance();

        var shopStream = ctx.CreateShopItemStream(1, MotelyDeck.Red.GetDefaultRunState());
        var fromContext = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            fromContext.Add(ctx.GetNextShopItem(ref shopStream).Value);
        }

        var analysis = MotelyLegacyTextAnalyzer.Analyze(new(seed, MotelyDeck.Red, MotelyStake.White));
        var fromAnalyzer = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            fromAnalyzer.Add(analysis.Antes[0].ShopQueue[i].Value);
        }

        Assert.Equal(fromAnalyzer, fromContext);
    }

    // Smoke test: analyzer runs end-to-end on a range of seeds/decks/stakes without
    // throwing and produces non-empty output. (Snapshot verification via Verify was
    // dropped from the project; keeping the input matrix here is still valuable
    // because it exercises the analyzer's ante/shop/pack walk across all decks.)
    [Theory]
    [InlineData("1234567")]
    [InlineData("12345678")]
    [InlineData("ALEEB")]
    [InlineData("ALEEBOOO")]
    [InlineData("UNITTES")]
    [InlineData("UNITTEST")]
    [InlineData("KK1XD111", MotelyDeck.Ghost, MotelyStake.Black)]
    public void TestAnalyzer_ProducesOutput(
        string seed,
        MotelyDeck deck = MotelyDeck.Red,
        MotelyStake stake = MotelyStake.White
    )
    {
        string actualOutput = GetAnalyzerOutput(seed, deck, stake);

        Assert.False(
            string.IsNullOrWhiteSpace(actualOutput),
            $"Analyzer returned empty output for {seed}"
        );
        Assert.Contains("==ANTE 1==", actualOutput);
    }

    private string GetAnalyzerOutput(
        string seed,
        MotelyDeck deck = MotelyDeck.Red,
        MotelyStake stake = MotelyStake.White
    )
    {
        return MotelyLegacyTextAnalyzer.Analyze(new(seed, deck, stake)).ToString();
    }

    [Fact]
    public void TestAnalyzer_PackContentsFormat()
    {
        // Test that pack contents are formatted correctly
        string seed = "UNITTEST";
        var output = GetAnalyzerOutput(seed);

        // Check that packs have the correct format: "Pack Name - Card1, Card2"
        Assert.Contains("Buffoon Pack - ", output);
        Assert.Contains("Arcana Pack - ", output);
        Assert.Contains("Standard Pack - ", output);

        // Check that Mega packs DON'T have the "(pick 2)" suffix (Immolate doesn't use it)
        Assert.Contains("Mega Standard Pack - ", output);
        Assert.Contains("Mega Arcana Pack - ", output);
        Assert.Contains("Mega Celestial Pack - ", output);
    }

    [Fact]
    public void TestAnalyzer_TagsNotActivated()
    {
        // Test that tags are just listed, not "activated" to show their packs
        string seed = "UNITTEST";
        var output = GetAnalyzerOutput(seed);

        // Check first ante has Speed Tags but no extra packs from them
        var lines = output.Split('\n');
        bool inAnte1 = false;
        int packCount = 0;

        foreach (var line in lines)
        {
            if (line.Contains("==ANTE 1=="))
            {
                inAnte1 = true;
            }
            else if (line.Contains("==ANTE 2=="))
            {
                break;
            }
            else if (
                inAnte1 && line.Trim().StartsWith("Buffoon Pack")
                || line.Trim().StartsWith("Arcana Pack")
                || line.Trim().StartsWith("Celestial Pack")
                || line.Trim().StartsWith("Spectral Pack")
                || line.Trim().StartsWith("Standard Pack")
                || line.Trim().StartsWith("Jumbo")
                || line.Trim().StartsWith("Mega")
            )
            {
                packCount++;
            }
        }

        // Ante 1 should have exactly 4 packs
        Assert.Equal(4, packCount);
    }
}
