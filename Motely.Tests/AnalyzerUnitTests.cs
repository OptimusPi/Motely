using Motely.Analysis;

namespace Motely.Tests;

public sealed class AnalyzerUnitTests
{

    [Theory]
    [InlineData("1234567")]
    [InlineData("12345678")]
    [InlineData("ALEEB")]
    [InlineData("ALEEBOOO")]
    [InlineData("UNITTES")]
    [InlineData("UNITTEST")]
    [InlineData("KK1XD111", MotelyDeck.Ghost, MotelyStake.Black)]
    public async Task TestAnalyzer(string seed, MotelyDeck deck = MotelyDeck.Red, MotelyStake stake = MotelyStake.White)
    {
        string actualOutput = GetAnalyzerOutput(seed, deck, stake);

        // Assert using Verify - this will create a nice diff view
        await Verify(actualOutput)
            .UseFileName(seed)
            .UseDirectory("seeds");
    }

    private string GetAnalyzerOutput(string seed, MotelyDeck deck = MotelyDeck.Red, MotelyStake stake = MotelyStake.White)
    {
        return MotelySeedAnalyzer.Analyze(new(seed, deck, stake)).ToString();
    }

    // This method is now only used by other tests that don't use Verify yet
    private void AssertOutputsMatch(string expected, string actual, string seed)
    {
        Assert.Equal(expected, actual);
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
            else if (inAnte1 && line.Trim().StartsWith("Buffoon Pack") ||
                     line.Trim().StartsWith("Arcana Pack") ||
                     line.Trim().StartsWith("Celestial Pack") ||
                     line.Trim().StartsWith("Spectral Pack") ||
                     line.Trim().StartsWith("Standard Pack") ||
                     line.Trim().StartsWith("Jumbo") ||
                     line.Trim().StartsWith("Mega"))
            {
                packCount++;
            }
        }

        // Ante 1 should have exactly 4 packs
        Assert.Equal(4, packCount);
    }
}