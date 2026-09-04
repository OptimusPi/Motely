using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// The twelve roll-scoped event families answer "how rare is this?" from constants, before any
/// seed is visited. These tests are the proof of that claim: every expectation below is a number
/// written out by hand from the game's own odds, never a figure sampled from a run and pasted back
/// in. If the maths and the runtime ever disagree, this file is the one asserting what the runtime
/// was supposed to be doing.
/// </summary>
public sealed class JamlRollRarityTests
{
    private const double Tolerance = 1e-12;

    /// <summary>Red/White — the events ignore the context, so any value would do; this is the engine's default.</summary>
    private static readonly JamlRarityContext Ctx = JamlRarityContext.Default;

    private static LuckyMoneyClause LuckyMoney(int[] rolls, int min = 1, int? max = null) =>
        new()
        {
            Rolls = rolls,
            Min = min,
            Max = max,
        };

    // ── the headline case ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One draw against a 1-in-15 rate is 1/15. The whole design rests on this being a division
    /// rather than a search, so it is asserted exactly, not approximately.
    /// </summary>
    [Fact]
    public void SingleRoll_IsTheBareRate()
    {
        Assert.Equal(1.0 / 15.0, LuckyMoneyFilterDesc.EstimateRarity(LuckyMoney([0]), Ctx), Tolerance);
    }

    /// <summary>The rarest event in the set, and the one where a sloppy factorial would lose digits.</summary>
    [Fact]
    public void CavendishExtinct_IsOneInAThousand()
    {
        var clause = new CavendishExtinctClause { Rolls = [0] };
        Assert.Equal(1.0 / 1000.0, CavendishExtinctFilterDesc.EstimateRarity(clause, Ctx), Tolerance);
    }

    /// <summary>Rolls are independent trials, so several of them compose as a binomial, not a sum.</summary>
    [Fact]
    public void SeveralRolls_ComposeAsBinomial()
    {
        // Three coin flips, at least two heads: (C(3,2) + C(3,3)) / 8 = 4/8.
        var clause = new BusinessPayoutClause { Rolls = [0, 1, 2], Min = 2 };
        Assert.Equal(0.5, BusinessPayoutFilterDesc.EstimateRarity(clause, Ctx), Tolerance);
    }

    /// <summary>
    /// The filters walk the stream once and step past a repeated index without re-rolling it, so a
    /// duplicate is not a second chance. Counting the array length would make this 1-(14/15)³.
    /// </summary>
    [Fact]
    public void DuplicateRolls_AreOneTrial()
    {
        Assert.Equal(
            LuckyMoneyFilterDesc.EstimateRarity(LuckyMoney([0]), Ctx),
            LuckyMoneyFilterDesc.EstimateRarity(LuckyMoney([0, 0, 0]), Ctx),
            Tolerance
        );
    }

    // ── the window ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Asking for more hits than there are rolls is impossible — a fact, not an unknown.</summary>
    [Fact]
    public void MinAboveRollCount_IsImpossibleRatherThanUnknown()
    {
        double p = LuckyMoneyFilterDesc.EstimateRarity(LuckyMoney([0, 1], min: 3), Ctx);

        Assert.False(double.IsNaN(p));
        Assert.Equal(0.0, p);
    }

    /// <summary>
    /// A Max of 0 is a ceiling like any other, exactly as <c>MeetsOccurrenceBounds</c> reads it:
    /// with min 0 it is "neither roll fires", (14/15)²; with the default min of 1 the window is
    /// empty and the answer is an honest zero. Only a null Max means "no upper gate".
    /// </summary>
    [Fact]
    public void MaxOfZero_IsACeiling()
    {
        Assert.Equal(
            Math.Pow(14.0 / 15.0, 2),
            LuckyMoneyFilterDesc.EstimateRarity(LuckyMoney([0, 1], min: 0, max: 0), Ctx),
            Tolerance
        );
        Assert.Equal(0.0, LuckyMoneyFilterDesc.EstimateRarity(LuckyMoney([0, 1], max: 0), Ctx), Tolerance);
    }

    /// <summary>An upper gate genuinely excludes: exactly one of two flips is 2·½·½.</summary>
    [Fact]
    public void MaxGate_ExcludesTheUpperTail()
    {
        var clause = new BusinessPayoutClause
        {
            Rolls = [0, 1],
            Min = 1,
            Max = 1,
        };
        Assert.Equal(0.5, BusinessPayoutFilterDesc.EstimateRarity(clause, Ctx), Tolerance);
    }

    // ── saturating luck ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Oops! All 6s multiplies the odds, and the runtime comparison is <c>random &lt; luck/chance</c>
    /// — so once that ratio reaches 1 the roll is guaranteed, not more-than-certain.
    /// </summary>
    [Fact]
    public void SaturatedLuck_IsCertaintyNotMoreThanOne()
    {
        // Glass is 1-in-4; five Oops makes the comparison `< 1.25`, i.e. always true.
        var clause = new GlassDestroyClause { Rolls = [0], With = new JamlWith { Luck = MotelyLuck.X5 } };
        Assert.Equal(1.0, GlassDestroyFilterDesc.EstimateRarity(clause, Ctx), Tolerance);
    }

    /// <summary>
    /// Saturated luck plus an upper gate is the one shape that is provably impossible while looking
    /// entirely reasonable on the page: all three rolls must fire, and the clause forbids three.
    /// </summary>
    [Fact]
    public void SaturatedLuck_UnderAMaxGate_IsImpossible()
    {
        var clause = new GlassDestroyClause
        {
            Rolls = [0, 1, 2],
            Min = 1,
            Max = 2,
            With = new JamlWith { Luck = MotelyLuck.X5 },
        };

        double p = GlassDestroyFilterDesc.EstimateRarity(clause, Ctx);

        Assert.False(double.IsNaN(p));
        Assert.Equal(0.0, p);
    }

    // ── the one non-coin-flip ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1.0)] // the whole 0–23 range clears a threshold of 0
    [InlineData(12, 12.0 / 24.0)] // 12 through 23 inclusive
    [InlineData(23, 1.0 / 24.0)] // only the top value
    [InlineData(24, 0.0)] // past the top of the range — impossible, not unknown
    public void MisprintMult_IsTheShareOfTheRangeAtOrAboveTheThreshold(int mult, double expected)
    {
        var clause = new MisprintMultClause { Rolls = [0], Mult = mult };
        Assert.Equal(expected, MisprintMultFilterDesc.EstimateRarity(clause, Ctx), Tolerance);
    }

    // ── properties of the distribution itself ──────────────────────────────────────────────────

    /// <summary>
    /// The windows partition the outcomes, so they must sum to one. This is what catches an
    /// off-by-one in the term recurrence, which no single hand-computed case reliably does.
    /// (Started at k=1 because "exactly zero" cannot be spelled with a Max of 0 — see above.)
    /// </summary>
    [Theory]
    [InlineData(1.0 / 15.0)]
    [InlineData(0.5)]
    [InlineData(1.0 / 1000.0)]
    public void ExactCountsSumToOne(double rate)
    {
        const int Trials = 6;

        double total = Math.Pow(1.0 - rate, Trials); // exactly zero
        for (int k = 1; k <= Trials; k++)
            total += JamlRollRarity.Window(Trials, k, k, rate);

        Assert.Equal(1.0, total, 1e-9);
    }

    /// <summary>A probability that leaves [0,1] is worse than no probability at all downstream.</summary>
    [Fact]
    public void EveryWindow_StaysAProbability()
    {
        foreach (double rate in new[] { 0.0, 1e-9, 1.0 / 1000.0, 1.0 / 15.0, 0.5, 1.0 })
        foreach (int trials in new[] { 0, 1, 2, 8, 52 })
        foreach (int min in new[] { 0, 1, 2, 53 })
        foreach (int? max in new int?[] { null, 0, 1, 4 })
        {
            double p = JamlRollRarity.Window(trials, min, max, rate);
            Assert.False(double.IsNaN(p), $"NaN at rate={rate} trials={trials} min={min} max={max}");
            Assert.InRange(p, 0.0, 1.0);
        }
    }

    // ── the dispatch seam ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The event families, picked out the way <see cref="IRollScopedClause"/> defines them: scoped
    /// to rolls and <em>not</em> to antes. Roll-scoped alone is not enough — tags, vouchers and
    /// booster packs carry roll indices too (tag draw 0–5, voucher draw 0–2, pack slot) while still
    /// belonging to an ante, and their odds depend on pool sizes this bite does not model.
    /// </summary>
    public static TheoryData<string> EventDiscriminators()
    {
        var data = new TheoryData<string>();
        foreach (var disc in JamlSchema.Discriminators)
        {
            if (JamlSchema.CreateClause(disc) is IRollScopedClause and not IAnteScopedClause)
                data.Add(disc);
        }
        return data;
    }

    /// <summary>
    /// Driven from the schema rather than a hand-kept list of twelve, so a thirteenth event family
    /// arriving without a rarity model fails here instead of quietly widening the "no model yet"
    /// footnote in the report.
    /// </summary>
    [Theory]
    [MemberData(nameof(EventDiscriminators))]
    public void EveryEventFamily_DeclaresItsOdds(string discriminator)
    {
        double p = JamlClauseDescDispatch.EstimateRarity(JamlSchema.CreateClause(discriminator), Ctx);

        Assert.False(double.IsNaN(p), $"{discriminator} has no rarity model");
    }

    /// <summary>Routing through the switch must land on the family, not on a lookalike arm.</summary>
    [Fact]
    public void Dispatch_RoutesToTheOwningFamily()
    {
        var clause = LuckyMoney([0]);

        Assert.Equal(
            LuckyMoneyFilterDesc.EstimateRarity(clause, Ctx),
            JamlClauseDescDispatch.EstimateRarity(clause, Ctx),
            Tolerance
        );
    }

    /// <summary>
    /// A family with no model yet reports NaN — the sentinel the report turns into "at most 1 in N,
    /// 2 of 5 clauses modelled". Anything else here would be a confident-looking lie.
    /// </summary>
    [Fact]
    public void UnmodelledFamily_IsNaN()
    {
        Assert.True(double.IsNaN(JamlClauseDescDispatch.EstimateRarity(new PokerHandClause(), Ctx)));
    }

    /// <summary>And/Or are not desc families; composing them is the caller's job, not a lookup.</summary>
    [Fact]
    public void GroupingClauses_AreNaN()
    {
        Assert.True(double.IsNaN(JamlClauseDescDispatch.EstimateRarity(new AndClause(), Ctx)));
    }
}
