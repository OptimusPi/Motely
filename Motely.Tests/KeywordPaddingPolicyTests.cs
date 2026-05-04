using System.Linq;
using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class KeywordPaddingPolicyTests
{
    private static readonly char[] ExplicitPadding = ['A', 'B', 'C'];

    [Fact]
    public void GetPaddedSeedCountForKeywords_RejectsLength2Keyword_WithoutExplicitPadding()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            GetPaddedSeedCountForKeywords(new[] { "AB" }, validChars: null)
        );
        Assert.Contains("AB", ex.Message, StringComparison.Ordinal);
        Assert.Contains("padding", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPaddedSeedCountForKeywords_RejectsLength1Keyword_WithoutExplicitPadding()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            GetPaddedSeedCountForKeywords(new[] { "X" }, validChars: null)
        );
        Assert.Contains("X", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPaddedSeedCountForKeywords_AllowsLength2Keyword_WithExplicitPadding()
    {
        ulong n = GetPaddedSeedCountForKeywords(new[] { "AB" }, ExplicitPadding);
        Assert.True(n > 0);
    }

    [Fact]
    public void GetPaddedSeedCountForKeywords_AllowsLength3PlusKeyword_WithoutExplicitPadding()
    {
        ulong n = GetPaddedSeedCountForKeywords(new[] { "CAT" }, validChars: null);
        Assert.True(n > 0);
    }

    [Fact]
    public void GetPaddedSeedCountForKeywordsLong_MatchesLongFromUlongPath()
    {
        long fromLong = GetPaddedSeedCountForKeywordsLong(new[] { "CAT" }, null);
        ulong fromUlong = GetPaddedSeedCountForKeywords(new[] { "CAT" }, null);
        Assert.Equal((long)fromUlong, fromLong);
    }

    [Fact]
    public void GeneratePaddedSeedsForKeywords_ThrowsOnFirstEnumeration_WhenShortKeywordNoPadding()
    {
        var seq = GeneratePaddedSeedsForKeywords(new[] { "ZZ" }, null);
        Assert.Throws<ArgumentException>(() => seq.GetEnumerator().MoveNext());
    }

    [Fact]
    public void GeneratePaddedSeedsForKeywords_AllowsShortKeyword_WithExplicitPadding()
    {
        var list = GeneratePaddedSeedsForKeywords(new[] { "ZZ" }, ExplicitPadding).Take(5).ToList();
        Assert.NotEmpty(list);
    }

    [Fact]
    public void MotelyKeywordSeedProvider_Throws_ForShortKeywordWithoutPadding()
    {
        Assert.Throws<ArgumentException>(() => _ = new MotelyKeywordSeedProvider(new[] { "NO" }, null));
    }

    [Fact]
    public void MotelyKeywordSeedProvider_Succeeds_ForShortKeywordWithExplicitPadding()
    {
        var provider = new MotelyKeywordSeedProvider(new[] { "NO" }, ExplicitPadding);
        Assert.True(provider.SeedCount > 0);
    }

    [Fact]
    public void BakedKeywordAestheticSeedCounts_Match_GetPaddedSeedCountForKeywordsLong()
    {
        Assert.Equal(
            MotelySeedKeywordSequences.GrossKeywordAestheticSeedCount,
            GetPaddedSeedCountForKeywordsLong(MotelySeedKeywordSequences.GrossKeywords)
        );
        Assert.Equal(
            MotelySeedKeywordSequences.NsfwKeywordAestheticSeedCount,
            GetPaddedSeedCountForKeywordsLong(MotelySeedKeywordSequences.NsfwKeywords)
        );
        Assert.Equal(
            MotelySeedKeywordSequences.FunnyKeywordAestheticSeedCount,
            GetPaddedSeedCountForKeywordsLong(MotelySeedKeywordSequences.FunnyKeywords)
        );
        Assert.Equal(
            MotelySeedKeywordSequences.BalatroKeywordAestheticSeedCount,
            GetPaddedSeedCountForKeywordsLong(MotelySeedKeywordSequences.BalatroKeywords)
        );
    }

    [Fact]
    public void JamlAesthetics_GetSeedCount_Uses_BakedKeywordTotals()
    {
        Assert.Equal(MotelySeedKeywordSequences.GrossKeywordAestheticSeedCount, JamlAesthetics.GetSeedCount(JamlAesthetic.Gross));
        Assert.Equal(MotelySeedKeywordSequences.NsfwKeywordAestheticSeedCount, JamlAesthetics.GetSeedCount(JamlAesthetic.Nsfw));
        Assert.Equal(MotelySeedKeywordSequences.FunnyKeywordAestheticSeedCount, JamlAesthetics.GetSeedCount(JamlAesthetic.Funny));
        Assert.Equal(MotelySeedKeywordSequences.BalatroKeywordAestheticSeedCount, JamlAesthetics.GetSeedCount(JamlAesthetic.Balatro));
    }
}
