namespace Motely.Tests;

public sealed class CoverageUtilityTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("1", 1)]
    [InlineData("9", 9)]
    [InlineData("A", 10)]
    [InlineData("Z", 35)]
    [InlineData("11", 36)]
    [InlineData("11111111", 66231629136)]
    public void SeedMath_TotalIndexRoundTrips(string seed, long index)
    {
        Assert.Equal(index, SeedMath.SeedToTotalIndex(seed));
        Assert.Equal(seed, SeedMath.TotalIndexToSeed(index));
    }

    [Theory]
    [InlineData("11111111", 0)]
    [InlineData("11111112", 1)]
    [InlineData("1111111Z", 34)]
    [InlineData("11111121", 35)]
    public void SeedMath_SearchIndexRoundTrips(string seed, long index)
    {
        Assert.Equal(index, SeedMath.SeedToSearchIndex(seed));
        Assert.Equal(seed, SeedMath.SearchIndexToSeed(index, seed.Length));
    }

    [Fact]
    public void SeedMath_BatchAndRangeHelpersUseInclusiveSearchIndices()
    {
        Assert.Equal(0, SeedMath.GetFirstSeedOfLength(0));
        Assert.Equal(1, SeedMath.GetFirstSeedOfLength(1));
        Assert.Equal(36, SeedMath.GetFirstSeedOfLength(2));
        Assert.Equal(35 * 35 - 1, SeedMath.MaxSearchIndexInclusive(2));

        Assert.Equal(0, SeedMath.SeedToBatchIndex("11111111", 3));
        Assert.Equal("11111", SeedMath.BatchIndexToSeedPrefix(0, 3));

        // One batch is the 35 seeds that share a suffix and vary the FIRST batchCharCount chars —
        // "11111111".."Z1111111" at batchCharCount 1, i.e. search indices 0 .. 34*35^7. Not
        // 0..34: those differ in the LAST char, which is part of the batch id, so they scatter
        // across 35 batches at opposite ends of the space. Search-index order is lexicographic;
        // batch order is the engine's, and the two are mirror images of each other.
        long lastFirstCharSeed = 34 * (long)Math.Pow(35, 7);
        var oneBatch = SeedMath.SearchIndexRangeToBatchRange(0, lastFirstCharSeed, 1);
        Assert.Equal(0, oneBatch.StartBatchIndex);
        Assert.Equal(1, oneBatch.EndBatchIndexExclusive);

        // And a lexicographic 35-seed run really does span the whole space, because its last
        // character is the most significant batch digit.
        var scattered = SeedMath.SearchIndexRangeToBatchRange(0, 34, 1);
        Assert.Equal(0, scattered.StartBatchIndex);
        Assert.Equal(34 * (long)Math.Pow(35, 6) + 1, scattered.EndBatchIndexExclusive);
    }

    [Fact]
    public void SeedMath_BatchIndexToMinSeed_RoundTripsThroughSeedToBatchIndex()
    {
        // The resume hint printed by --startBatch/--startSeed must land back on the same batch.
        // It used to emit the seed reversed AND with the halves swapped: batch 711,205 at
        // batchCharCount 1 printed "111HLL61" instead of "16LLH111", so resuming a stopped
        // sweep restarted somewhere unrelated.
        Assert.Equal("16LLH111", SeedMath.BatchIndexToMinSeed(711205, 1));

        for (int n = 1; n <= 7; n++)
        {
            long maxBatch = SeedMath.MaxSearchIndexInclusive(8 - n);
            foreach (long b in new[] { 0L, 1L, 35L, 711205L, maxBatch })
            {
                if (b > maxBatch)
                    continue;
                string seed = SeedMath.BatchIndexToMinSeed(b, n);
                Assert.Equal(MotelyGlobals.MaxSeedLength, seed.Length);
                Assert.Equal(b, SeedMath.SeedToBatchIndex(seed, n));
            }
        }
    }

    [Fact]
    public void ResumeBatchIndex_IsTheLowestUnfinishedBatch_NotTheCompletedCount()
    {
        // Batches dispatch in order and finish out of order. Resuming at the completed *count*
        // skips whatever a killed thread was holding: 16 threads on batches 0-15, the one on
        // batch 4 dies, the other 15 finish → count 15 → batch 4 never searched.
        static long Resume(long[] inFlight, long lastDispatched, long maxBatch) =>
            MotelySearch<PassthroughFilterDesc.PassthroughFilter>.ComputeResumeBatchIndex(
                inFlight,
                lastDispatched,
                maxBatch
            );

        long[] idle = [-1, -1, -1, -1];

        // Nothing in flight: everything dispatched is finished, so resume one past it.
        Assert.Equal(16, Resume(idle, 15, 1000));

        // Before the first batch, _batchIndex sits at startBatch - 1 and this must be startBatch.
        Assert.Equal(0, Resume(idle, -1, 1000));
        Assert.Equal(500, Resume(idle, 499, 1000));

        // The killed-thread case: 15 finished above it, one abandoned batch 4.
        Assert.Equal(4, Resume([-1, -1, 4, -1], 15, 1000));

        // Every thread parked → the lowest of them, regardless of dispatch order.
        Assert.Equal(7, Resume([12, 7, 15, 9], 15, 1000));

        // A bounded run (--endBatch, or a DistributedWorker block) leaves _batchIndex up to
        // threadCount-1 past the end; the hint must never point outside the range.
        Assert.Equal(64, Resume(idle, 70, 64));
        Assert.Equal(0, Resume([], -1, 64));
    }

    [Fact]
    public void SeedMath_RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentException>(() => SeedMath.SeedToTotalIndex("10"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SeedMath.TotalIndexToSeed(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SeedMath.SearchIndexRangeToBatchRange(0, 1, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SeedMath.SearchIndexRangeToBatchRange(2, 1, 1)
        );
    }

    [Fact]
    public void KeywordSequences_BasicGeneratorsAreLazyAndDeterministic()
    {
        Assert.Equal("AAA", MotelySeedKeywordSequences.RepeatCharKeywords(3).First());
        Assert.Equal("ZZZ", MotelySeedKeywordSequences.RepeatCharKeywords(3).Last());
        Assert.Equal(26, MotelySeedKeywordSequences.RepeatCharKeywords(3).Count());

        Assert.Equal("1234", MotelySeedKeywordSequences.AscendingDigitLetterKeywords(4).First());
        Assert.Equal("WXYZ", MotelySeedKeywordSequences.AscendingDigitLetterKeywords(4).Last());
        Assert.Equal("ZYXW", MotelySeedKeywordSequences.DescendingDigitLetterKeywords(4).First());
        Assert.Equal("4321", MotelySeedKeywordSequences.DescendingDigitLetterKeywords(4).Last());

        var mirrors = MotelySeedKeywordSequences.MirrorPatternKeywords(2).ToArray();
        Assert.Equal(13 * 13, mirrors.Length);
        Assert.Contains("AA", mirrors);
        Assert.Contains("88", mirrors);
    }

    [Fact]
    public void KeywordSequences_AestheticCountsAndValidationArePinned()
    {
        // Each baked constant must be recomputed from its own keyword array. Comparing the
        // constant to GetAestheticSeedCount(aesthetic) is a tautology for the null-pad case —
        // that call returns the constant — so the live count is the only real pin.
        var baked = string.Join(
            " ",
            MotelySeedKeywordSequences.GrossKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.FunnyKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.BalatroKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.LeetKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.NsfwKeywordAestheticSeedCount
        );
        var live = string.Join(
            " ",
            MotelyGlobals.GetPaddedSeedCountForKeywordsLong(
                MotelySeedKeywordSequences.GrossKeywords
            ),
            MotelyGlobals.GetPaddedSeedCountForKeywordsLong(
                MotelySeedKeywordSequences.FunnyKeywords
            ),
            MotelyGlobals.GetPaddedSeedCountForKeywordsLong(
                MotelySeedKeywordSequences.BalatroKeywords
            ),
            MotelyGlobals.GetPaddedSeedCountForKeywordsLong(MotelySeedKeywordSequences.LeetKeywords),
            MotelyGlobals.GetPaddedSeedCountForKeywordsLong(MotelySeedKeywordSequences.NsfwKeywords)
        );
        Assert.Equal(baked, live);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MotelySeedKeywordSequences.GetAestheticSeedCount(JamlAesthetic.Palindrome)
        );

        foreach (
            var keywords in new[]
            {
                MotelySeedKeywordSequences.GrossKeywords,
                MotelySeedKeywordSequences.FunnyKeywords,
                MotelySeedKeywordSequences.BalatroKeywords,
                MotelySeedKeywordSequences.NsfwKeywords,
            }
        )
        {
            Assert.NotEmpty(keywords);
            Assert.All(
                keywords,
                keyword =>
                {
                    Assert.InRange(keyword.Length, 1, 8);
                    Assert.All(
                        keyword.ToUpperInvariant(),
                        c => Assert.Contains(c, MotelyGlobals.SeedDigits)
                    );
                }
            );
        }
    }

    [Fact]
    public void NativeFilterNames_ParseEveryDisplayNameAndFactoryCreatesSettings()
    {
        Assert.Equal(
            Enum.GetValues<MotelyNativeFilter>().Length,
            MotelyNativeFilterNames.DisplayNames.Length
        );

        foreach (var expected in Enum.GetValues<MotelyNativeFilter>())
        {
            var name = MotelyNativeFilterNames.DisplayNames[(int)expected];
            Assert.True(MotelyNativeFilterNames.TryParse(name, out var parsed));
            Assert.Equal(expected, parsed);
            Assert.NotNull(MotelyNativeFilterFactory.CreateSettings(parsed));
        }
    }

    [Fact]
    public void NativeFilterNames_RejectUnknownAndFactoryRejectsOutOfRange()
    {
        Assert.False(MotelyNativeFilterNames.TryParse("not-a-filter", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MotelyNativeFilterFactory.CreateSettings((MotelyNativeFilter)999)
        );
    }
}
