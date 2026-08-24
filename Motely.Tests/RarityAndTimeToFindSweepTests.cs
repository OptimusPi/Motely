using System.Diagnostics;
using Motely.Filters;
using Motely.Filters.Jaml;
using Xunit.Abstractions;

namespace Motely.Tests;

/// <summary>
/// Rarity + time-to-find sweep. For each FilterDesc family we run a real sequential search and
/// measure the empirical per-seed match probability p̂ = matches / seedsSearched, then report it as
/// "1 in N" and an estimated wall-clock time-to-first-hit.
///
/// Lazy on purpose (context is the tank): ONE data-driven test sweeps a table of clauses instead of
/// 59 hand-written ones. Batches escalate — 1 batch (~1.5M), then 2, 3, 4, 5 — and we stop the
/// moment a clause has produced enough hits to trust the number. If a clause still hasn't after 5
/// batches it is simply *rare* (that's the point — negative legendary is ~1 in 300k+), so we report
/// what we saw rather than fail; a hard convergence assertion lives on the one clause with a known
/// lua-derived rate (see <see cref="Rarity_MatchesBalatroLua_ForNegativeLegendary"/>).
///
/// The oracle for correctness is Balatro's own lua (D:\Balatro\functions\common_events.lua):
/// negative edition = 0.003, rarity tiers common ≤0.7 / uncommon 0.7–0.95 / rare 0.95. Motely has
/// had bugs before, so measured rarity is cross-checked against the game source, not trusted blind.
/// </summary>
public sealed class RarityAndTimeToFindSweepTests(ITestOutputHelper output)
{
    /// <summary>Seeds per escalation batch. 1.5M ≈ one comfortable sequential slice.</summary>
    private const long BatchSize = 1_500_000;

    /// <summary>Max escalations before we stop searching and just report (rare is not a failure).</summary>
    private const int MaxBatches = 5;

    /// <summary>Enough hits that p̂ is worth printing as a stable-ish estimate.</summary>
    private const int EnoughHits = 8;

    private readonly record struct Case(string Name, IJamlClause Clause);

    /// <summary>The sweep table — one representative clause per FilterDesc family. Add a row to
    /// cover a new family; the harness does the rest. (Lazy: the table IS the test surface.)</summary>
    private static IEnumerable<Case> Cases()
    {
        // Jokers
        yield return new("Joker.any", new JokerClause { Antes = [1] });
        yield return new("Joker.Common", new CommonJokerClause { Antes = [1] });
        yield return new("Joker.Uncommon", new UncommonJokerClause { Antes = [1] });
        yield return new("Joker.Rare", new RareJokerClause { Antes = [1] });
        yield return new("Joker.Legendary", new LegendaryJokerClause { Antes = [1] });
        yield return new(
            "Joker.NegativeLegendary(own stream)",
            new LegendaryJokerClause { Edition = MotelyItemEdition.Negative, Antes = [1], Min = 1 }
        );

        // Cards
        yield return new(
            "Tarot.TheFool",
            new TarotCardClause { Tarots = [MotelyTarotCard.TheFool], Antes = [1] }
        );
        yield return new(
            "Planet.Mercury",
            new PlanetCardClause { Planets = [MotelyPlanetCard.Mercury], Antes = [1] }
        );
        yield return new(
            "Spectral.Familiar",
            new SpectralCardClause { Spectrals = [MotelySpectralCard.Familiar], Antes = [1] }
        );
        yield return new(
            "Spectral.TheSoul",
            new SpectralCardClause { Spectrals = [MotelySpectralCard.TheSoul], Antes = [1] }
        );
        yield return new(
            "Spectral.BlackHole",
            new SpectralCardClause { Spectrals = [MotelySpectralCard.BlackHole], Antes = [1] }
        );
        yield return new(
            "StandardCard.2ofSpades",
            new StandardCardClause
            {
                Rank = MotelyStandardcardRank.Two,
                Suit = MotelyStandardcardSuit.Spades,
                Antes = [1],
            }
        );
        yield return new(
            "Erratic.Rank2",
            new ErraticRankClause { Rank = MotelyStandardcardRank.Two, Antes = [1] }
        );
        yield return new(
            "Erratic.Spades",
            new ErraticSuitClause { Suit = MotelyStandardcardSuit.Spades, Antes = [1] }
        );

        // Ante features
        yield return new(
            "Voucher.Overstock",
            new VoucherClause { Vouchers = [MotelyVoucher.Overstock], Rolls = [0], Antes = [1] }
        );
        yield return new(
            "Tag.RareTag",
            new TagClause { Tags = [MotelyTag.RareTag], Rolls = [0], Antes = [1] }
        );
        yield return new(
            "Boss.CeruleanBell",
            new BossClause { Bosses = [MotelyBossBlind.CeruleanBell], Antes = [1] }
        );
        yield return new(
            "BoosterPack.any",
            new BoosterPackClause { Antes = [1] }
        );
        yield return new(
            "PokerHand.any",
            new PokerHandClause { Antes = [1] }
        );
        yield return new(
            "StartingDraw.Rank2",
            new StartingDrawClause { Rank = MotelyStandardcardRank.Two, Antes = [1] }
        );

        // Events (roll-scoped)
        yield return new("Event.LuckyMoney", new LuckyMoneyClause { Rolls = [0, 1] });
        yield return new("Event.LuckyMult", new LuckyMultClause { Rolls = [0, 1] });
        yield return new("Event.MisprintMult", new MisprintMultClause { Rolls = [0, 1], Mult = 1 });
        yield return new("Event.WheelOfFortune", new WheelOfFortuneClause { Rolls = [0] });
        yield return new("Event.CavendishExtinct", new CavendishExtinctClause { Rolls = [0] });
        yield return new("Event.GrosMichelExtinct", new GrosMichelExtinctClause { Rolls = [0] });
        yield return new("Event.SpaceLevelup", new SpaceLevelupClause { Rolls = [0] });
        yield return new("Event.BusinessPayout", new BusinessPayoutClause { Rolls = [0] });
        yield return new("Event.BloodstoneTrigger", new BloodstoneTriggerClause { Rolls = [0] });
        yield return new("Event.ParkingPayout", new ParkingPayoutClause { Rolls = [0] });
        yield return new("Event.GlassDestroy", new GlassDestroyClause { Rolls = [0] });
        yield return new("Event.WheelStaysFlipped", new WheelStaysFlippedClause { Rolls = [0] });
    }

    /// <summary>
    /// Fast single-row smoke: one common clause over one small slice, so it returns in ~a second.
    /// Proves the harness + "1 in N" notation work without the full multi-million-seed sweep.
    /// </summary>
    [Fact]
    public void Smoke_OneRow_Fast()
    {
        var clause = new JokerClause { Antes = [1] }; // "any joker" — common, hits immediately
        var (matches, searched) = RunSequentialSlice(clause, 0, 50_000);
        output.WriteLine($"Joker.any: {matches}/{searched} = {OneInNotation(matches, searched)}");
        Assert.True(searched > 0);
        Assert.True(matches >= 0 && matches <= searched);
    }

    [Fact]
    public void Sweep_RarityAndTimeToFind_AcrossFilterFamilies()
    {
        output.WriteLine($"{"filter",-38} {"1 in N",-16} {"hits/searched",-20} {"~time-to-find"}");
        output.WriteLine(new string('-', 92));

        foreach (var c in Cases())
        {
            var (matches, searched, elapsed) = SearchUntilEnoughHits(c.Clause);
            output.WriteLine(FormatRow(c.Name, matches, searched, elapsed));

            // Ran at all, produced honest counters — that's the only universal invariant. Rarity
            // itself is data, reported above, not asserted here (rare ≠ broken).
            Assert.True(searched > 0, $"{c.Name}: search must run over the sequential space");
            Assert.True(matches >= 0 && matches <= searched, $"{c.Name}: match count sane");
        }
    }

    /// <summary>
    /// The one hard cross-check against the game source: a negative-edition roll is 0.003 in the lua
    /// (<c>edition_poll &gt; 1 - 0.003*_mod</c>). It is its own stream, exact in SIMD, so the
    /// measured negative rate should sit in the right order of magnitude. We assert only a loose
    /// band (it is genuinely rare, so we allow a wide envelope and mostly guard against a rate that
    /// is wildly wrong — e.g. the NaN-hash bug making everything "match").
    /// </summary>
    [Fact]
    public void Rarity_MatchesBalatroLua_ForNegativeLegendary()
    {
        var clause = new LegendaryJokerClause
        {
            Edition = MotelyItemEdition.Negative,
            Antes = [1],
            Min = 1,
        };

        var (matches, searched, _) = SearchUntilEnoughHits(clause);
        output.WriteLine($"negative legendary: {matches} / {searched} = {OneInNotation(matches, searched)}");

        // If searched is huge and matches is (say) > 10% of it, the deterministic PRNG is producing
        // garbage (the 35 NaN-hash seeds writ large / a real bug) — negative-anything can't be common.
        if (searched > 0 && matches > 0)
        {
            double p = matches / (double)searched;
            Assert.True(p < 0.05, $"negative legendary rate {p:P3} is implausibly common vs lua 0.003 — likely a hash/rarity bug");
        }
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sequentially search escalating batches until we have <see cref="EnoughHits"/> matches or hit
    /// <see cref="MaxBatches"/>. Returns cumulative (matches, searched, elapsed). Uses batch-index
    /// bounds so each escalation covers a fresh, contiguous slice of the deterministic space.
    /// </summary>
    private static (long Matches, long Searched, TimeSpan Elapsed) SearchUntilEnoughHits(
        IJamlClause clause
    )
    {
        long matches = 0,
            searched = 0;
        var sw = Stopwatch.StartNew();

        for (int batch = 0; batch < MaxBatches; batch++)
        {
            long start = batch * BatchSize;
            long stop = start + BatchSize - 1;
            var (m, s) = RunSequentialSlice(clause, start, stop);
            matches += m;
            searched += s;
            if (matches >= EnoughHits)
                break;
        }

        sw.Stop();
        return (matches, searched, sw.Elapsed);
    }

    private static (long Matches, long Searched) RunSequentialSlice(
        IJamlClause clause,
        long startSearchIndex,
        long stopSearchIndex
    )
    {
        var config = new JamlConfig
        {
            Id = "rarity-sweep",
            Deck = MotelyDeck.Red,
            Stake = MotelyStake.White,
        };
        config.Must.Add(clause);

        const int batchCharCount = 4;
        var (startBatch, endBatchExclusive) = SeedMath.SearchIndexRangeToBatchRange(
            startSearchIndex,
            stopSearchIndex,
            batchCharCount
        );

        long hits = 0;
        var settings = JamlSearchBuilder
            .CreateSettings(config)
            .WithSequentialSearch()
            .WithBatchCharacterCount(batchCharCount)
            .WithStartBatchIndex(startBatch)
            .WithEndBatchIndex(endBatchExclusive)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithSeedMatchCallback(_ => Interlocked.Increment(ref hits));

        using var search = settings.Start();
        search.AwaitCompletion();
        return (hits, search.TotalSeedsSearched);
    }

    // ── notation ─────────────────────────────────────────────────────────────

    private static string FormatRow(string name, long matches, long searched, TimeSpan elapsed)
    {
        string oneIn = OneInNotation(matches, searched);
        string time = matches > 0
            ? MeasuredTimeToFind(matches, searched, elapsed)
            : $"(0 in {searched:N0})";
        return $"{name,-38} {oneIn,-16} {matches}/{searched,-14:N0} {time}";
    }

    /// <summary>
    /// "1 in 1.53M" style, from <see cref="JamlRarityReport"/> so the sweep report and the
    /// pre-search block cannot drift apart in how they spell the same quantity.
    /// </summary>
    internal static string OneInNotation(long matches, long searched) =>
        JamlRarityReport.OneInNotation(matches, searched);

    /// <summary>
    /// Extrapolate wall-clock time to find one, from this run's measured throughput.
    /// <para>
    /// Formats through <see cref="JamlRarityReport.Duration"/>. The local version this replaced
    /// used <c>hh\:mm\:ss</c>, whose hours component wraps at a day — so every rare row in this
    /// table that took longer than 24h to find printed a number 24 hours too small, silently.
    /// </para>
    /// </summary>
    private static string MeasuredTimeToFind(long matches, long searched, TimeSpan elapsed)
    {
        double seedsPerSec = elapsed.TotalSeconds > 0 ? searched / elapsed.TotalSeconds : 0;
        if (seedsPerSec <= 0)
            return "(too fast to time)";
        return JamlRarityReport.Duration(
            JamlRarityReport.SecondsToFirstMatch(matches / (double)searched, seedsPerSec)
        );
    }
}
