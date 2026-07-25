using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// One-line JAML round-trip. The thesis under test: an item is a packed int that maps
/// to exactly one descriptive string and back, so a one-line spelling (item string + ante
/// tail) and a JAML joker clause are deterministic, lossless re-encodings of each other.
/// </summary>
public class JamlLineTests
{
    // ── The exact example, pinned ─────────────────────────────────────────────

    [Fact]
    public void EternalBlueprintInAntes1Or2_parsesToTheExpectedClause()
    {
        Assert.True(
            JamlLine.TryToClause(
                "Eternal Blueprint in antes 1 or 2",
                out var clause,
                out var error
            ),
            $"parse failed: {error}"
        );

        var joker = Assert.IsType<JokerClause>(clause);
        Assert.Equal([MotelyJoker.Blueprint], joker.Jokers);
        Assert.Equal([MotelyJokerSticker.Eternal], joker.Stickers);
        Assert.Equal([1, 2], joker.Antes);
        Assert.Null(joker.Edition);
    }

    [Fact]
    public void EternalBlueprintInAntes1Or2_roundTripsBackToTheSameLine()
    {
        Assert.True(
            JamlLine.TryToClause("Eternal Blueprint in antes 1 or 2", out var clause, out _)
        );
        Assert.Equal("Eternal Blueprint in antes 1 or 2", JamlLine.FromClause(clause!));
    }

    // ── Tails ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Blueprint", new int[0])]
    [InlineData("Blueprint in ante 1", new[] { 1 })]
    [InlineData("Blueprint in antes 1 or 2", new[] { 1, 2 })]
    [InlineData("Blueprint in antes 1, 2, 3", new[] { 1, 2, 3 })]
    public void AnteTail_parsesAndCanonicalizes(string line, int[] expectedAntes)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.Equal(expectedAntes, Assert.IsType<JokerClause>(clause).Antes);
    }

    [Fact]
    public void CommaAndOrSeparators_areInterchangeable()
    {
        Assert.True(JamlLine.TryToClause("Showman in antes 1, 2", out var a, out _));
        Assert.True(JamlLine.TryToClause("Showman in antes 1 or 2", out var b, out _));
        Assert.Equal(JamlLine.FromClause(a!), JamlLine.FromClause(b!));
    }

    // ── JUMMY: Jammy Understands My Mumbling ──────────────────────────────────
    // The forgiving shorthand a real person actually types — ranges and filler words —
    // must parse, not throw. Accessibility requirement: capture loose input, don't demand
    // precise typing. These tests are the gate; if a change breaks them, JUMMY regressed.

    [Theory]
    [InlineData("Blueprint in antes 1-8", new[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData("Blueprint in antes 1..3", new[] { 1, 2, 3 })]
    [InlineData("Blueprint in antes 1 to 3", new[] { 1, 2, 3 })]
    [InlineData("Blueprint in antes 1 through 3", new[] { 1, 2, 3 })]
    [InlineData("Blueprint in antes 3-1", new[] { 3, 2, 1 })]
    [InlineData("Blueprint in antes 1-2, 5", new[] { 1, 2, 5 })]
    public void AnteRange_expandsInclusively(string line, int[] expected)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.Equal(expected, Assert.IsType<JokerClause>(clause).Antes);
    }

    [Theory]
    [InlineData("Perkeo score 100", 100)]
    [InlineData("Perkeo score 1", 1)]
    [InlineData("Perkeo score -5", -5)]
    public void ScoreTail_capturedOnOneLine(string line, int expectedScore)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.Equal(expectedScore, clause!.Score);
    }

    [Fact]
    public void ScoreTail_ridesAlongsideAnAnteRange()
    {
        Assert.True(
            JamlLine.TryToClause("Perkeo in antes 1-8 score 100", out var clause, out var error),
            $"parse failed: {error}"
        );
        var joker = Assert.IsType<JokerClause>(clause);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], joker.Antes);
        Assert.Equal(100, joker.Score);
    }

    // ── Modifiers ride the packed int ─────────────────────────────────────────

    [Theory]
    [InlineData("Negative Blueprint in ante 1")]
    [InlineData("Eternal Perishable Showman in antes 2 or 3")]
    [InlineData("Foil Oops! All 6s in ante 1")]
    public void Modifiers_roundTrip(string line)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.Equal(line, JamlLine.FromClause(clause!));
    }

    [Fact]
    public void Wildcard_roundTrips()
    {
        Assert.True(JamlLine.TryToClause("Any in ante 1", out var clause, out _));
        Assert.True(Assert.IsType<JokerClause>(clause).IsWildcard);
        Assert.Equal("Any in ante 1", JamlLine.FromClause(clause!));
    }

    // ── The whole joker universe round-trips ──────────────────────────────────

    [Fact]
    public void EveryJoker_roundTripsThroughTheLine()
    {
        var failures = new List<string>();
        foreach (var j in Enum.GetValues<MotelyJoker>())
        {
            var line = JamlLine.FromClause(new JokerClause { Jokers = [j], Antes = [1] });
            if (line is null)
            {
                failures.Add($"{j}: FromClause returned null");
                continue;
            }
            if (!JamlLine.TryToClause(line, out var clause, out var error))
            {
                failures.Add($"{j}: '{line}' failed to parse ({error})");
                continue;
            }
            var back = Assert.IsType<JokerClause>(clause);
            if (back.Jokers is not [var only] || only != j)
                failures.Add($"{j}: '{line}' parsed back to [{string.Join(",", back.Jokers)}]");
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count} joker(s) did not round-trip:\n{string.Join("\n", failures)}"
        );
    }

    // ── Consumable families (tarot / spectral / planet) ───────────────────────

    [Theory]
    [InlineData("The Fool in ante 1")]
    [InlineData("The Emperor in antes 1 or 2")]
    [InlineData("Aura in ante 1")]
    [InlineData("Black Hole in antes 2 or 3")]
    [InlineData("Pluto in ante 1")]
    [InlineData("Planet X in antes 1-3")]
    public void Consumables_roundTrip(string line)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.Equal(line, JamlLine.FromClause(clause!));
    }

    [Fact]
    public void TheFool_parsesToATarotClause()
    {
        Assert.True(JamlLine.TryToClause("The Fool in ante 1", out var clause, out _));
        Assert.Equal([MotelyTarotCard.TheFool], Assert.IsType<TarotCardClause>(clause).Tarots);
    }

    [Fact]
    public void EveryTarot_spectral_planet_roundTripsThroughTheLine()
    {
        var failures = new List<string>();
        RoundTripAll(
            failures,
            (MotelyTarotCard t) => new TarotCardClause { Tarots = [t], Antes = [1] }
        );
        RoundTripAll(
            failures,
            (MotelySpectralCard s) => new SpectralCardClause { Spectrals = [s], Antes = [1] }
        );
        RoundTripAll(
            failures,
            (MotelyPlanetCard p) => new PlanetCardClause { Planets = [p], Antes = [1] }
        );
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} consumable(s) did not round-trip:\n{string.Join("\n", failures)}"
        );
    }

    // ── Remaining one-line families ───────────────────────────────────────────

    [Theory]
    [InlineData("Red Seal Polychrome Steel King of Hearts in ante 1")]
    [InlineData("Gold Ace of Spades in antes 1 or 2")]
    public void StandardCards_roundTrip(string line)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.Equal(line, JamlLine.FromClause(clause!));
    }

    [Fact]
    public void StandardCard_parsesToExpectedClause()
    {
        Assert.True(
            JamlLine.TryToClause(
                "Red Seal Polychrome Steel King of Hearts in ante 1",
                out var clause,
                out var error
            ),
            $"parse failed: {error}"
        );
        var standard = Assert.IsType<StandardCardClause>(clause);
        Assert.Equal(MotelyStandardcardRank.King, standard.Rank);
        Assert.Equal(MotelyStandardcardSuit.Hearts, standard.Suit);
        Assert.Equal(MotelyItemSeal.Red, standard.Seal);
        Assert.Equal(MotelyItemEdition.Polychrome, standard.Edition);
        Assert.Equal(MotelyItemEnhancement.Steel, standard.Enhancement);
        Assert.Equal([1], standard.Antes);
    }

    [Fact]
    public void EveryStandardCard_roundTripsThroughTheLine()
    {
        var failures = new List<string>();
        foreach (var card in Enum.GetValues<MotelyStandardCard>())
        {
            var line = JamlLine.FromClause(
                new StandardCardClause
                {
                    Rank = card.GetRank(),
                    Suit = card.GetSuit(),
                    Antes = [1],
                }
            );
            if (line is null)
            {
                failures.Add($"{card}: FromClause returned null");
                continue;
            }
            if (!JamlLine.TryToClause(line, out var clause, out var error))
            {
                failures.Add($"{card}: '{line}' failed to parse ({error})");
                continue;
            }
            if (JamlLine.FromClause(clause!) != line)
                failures.Add($"{card}: '{line}' -> '{JamlLine.FromClause(clause!)}'");
        }
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} standard card(s) did not round-trip:\n{string.Join("\n", failures)}"
        );
    }

    [Fact]
    public void StartingDraw_roundTripsRankSuitOnly()
    {
        Assert.True(
            JamlLine.TryToClause(
                "Starting Draw King of Hearts in ante 1",
                out var clause,
                out var error
            ),
            $"parse failed: {error}"
        );
        var draw = Assert.IsType<StartingDrawClause>(clause);
        Assert.Equal(MotelyStandardcardRank.King, draw.Rank);
        Assert.Equal(MotelyStandardcardSuit.Hearts, draw.Suit);
        Assert.Equal("Starting Draw King of Hearts in ante 1", JamlLine.FromClause(clause!));

        Assert.False(
            JamlLine.TryToClause("Starting Draw Red Seal King of Hearts", out _, out error)
        );
        Assert.Contains("rank/suit only", error);
    }

    [Fact]
    public void EveryVoucher_tag_boss_roundTripsThroughTheLine()
    {
        var failures = new List<string>();
        RoundTripAll(
            failures,
            (MotelyVoucher v) =>
                new VoucherClause
                {
                    Vouchers = [v],
                    Rolls = [0],
                    Antes = [1],
                }
        );
        RoundTripAll(
            failures,
            (MotelyTag t) =>
                new TagClause
                {
                    Tags = [t],
                    Rolls = [0, 1],
                    Antes = [1],
                }
        );
        RoundTripAll(failures, (MotelyBossBlind b) => new BossClause { Bosses = [b], Antes = [1] });
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} feature clause(s) did not round-trip:\n{string.Join("\n", failures)}"
        );
    }

    [Theory]
    [InlineData("Voucher Telescope rolls 0 in ante 1", typeof(VoucherClause))]
    [InlineData("Small Blind Tag Negative Tag in ante 1", typeof(TagClause))]
    [InlineData("Big Blind Tag Rare Tag in ante 2", typeof(TagClause))]
    [InlineData("Tag Charm Tag rolls 0 or 1 in ante 1", typeof(TagClause))]
    [InlineData("Boss The Wall in ante 3", typeof(BossClause))]
    public void FeaturePrefixes_parseToExpectedClauseTypes(string line, Type expectedType)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.IsType(expectedType, clause);
        Assert.Equal(line, JamlLine.FromClause(clause!));
    }

    [Theory]
    [InlineData("Lucky Money rolls 0 or 1 with luck 4", typeof(LuckyMoneyClause))]
    [InlineData("Lucky Mult rolls 0 with luck 5", typeof(LuckyMultClause))]
    [InlineData("Misprint Mult rolls 0 or 1 mult 23", typeof(MisprintMultClause))]
    [InlineData("Wheel of Fortune rolls 0 with luck 4", typeof(WheelOfFortuneClause))]
    [InlineData("Gros Michel Extinct rolls 0", typeof(GrosMichelExtinctClause))]
    [InlineData("Cavendish Extinct rolls 0", typeof(CavendishExtinctClause))]
    [InlineData("Space Levelup rolls 0 with luck 4", typeof(SpaceLevelupClause))]
    [InlineData("Glass Destroy rolls 0 with luck 4", typeof(GlassDestroyClause))]
    [InlineData("Wheel Stays Flipped rolls 0 with luck 4", typeof(WheelStaysFlippedClause))]
    [InlineData("Business Payout rolls 0", typeof(BusinessPayoutClause))]
    [InlineData("Bloodstone Trigger rolls 0", typeof(BloodstoneTriggerClause))]
    [InlineData("Parking Payout rolls 0", typeof(ParkingPayoutClause))]
    public void Events_parseToExpectedClauseTypes(string line, Type expectedType)
    {
        Assert.True(
            JamlLine.TryToClause(line, out var clause, out var error),
            $"parse failed: {error}"
        );
        Assert.IsType(expectedType, clause);
        Assert.Equal(line, JamlLine.FromClause(clause!));
    }

    [Fact]
    public void EventLuck_rejectsUnsupportedEvent()
    {
        Assert.False(
            JamlLine.TryToClause("Business Payout rolls 0 with luck 4", out _, out var error)
        );
        Assert.Contains("does not support luck", error);
    }

    [Fact]
    public void LogicAndMultiValueClauses_areNotSingleJamlLines()
    {
        Assert.Null(JamlLine.FromClause(new AndClause { Clauses = [] }));
        Assert.Null(
            JamlLine.FromClause(
                new JokerClause { Jokers = [MotelyJoker.Blueprint, MotelyJoker.Brainstorm] }
            )
        );
        Assert.Null(
            JamlLine.FromClause(
                new VoucherClause
                {
                    Vouchers = [MotelyVoucher.Telescope, MotelyVoucher.Observatory],
                    Rolls = [0],
                }
            )
        );
    }

    private static void RoundTripAll<T>(List<string> failures, Func<T, IJamlClause> make)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            var line = JamlLine.FromClause(make(value));
            if (line is null)
            {
                failures.Add($"{typeof(T).Name}.{value}: FromClause returned null");
                continue;
            }
            if (!JamlLine.TryToClause(line, out var clause, out var error))
            {
                failures.Add($"{typeof(T).Name}.{value}: '{line}' failed to parse ({error})");
                continue;
            }
            var back = JamlLine.FromClause(clause!);
            if (back != line)
                failures.Add($"{typeof(T).Name}.{value}: '{line}' -> '{back}'");
        }
    }

    // ── The int law itself (the foundation the one-line spelling stands on) ───

    [Fact]
    public void FormatThenParse_isIdentityOnThePackedInt()
    {
        MotelyItem[] samples =
        [
            new(MotelyJoker.Blueprint),
            new MotelyItem(MotelyJoker.Blueprint).WithEternal(true),
            new MotelyItem(MotelyJoker.Showman, MotelyItemEdition.Negative),
            new MotelyItem(MotelyJoker.OopsAll6s, MotelyItemEdition.Foil).WithPerishable(true),
        ];

        foreach (var item in samples)
        {
            Assert.True(
                FormatUtils.TryParseMotelyItem(FormatUtils.FormatItem(item), out var back),
                $"could not parse '{FormatUtils.FormatItem(item)}'"
            );
            Assert.Equal(item.Value, back.Value);
        }
    }
}
