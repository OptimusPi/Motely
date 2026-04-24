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
    // HI + padLen 4: (padLen + 1) = 5 keyword-contiguous slots × 3^4 = 405.
    // Old value (243) encoded the pre-fix bug where the recursive padLen>3 path
    // used (keyword.Length + 1) slots AND split the keyword across padding.
    [InlineData("HI", 4, 405)]
    public void GetPaddedSeedCount_MatchesExpectedCount(string keyword, int padLen, int expected)
    {
        char[] validChars = ['A', 'B', 'C'];
        ulong actual = GetPaddedSeedCount(keyword, padLen, validChars);

        Assert.Equal((ulong)expected, actual);
    }

    [Fact]
    public void GeneratePaddedSeeds_YieldsExpectedCount_ForPadLenGreaterThanThree()
    {
        char[] validChars = ['A', 'B', 'C'];
        ulong expected = GetPaddedSeedCount("JUST", 4, validChars);
        int actual = GeneratePaddedSeeds("JUST", 4, validChars).Count();

        Assert.Equal((int)expected, actual);
    }

    [Fact]
    public void PalindromeSeedProvider_ReportsExactSeedCount()
    {
        var provider = new MotelyPalindromeSeedProvider();
        var buffer = new string[MaxVectorWidth];
        int total = 0;

        while (true)
        {
            int pulled = provider.NextSeeds(buffer);
            if (pulled == 0)
                break;

            total += pulled;
        }

        Assert.Equal((long)total, provider.SeedCount);
    }

    [Fact]
    public void SeedListProvider_InfersCollectionCount()
    {
        string[] seeds = ["ABCD1234", "BCDE2345", "CDEF3456"];
        var provider = new MotelySeedListProvider(seeds);

        Assert.Equal((long)seeds.Length, provider.SeedCount);
    }
}
