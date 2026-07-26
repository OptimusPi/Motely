using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// The loader's value reader decides what a clause key is allowed to say. Its contract is
/// three-way and easy to blur: <c>true</c> means parsed, <c>false</c> means <em>absent</em>, and a
/// throw means <em>present but wrong</em>. Every case below pins which of the three applies.
///
/// The two search tests at the bottom are the R2 half: a value the reader accepts has to change
/// (or deliberately not change) a real match set, so "parses fine" cannot pass for "works".
/// </summary>
public sealed class JamlLoaderValueReaderTests
{
    // ── scalar / array construction ──

    [Fact]
    public void FromScalar_NullBecomesEmptyNotNull()
    {
        var reader = JamlLoaderValueReader.FromScalar(null);

        Assert.Equal("", reader.Text);
        Assert.False(reader.IsAny);
        Assert.False(reader.TryInt(out _));
        Assert.False(reader.TryIntArray(out var ints));
        Assert.Empty(ints);
    }

    [Fact]
    public void FromStrings_EmptyOrNullIsAbsent()
    {
        Assert.Equal("", JamlLoaderValueReader.FromStrings(null).Text);
        Assert.Equal("", JamlLoaderValueReader.FromStrings([]).Text);
        Assert.False(JamlLoaderValueReader.FromStrings([]).TryEnumArray<MotelyVoucher>(out var v));
        Assert.Empty(v);
    }

    [Fact]
    public void FromStrings_JoinsManyValuesForDisplay()
    {
        Assert.Equal("Overstock", JamlLoaderValueReader.FromStrings(["Overstock"]).Text);
        Assert.Equal(
            "Overstock, Grabber",
            JamlLoaderValueReader.FromStrings(["Overstock", "Grabber"]).Text
        );
    }

    [Theory]
    [InlineData("any", true)]
    [InlineData("ANY", true)]
    [InlineData("Any", true)]
    [InlineData("anything", false)]
    [InlineData("", false)]
    public void IsAny_IsCaseInsensitiveExactMatch(string text, bool expected) =>
        Assert.Equal(expected, JamlLoaderValueReader.FromScalar(text).IsAny);

    // ── ints ──

    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("-3", -3)]
    public void TryInt_ParsesIntegers(string text, int expected)
    {
        Assert.True(JamlLoaderValueReader.FromScalar(text).TryInt(out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void TryInt_AbsentOrUnparseableIsFalseNotThrow(string text)
    {
        Assert.False(JamlLoaderValueReader.FromScalar(text).TryInt(out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryIntArray_SingleScalarBecomesOneElement()
    {
        Assert.True(JamlLoaderValueReader.FromScalar("4").TryIntArray(out var value));
        Assert.Equal([4], value);
    }

    [Fact]
    public void TryIntArray_MultiElementArrayParsesEveryMember()
    {
        Assert.True(JamlLoaderValueReader.FromStrings(["1", "2", "3"]).TryIntArray(out var value));
        Assert.Equal([1, 2, 3], value);
    }

    /// <summary>Present-but-wrong throws — silently dropping a bad ante would be the bad outcome.</summary>
    [Fact]
    public void TryIntArray_UnparseableMemberThrows()
    {
        var reader = JamlLoaderValueReader.FromStrings(["1", "notanumber"]);

        var ex = Assert.Throws<InvalidOperationException>(() => reader.TryIntArray(out _));
        Assert.Contains("notanumber", ex.Message);
    }

    [Fact]
    public void TryIntArray_BlankIsAbsent()
    {
        Assert.False(JamlLoaderValueReader.FromScalar("   ").TryIntArray(out var value));
        Assert.Empty(value);
    }

    // ── bools ──

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("no", false)]
    [InlineData("No", false)]
    public void TryBool_AcceptsBoolAndYesNo(string text, bool expected)
    {
        Assert.True(JamlLoaderValueReader.FromScalar(text).TryBool(out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("maybe")]
    [InlineData("1")]
    public void TryBool_UnknownWordIsAbsent(string text)
    {
        Assert.False(JamlLoaderValueReader.FromScalar(text).TryBool(out var value));
        Assert.False(value);
    }

    // ── enums ──

    [Fact]
    public void TryEnum_BlankIsAbsent()
    {
        Assert.False(JamlLoaderValueReader.FromScalar("").TryEnum<MotelyVoucher>(out var value));
        Assert.Equal(default, value);
    }

    [Theory]
    [InlineData("Overstock")]
    [InlineData("overstock")]
    [InlineData("OVERSTOCK")]
    public void TryEnum_IsCaseInsensitive(string text)
    {
        Assert.True(JamlLoaderValueReader.FromScalar(text).TryEnum<MotelyVoucher>(out var value));
        Assert.Equal(MotelyVoucher.Overstock, value);
    }

    /// <summary>Spaces, hyphens and underscores are noise — the vocabulary is the engine enum.</summary>
    [Theory]
    [InlineData("Holo graphic")]
    [InlineData("Holo-graphic")]
    [InlineData("Holo_graphic")]
    public void TryEnum_NormalizesSeparators(string text)
    {
        Assert.True(
            JamlLoaderValueReader.FromScalar(text).TryEnum<MotelyItemEdition>(out var value)
        );
        Assert.Equal(MotelyItemEdition.Holographic, value);
    }

    [Fact]
    public void TryEnum_UnknownValueThrowsAndListsTheVocabulary()
    {
        var reader = JamlLoaderValueReader.FromScalar("NotAVoucher");

        var ex = Assert.Throws<InvalidOperationException>(
            () => reader.TryEnum<MotelyVoucher>(out _)
        );
        Assert.Contains("NotAVoucher", ex.Message);
        Assert.Contains(nameof(MotelyVoucher.Overstock), ex.Message);
    }

    [Fact]
    public void TryEnumArray_ParsesEveryMember()
    {
        Assert.True(
            JamlLoaderValueReader
                .FromStrings(["Overstock", "grabber", "Tele-scope"])
                .TryEnumArray<MotelyVoucher>(out var value)
        );
        Assert.Equal(
            [MotelyVoucher.Overstock, MotelyVoucher.Grabber, MotelyVoucher.Telescope],
            value
        );
    }

    [Fact]
    public void TryEnumArray_ScalarBecomesOneElement()
    {
        Assert.True(
            JamlLoaderValueReader.FromScalar("Grabber").TryEnumArray<MotelyVoucher>(out var value)
        );
        Assert.Equal([MotelyVoucher.Grabber], value);
    }

    // ── rank, which has its own pip/letter spelling ──

    [Theory]
    [InlineData("2", MotelyStandardcardRank.Two)]
    [InlineData("5", MotelyStandardcardRank.Five)]
    [InlineData("9", MotelyStandardcardRank.Nine)]
    [InlineData("10", MotelyStandardcardRank.Ten)]
    [InlineData("J", MotelyStandardcardRank.Jack)]
    [InlineData("q", MotelyStandardcardRank.Queen)]
    [InlineData("K", MotelyStandardcardRank.King)]
    [InlineData("a", MotelyStandardcardRank.Ace)]
    [InlineData("Ace", MotelyStandardcardRank.Ace)]
    [InlineData("Jack", MotelyStandardcardRank.Jack)]
    public void TryEnum_RankAcceptsPipsAndLetters(string text, MotelyStandardcardRank expected)
    {
        Assert.True(
            JamlLoaderValueReader.FromScalar(text).TryEnum<MotelyStandardcardRank>(out var value)
        );
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("11")]
    [InlineData("0")]
    public void TryEnum_RankRejectsPipsThatAreNotCards(string text)
    {
        var reader = JamlLoaderValueReader.FromScalar(text);

        var ex = Assert.Throws<InvalidOperationException>(
            () => reader.TryEnum<MotelyStandardcardRank>(out _)
        );
        Assert.Contains("rank pip", ex.Message);
    }

    [Fact]
    public void TryEnumArray_RankMixesPipsAndLetters()
    {
        Assert.True(
            JamlLoaderValueReader
                .FromStrings(["2", "K", "Ace"])
                .TryEnumArray<MotelyStandardcardRank>(out var value)
        );
        Assert.Equal(
            [
                MotelyStandardcardRank.Two,
                MotelyStandardcardRank.King,
                MotelyStandardcardRank.Ace,
            ],
            value
        );
    }

    // ── R2: the spellings above have to mean the same thing to a real search ──

    private const string DrawRankLetter = """
        name: reader-rank-letter
        deck: Red
        stake: White
        must:
          - startingDraw:
            rank: A
            suit: Hearts
            antes: [1]
        """;

    private const string DrawRankWord = """
        name: reader-rank-word
        deck: Red
        stake: White
        must:
          - startingDraw:
            rank: Ace
            suit: Hearts
            antes: [1]
        """;

    private const string DrawRankKing = """
        name: reader-rank-king
        deck: Red
        stake: White
        must:
          - startingDraw:
            rank: K
            suit: Hearts
            antes: [1]
        """;

    private static readonly string[] DrawSeeds = ["99", "CC", "F", "Q", "R", "VV"];

    [Fact]
    public void RankSpellings_A_And_Ace_SelectTheSameSeeds()
    {
        var byLetter = ProofSearch.ListMatch(DrawRankLetter, DrawSeeds);
        var byWord = ProofSearch.ListMatch(DrawRankWord, DrawSeeds);

        Assert.Equal(DrawSeeds.Length, (int)byLetter.Matching);
        Assert.Equal(byLetter.Matching, byWord.Matching);
        Assert.Equal(
            byLetter.Matched.OrderBy(static s => s, StringComparer.Ordinal),
            byWord.Matched.OrderBy(static s => s, StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// Guards the previous test: if rank were being ignored entirely, "K" would also match all
    /// six seeds and the alias test would pass for the wrong reason.
    /// </summary>
    [Fact]
    public void RankIsActuallyRead_KingDoesNotMatchTheAceSeeds()
    {
        var (matching, _) = ProofSearch.ListMatch(DrawRankKing, DrawSeeds);

        Assert.True(
            matching < DrawSeeds.Length,
            $"rank: K matched all {DrawSeeds.Length} Ace-of-Hearts seeds — rank is not being read"
        );
    }

    [Fact]
    public void UnknownEnumValue_FailsTheLoadWithANamedError()
    {
        const string bad = """
            name: reader-bad-voucher
            deck: Red
            stake: White
            must:
              - voucher: DefinitelyNotAVoucher
                antes: [1]
            """;

        Assert.False(JamlConfigLoader.TryLoad(bad, out _, out var error));
        Assert.Contains("DefinitelyNotAVoucher", error);
    }
}
