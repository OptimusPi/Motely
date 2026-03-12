using System.Linq;
using Xunit;

namespace Motely.Tests;

public class SeedProviderCountTests
{
    [Theory]
    [InlineData("A", 0)]
    [InlineData("A", 1)]
    [InlineData("AB", 2)]
    [InlineData("SHOW", 3)]
    [InlineData("HI", 4)]
    public void GetPaddedSeedCount_MatchesGeneratedSequenceCount(string keyword, int padLen)
    {
        char[] validChars = ['A', 'B', 'C'];
        int expected = MotelyCore.GeneratePaddedSeeds(keyword, padLen, validChars).Count();
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

        Assert.Equal(total, provider.SeedCount.GetValueOrDefault(-1));
    }

    [Fact]
    public void SeedListProvider_InfersCollectionCount()
    {
        string[] seeds = ["ABCD1234", "BCDE2345", "CDEF3456"];
        var provider = new MotelySeedListProvider(seeds);

        Assert.Equal(seeds.Length, provider.SeedCount.GetValueOrDefault(-1));
    }
}
