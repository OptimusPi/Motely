using Xunit;

namespace Motely.Tests;

public sealed class MotelyItemTests
{
    public static TheoryData<string> PlainTypeNames =>
        new()
        {
            "C2",
            "SA",
            "Mercury",
            "TheFool",
            "TheHighPriestess",
            "Familiar",
            "BlackHole",
            "Joker",
            "GreedyJoker",
            "DNA",
            "Canio",
            "Invalid",
        };

    [Theory]
    [MemberData(nameof(PlainTypeNames))]
    public void Parse_round_trips_FormatItem_plain_type(string typeName)
    {
        var type = Enum.Parse<MotelyItemType>(typeName);
        var item = new MotelyItem(type);
        var jummy = FormatUtils.FormatItem(item);
        Assert.Equal(item, MotelyItem.Parse(jummy));
    }

    [Fact]
    public void Parse_round_trips_FormatItem_full_jummy()
    {
        var item = new MotelyItem(MotelyItemType.Joker)
            .WithSeal(MotelyItemSeal.Purple)
            .WithPerishable(true)
            .WithEternal(true)
            .WithRental(true)
            .WithEdition(MotelyItemEdition.Foil)
            .WithEnhancement(MotelyItemEnhancement.Bonus);
        Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
    }

    [Fact]
    public void Parse_round_trips_FormatItem_seal_only()
    {
        var item = new MotelyItem(MotelyItemType.CA).WithSeal(MotelyItemSeal.Gold);
        Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
    }

    [Fact]
    public void Parse_round_trips_FormatItem_edition_only_joker()
    {
        var item = new MotelyItem(MotelyItemType.Misprint).WithEdition(MotelyItemEdition.Negative);
        Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
    }

    [Fact]
    public void Parse_round_trips_FormatItem_enhancement_only_playing_card()
    {
        var item = new MotelyItem(MotelyItemType.HA).WithEnhancement(MotelyItemEnhancement.Steel);
        Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
    }

    [Fact]
    public void Parse_round_trips_FormatItem_seal_and_stickers_no_edition_enhancement()
    {
        var item = new MotelyItem(MotelyItemType.S7)
            .WithSeal(MotelyItemSeal.Blue)
            .WithPerishable(true)
            .WithEternal(true);
        Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
    }

    [Fact]
    public void Parse_round_trips_FormatItem_all_seals()
    {
        foreach (MotelyItemSeal seal in Enum.GetValues<MotelyItemSeal>())
        {
            if (seal == MotelyItemSeal.None)
                continue;
            var item = new MotelyItem(MotelyItemType.Temperance).WithSeal(seal);
            Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
        }
    }

    [Fact]
    public void Parse_round_trips_FormatItem_all_enhancements_on_playing_card()
    {
        foreach (MotelyItemEnhancement enh in Enum.GetValues<MotelyItemEnhancement>())
        {
            if (enh == MotelyItemEnhancement.None)
                continue;
            var item = new MotelyItem(MotelyItemType.D3).WithEnhancement(enh);
            Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
        }
    }

    [Fact]
    public void Parse_round_trips_FormatItem_all_editions_on_tarot()
    {
        foreach (MotelyItemEdition ed in Enum.GetValues<MotelyItemEdition>())
        {
            if (ed == MotelyItemEdition.None)
                continue;
            var item = new MotelyItem(MotelyItemType.Death).WithEdition(ed);
            Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
        }
    }

    [Fact]
    public void Parse_round_trips_FormatItem_eternal_polychrome_scary_face()
    {
        var item = new MotelyItem(MotelyItemType.ScaryFace)
            .WithEternal(true)
            .WithEdition(MotelyItemEdition.Polychrome);
        Assert.Equal(item, MotelyItem.Parse(FormatUtils.FormatItem(item)));
    }

    [Theory]
    [InlineData("theworld")]
    [InlineData("THEWORLD")]
    [InlineData("  TheWorld  ")]
    public void Parse_accepts_case_insensitive_and_trimmed_type(string input)
    {
        var expected = new MotelyItem(MotelyItemType.TheWorld);
        Assert.Equal(expected, MotelyItem.Parse(input));
    }

    [Fact]
    public void Parse_throws_FormatException_on_empty()
    {
        var ex = Assert.Throws<FormatException>(() => MotelyItem.Parse(""));
        Assert.Contains("Unrecognized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_throws_FormatException_on_whitespace_only()
    {
        Assert.Throws<FormatException>(() => MotelyItem.Parse("   \t  "));
    }

    [Fact]
    public void Parse_throws_FormatException_on_unknown_type()
    {
        Assert.Throws<FormatException>(() => MotelyItem.Parse("TotallyFakeJoker"));
    }

    [Fact]
    public void Parse_throws_FormatException_on_two_token_unrecognized_prefix()
    {
        Assert.Throws<FormatException>(() => MotelyItem.Parse("Nope StillWrong"));
    }

    [Fact]
    public void Parse_throws_FormatException_on_too_many_tail_tokens()
    {
        Assert.Throws<FormatException>(() => MotelyItem.Parse("Foil Bonus Extra Joker"));
    }

    [Fact]
    public void Parse_throws_FormatException_on_three_token_with_None_edition()
    {
        Assert.Throws<FormatException>(() => MotelyItem.Parse("None Bonus Joker"));
    }

    [Fact]
    public void Parse_throws_FormatException_on_three_token_with_None_enhancement()
    {
        Assert.Throws<FormatException>(() => MotelyItem.Parse("Foil None Joker"));
    }
}
