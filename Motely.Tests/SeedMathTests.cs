using Xunit;

namespace Motely.Tests;

public class SeedMathTests
{
    [Fact]
    public void MaxSearchIndexInclusive_Length8_MatchesPow35()
    {
        long expected = 1;
        for (int i = 0; i < 8; i++)
            expected *= 35;
        Assert.Equal(expected - 1, SeedMath.MaxSearchIndexInclusive(8));
    }

    [Fact]
    public void SearchIndexRangeToBatchRange_FullSpace_CoversAllBatches_ForBatchChar2()
    {
        long max = SeedMath.MaxSearchIndexInclusive(8);
        int k = 2;
        long nonBatch = 8 - k;
        long expectedBatches = 1;
        for (int i = 0; i < nonBatch; i++)
            expectedBatches *= 35;

        var (start, endExclusive) = SeedMath.SearchIndexRangeToBatchRange(0, max, k);
        Assert.Equal(0, start);
        Assert.Equal(expectedBatches, endExclusive);
    }

    [Fact]
    public void SearchIndexRangeToBatchRange_SingleIndex_SingleBatchSpan()
    {
        var (sb, eb) = SeedMath.SearchIndexRangeToBatchRange(0, 0, 4);
        Assert.Equal(sb, SeedMath.SeedToBatchIndex(SeedMath.SearchIndexToSeed(0, 8), 4));
        Assert.Equal(sb + 1, eb);
    }

    [Fact]
    public void BatchPrefixPlusMinSuffix_MapsBackToSameBatch()
    {
        const int k = 3;
        long b = 42;
        string prefix = SeedMath.BatchIndexToSeedPrefix(b, k);
        string minSeed = prefix + new string('1', k);
        Assert.Equal(8, minSeed.Length);
        Assert.Equal(b, SeedMath.SeedToBatchIndex(minSeed, k));
    }
}
