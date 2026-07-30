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

        var range = SeedMath.SearchIndexRangeToBatchRange(0, 34, 1);
        Assert.Equal(0, range.StartBatchIndex);
        Assert.Equal(1, range.EndBatchIndexExclusive);
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
        Assert.Equal(
            MotelySeedKeywordSequences.GrossKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.GetAestheticSeedCount(JamlAesthetic.Gross)
        );
        Assert.Equal(
            MotelySeedKeywordSequences.FunnyKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.GetAestheticSeedCount(JamlAesthetic.Funny)
        );
        Assert.Equal(
            MotelySeedKeywordSequences.BalatroKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.GetAestheticSeedCount(JamlAesthetic.Balatro)
        );
        Assert.Equal(
            MotelySeedKeywordSequences.NsfwKeywordAestheticSeedCount,
            MotelySeedKeywordSequences.GetAestheticSeedCount(JamlAesthetic.Nsfw)
        );
        Assert.Equal(
            MotelySeedKeywordSequences.NsfwKeywordAestheticSeedCount,
            MotelyGlobals.GetPaddedSeedCountForKeywordsLong(MotelySeedKeywordSequences.NsfwKeywords)
        );
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
