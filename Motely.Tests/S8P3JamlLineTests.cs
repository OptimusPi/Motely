using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// S8.P3 — JamlLine terse grammar. Law: <c>Canonicalize</c> is a fixed point (line → clause
/// → line reproduces itself), and every family parses to the clause type the engine owns.
/// </summary>
public sealed class S8P3JamlLineTests
{
    [Theory]
    [InlineData("Blueprint", typeof(JokerClause))]
    [InlineData("Any", typeof(JokerClause))]
    [InlineData("Negative Blueprint", typeof(JokerClause))]
    [InlineData("Eternal Blueprint", typeof(JokerClause))]
    [InlineData("Voucher Overstock", typeof(VoucherClause))]
    [InlineData("Tag Charm Tag", typeof(TagClause))]
    [InlineData("Small Blind Tag Charm Tag", typeof(TagClause))]
    [InlineData("Big Blind Tag Charm Tag", typeof(TagClause))]
    [InlineData("The Fool", typeof(TarotCardClause))]
    [InlineData("Sigil", typeof(SpectralCardClause))]
    [InlineData("Pluto", typeof(PlanetCardClause))]
    [InlineData("2 of Clubs", typeof(StandardCardClause))]
    [InlineData("Lucky Money rolls 0-2 with luck 2", typeof(LuckyMoneyClause))]
    [InlineData("Lucky Mult rolls 0", typeof(LuckyMultClause))]
    [InlineData("Wheel of Fortune rolls 0", typeof(WheelOfFortuneClause))]
    [InlineData("Gros Michel Extinct rolls 0", typeof(GrosMichelExtinctClause))]
    [InlineData("Cavendish Extinct rolls 0", typeof(CavendishExtinctClause))]
    [InlineData("Space Levelup rolls 0", typeof(SpaceLevelupClause))]
    [InlineData("Glass Destroy rolls 0", typeof(GlassDestroyClause))]
    [InlineData("Wheel Stays Flipped rolls 0", typeof(WheelStaysFlippedClause))]
    [InlineData("Business Payout rolls 0", typeof(BusinessPayoutClause))]
    [InlineData("Bloodstone Trigger rolls 0", typeof(BloodstoneTriggerClause))]
    [InlineData("Parking Payout rolls 0", typeof(ParkingPayoutClause))]
    [InlineData("Misprint Mult rolls 0 mult 20", typeof(MisprintMultClause))]
    public void TerseLines_ParseToEngineClause_AndCanonicalizeIsFixedPoint(
        string line,
        Type clauseType
    )
    {
        Assert.Null(JamlLine.Validate(line));
        Assert.True(JamlLine.TryToClause(line, out var clause, out var error), error);
        Assert.IsType(clauseType, clause);

        var canonical = JamlLine.Canonicalize(line);
        Assert.Equal(canonical, JamlLine.Canonicalize(canonical));
    }

    [Fact]
    public void ScoreTail_LandsOnTheClause()
    {
        Assert.True(JamlLine.TryToClause("Blueprint score 100", out var clause, out var error),
            error);
        Assert.Equal(100, clause!.Score);
    }

    [Fact]
    public void AnteTail_ScopesTheClause()
    {
        Assert.True(JamlLine.TryToClause("Blueprint in ante 2", out var clause, out var error),
            error);
        var joker = Assert.IsType<JokerClause>(clause);
        Assert.Equal([2], joker.Antes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Utter Garbage Item Name")]
    [InlineData("Blueprint score notanumber")]
    [InlineData("Business Payout rolls 0 with luck 2")]
    [InlineData("Lucky Money rolls 0 with luck 99")]
    [InlineData("Lucky Money")]
    [InlineData("Boss Not A Boss")]
    public void BadLines_ValidateReturnsAnError(string line)
    {
        Assert.NotNull(JamlLine.Validate(line));
    }

    [Fact]
    public void BossLine_ParsesViaTheEngineFormatter()
    {
        var boss = MotelyBossBlind.TheArm;
        var line = $"Boss {FormatUtils.FormatBoss(boss)}";
        Assert.True(JamlLine.TryToClause(line, out var clause, out var error), error);
        var parsed = Assert.IsType<BossClause>(clause);
        Assert.Equal([boss], parsed.Bosses);
    }

    [Fact]
    public void FromClause_RendersWhatTryToClauseReads()
    {
        Assert.True(JamlLine.TryToClause("Voucher Overstock in ante 1", out var clause, out _));
        var rendered = JamlLine.FromClause(clause!);
        Assert.NotNull(rendered);
        Assert.True(JamlLine.TryToClause(rendered, out var reparsed, out var error), error);
        var voucher = Assert.IsType<VoucherClause>(reparsed);
        Assert.Equal([MotelyVoucher.Overstock], voucher.Vouchers);
        Assert.Equal([1], voucher.Antes);
    }
}
