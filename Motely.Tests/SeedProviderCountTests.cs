using System.Linq;
using Xunit;

namespace Motely.Tests;

public class SeedProviderCountTests
{
    [Theory]
    [InlineData("A", 0, 1)]
    [InlineData("A", 1, 6)]
    [InlineData("AB", 2, 27)]
    [InlineData("SHOW", 3, 108)]
    [InlineData("HI", 4, 243)]
    public void GetPaddedSeedCount_MatchesExpectedCount(string keyword, int padLen, int expected)
    {
        char[] validChars = ['A', 'B', 'C'];
        int actual = MotelyCore.GetPaddedSeedCount(keyword, padLen, validChars);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PalindromeSeedProvider_ReportsExactSeedCount()
    {
        var provider = new MotelyPalindromeSeedProvider();
        var buffer = new string[MotelyCore.MaxVectorWidth];
        int total = 0;

        while (true)
        {
            int pulled = provider.NextSeeds(buffer);
            if (pulled == 0)
                break;

            total += pulled;
        }

        Assert.Equal(total, provider.SeedCount);
    }

    [Fact]
    public void SeedListProvider_InfersCollectionCount()
    {
        string[] seeds = ["ABCD1234", "BCDE2345", "CDEF3456"];
        var provider = new MotelySeedListProvider(seeds);

        Assert.Equal(seeds.Length, provider.SeedCount);
    }
}
