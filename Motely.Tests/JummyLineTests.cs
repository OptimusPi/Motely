using Motely.Filters.Jummy;

namespace Motely.Tests;

/// <summary>
/// JUMMY ⟷ JAML round-trip. The thesis under test: an item is a packed int that maps
/// to exactly one descriptive string and back, so a JUMMY line (item string + ante
/// tail) and a JAML joker clause are deterministic, lossless re-encodings of each other.
/// </summary>
public class JummyLineTests
{
    // ── The exact example, pinned ─────────────────────────────────────────────

    [Fact]
    public void EternalBlueprintInAntes1Or2_parsesToTheExpectedClause()
    {
        Assert.True(
            JummyLine.TryToClause("Eternal Blueprint in antes 1 or 2", out var clause, out var error),
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
        Assert.True(JummyLine.TryToClause("Eternal Blueprint in antes 1 or 2", out var clause, out _));
        Assert.Equal("Eternal Blueprint in antes 1 or 2", JummyLine.FromClause(clause!));
    }

    // ── Tails ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Blueprint", new int[0])]
    [InlineData("Blueprint in ante 1", new[] { 1 })]
    [InlineData("Blueprint in antes 1 or 2", new[] { 1, 2 })]
    [InlineData("Blueprint in antes 1, 2, 3", new[] { 1, 2, 3 })]
    public void AnteTail_parsesAndCanonicalizes(string line, int[] expectedAntes)
    {
        Assert.True(JummyLine.TryToClause(line, out var clause, out var error), $"parse failed: {error}");
        Assert.Equal(expectedAntes, Assert.IsType<JokerClause>(clause).Antes);
    }

    [Fact]
    public void CommaAndOrSeparators_areInterchangeable()
    {
        Assert.True(JummyLine.TryToClause("Showman in antes 1, 2", out var a, out _));
        Assert.True(JummyLine.TryToClause("Showman in antes 1 or 2", out var b, out _));
        Assert.Equal(JummyLine.FromClause(a!), JummyLine.FromClause(b!));
    }

    // ── Modifiers ride the packed int ─────────────────────────────────────────

    [Theory]
    [InlineData("Negative Blueprint in ante 1")]
    [InlineData("Eternal Perishable Showman in antes 2 or 3")]
    [InlineData("Foil Oops! All 6s in ante 1")]
    public void Modifiers_roundTrip(string line)
    {
        Assert.True(JummyLine.TryToClause(line, out var clause, out var error), $"parse failed: {error}");
        Assert.Equal(line, JummyLine.FromClause(clause!));
    }

    [Fact]
    public void Wildcard_roundTrips()
    {
        Assert.True(JummyLine.TryToClause("Any in ante 1", out var clause, out _));
        Assert.True(Assert.IsType<JokerClause>(clause).IsWildcard);
        Assert.Equal("Any in ante 1", JummyLine.FromClause(clause!));
    }

    // ── The whole joker universe round-trips ──────────────────────────────────

    [Fact]
    public void EveryJoker_roundTripsThroughTheLine()
    {
        var failures = new List<string>();
        foreach (var j in Enum.GetValues<MotelyJoker>())
        {
            var line = JummyLine.FromClause(new JokerClause { Jokers = [j], Antes = [1] });
            if (line is null)
            {
                failures.Add($"{j}: FromClause returned null");
                continue;
            }
            if (!JummyLine.TryToClause(line, out var clause, out var error))
            {
                failures.Add($"{j}: '{line}' failed to parse ({error})");
                continue;
            }
            var back = Assert.IsType<JokerClause>(clause);
            if (back.Jokers is not [var only] || only != j)
                failures.Add($"{j}: '{line}' parsed back to [{string.Join(",", back.Jokers)}]");
        }

        Assert.True(failures.Count == 0, $"{failures.Count} joker(s) did not round-trip:\n{string.Join("\n", failures)}");
    }

    // ── Consumable families (tarot / spectral / planet) ───────────────────────

    [Theory]
    [InlineData("The Fool in ante 1")]
    [InlineData("The Emperor in antes 1 or 2")]
    [InlineData("Aura in ante 1")]
    [InlineData("Black Hole in antes 2 or 3")]
    [InlineData("Pluto in ante 1")]
    [InlineData("Planet X in antes 1 or 2 or 3")]
    public void Consumables_roundTrip(string line)
    {
        Assert.True(JummyLine.TryToClause(line, out var clause, out var error), $"parse failed: {error}");
        Assert.Equal(line, JummyLine.FromClause(clause!));
    }

    [Fact]
    public void TheFool_parsesToATarotClause()
    {
        Assert.True(JummyLine.TryToClause("The Fool in ante 1", out var clause, out _));
        Assert.Equal([MotelyTarotCard.TheFool], Assert.IsType<TarotCardClause>(clause).Tarots);
    }

    [Fact]
    public void EveryTarot_spectral_planet_roundTripsThroughTheLine()
    {
        var failures = new List<string>();
        RoundTripAll(failures, (MotelyTarotCard t) => new TarotCardClause { Tarots = [t], Antes = [1] });
        RoundTripAll(failures, (MotelySpectralCard s) => new SpectralCardClause { Spectrals = [s], Antes = [1] });
        RoundTripAll(failures, (MotelyPlanetCard p) => new PlanetCardClause { Planets = [p], Antes = [1] });
        Assert.True(failures.Count == 0, $"{failures.Count} consumable(s) did not round-trip:\n{string.Join("\n", failures)}");
    }

    private static void RoundTripAll<T>(List<string> failures, Func<T, IJamlClause> make)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            var line = JummyLine.FromClause(make(value));
            if (line is null)
            {
                failures.Add($"{typeof(T).Name}.{value}: FromClause returned null");
                continue;
            }
            if (!JummyLine.TryToClause(line, out var clause, out var error))
            {
                failures.Add($"{typeof(T).Name}.{value}: '{line}' failed to parse ({error})");
                continue;
            }
            var back = JummyLine.FromClause(clause!);
            if (back != line)
                failures.Add($"{typeof(T).Name}.{value}: '{line}' -> '{back}'");
        }
    }

    // ── The int law itself (the foundation JUMMY stands on) ───────────────────

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
            Assert.True(FormatUtils.TryParseMotelyItem(FormatUtils.FormatItem(item), out var back),
                $"could not parse '{FormatUtils.FormatItem(item)}'");
            Assert.Equal(item.Value, back.Value);
        }
    }
}
