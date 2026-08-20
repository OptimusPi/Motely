using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// Composition is where an honest per-clause number can still become a dishonest total. The
/// invariant these tests defend is the one the report's wording rests on: a share that omits an
/// unmodelled clause is an <em>upper bound</em>, never an under-estimate, and never NaN.
/// </summary>
public sealed class JamlRarityEstimatorTests
{
    private const double Tolerance = 1e-12;

    private const double LuckyMoney = 1.0 / 15.0;
    private const double Flip = 0.5;

    private static LuckyMoneyClause Lucky() => new() { Rolls = [0] };

    private static BusinessPayoutClause Business() => new() { Rolls = [0] };

    private static JamlConfig Config(
        List<IJamlClause>? must = null,
        List<IJamlClause>? mustNot = null,
        List<IJamlClause>? should = null,
        string? filter = null
    ) =>
        new()
        {
            Id = "rarity-test",
            Must = must ?? [],
            MustNot = mustNot ?? [],
            Should = should ?? [],
            Filter = filter,
        };

    // ── the shape of a total ───────────────────────────────────────────────────────────────────

    [Fact]
    public void OneModelledClause_IsThatClausesShare()
    {
        var estimate = JamlRarityEstimator.Estimate(Config(must: [Lucky()]));

        Assert.Equal(LuckyMoney, estimate.PassShare, Tolerance);
        Assert.Equal(1, estimate.KnownClauses);
        Assert.Equal(0, estimate.UnknownClauses);
        Assert.True(estimate.IsComplete);
    }

    [Fact]
    public void SeveralMustClauses_Multiply()
    {
        var estimate = JamlRarityEstimator.Estimate(Config(must: [Lucky(), Business()]));

        Assert.Equal(LuckyMoney * Flip, estimate.PassShare, Tolerance);
        Assert.Equal(2, estimate.KnownClauses);
    }

    /// <summary>A mustNot keeps the seeds the clause does <em>not</em> describe.</summary>
    [Fact]
    public void MustNot_Inverts()
    {
        var estimate = JamlRarityEstimator.Estimate(Config(mustNot: [Lucky()]));

        Assert.Equal(1.0 - LuckyMoney, estimate.PassShare, Tolerance);
        Assert.Equal(1, estimate.KnownClauses);
    }

    /// <summary>
    /// Forbidding something certain leaves nothing, and that is a modelled zero the report prints
    /// as "impossible" — the case worth catching before a nine-hour sweep, not after.
    /// </summary>
    [Fact]
    public void MustNot_OfACertainty_IsImpossible()
    {
        var certain = new GlassDestroyClause
        {
            Rolls = [0],
            With = new JamlWith { Luck = MotelyLuck.X5 },
        };

        var estimate = JamlRarityEstimator.Estimate(Config(mustNot: [certain]));

        Assert.Equal(0.0, estimate.PassShare);
        Assert.False(double.IsNaN(estimate.PassShare));
    }

    /// <summary>Should clauses move a score, not a gate; counting them would overstate the rarity.</summary>
    [Fact]
    public void ShouldClauses_DoNotGate()
    {
        var estimate = JamlRarityEstimator.Estimate(Config(must: [Lucky()], should: [Business()]));

        Assert.Equal(LuckyMoney, estimate.PassShare, Tolerance);
        Assert.Equal(1, estimate.KnownClauses);
    }

    // ── coverage honesty ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The invariant the whole report rests on: an unmodelled clause is skipped and counted, so the
    /// share stays a number and stays above the truth rather than becoming NaN.
    /// </summary>
    [Fact]
    public void UnmodelledClause_IsSkippedAndCountedNotGuessed()
    {
        var estimate = JamlRarityEstimator.Estimate(
            Config(must: [Lucky(), new PokerHandClause()])
        );

        Assert.Equal(LuckyMoney, estimate.PassShare, Tolerance);
        Assert.Equal(1, estimate.KnownClauses);
        Assert.Equal(1, estimate.UnknownClauses);
        Assert.Equal(["pokerHand"], estimate.UnknownFamilies);
        Assert.False(estimate.IsComplete);
    }

    /// <summary>A config nobody can model yet reports nothing rather than 1.0.</summary>
    [Fact]
    public void NothingModelled_IsEmpty()
    {
        var estimate = JamlRarityEstimator.Estimate(Config(must: [new PokerHandClause()]));

        Assert.True(estimate.IsEmpty);
    }

    /// <summary>A native filter is a gate the model cannot see, so the share can only be a bound.</summary>
    [Fact]
    public void NativeFilter_ForfeitsCompleteness()
    {
        var estimate = JamlRarityEstimator.Estimate(Config(must: [Lucky()], filter: "SomeNativeFilter"));

        Assert.True(estimate.HasNativeFilter);
        Assert.False(estimate.IsComplete);
    }

    // ── groupings ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An <c>and</c> under <c>must</c> is just more must-clauses, and flattening keeps its arms
    /// separately attributable — two known clauses here, not one opaque group.
    /// </summary>
    [Fact]
    public void AndUnderMust_FlattensIntoTheProduct()
    {
        var group = new AndClause { Clauses = [Lucky(), Business()] };
        var estimate = JamlRarityEstimator.Estimate(Config(must: [group]));

        Assert.Equal(LuckyMoney * Flip, estimate.PassShare, Tolerance);
        Assert.Equal(2, estimate.KnownClauses);
    }

    /// <summary>Flattening must not swallow the modelled arms of a partly modelled group.</summary>
    [Fact]
    public void AndUnderMust_KeepsModelledArmsWhenOneArmIsNot()
    {
        var group = new AndClause { Clauses = [Lucky(), new PokerHandClause()] };
        var estimate = JamlRarityEstimator.Estimate(Config(must: [group]));

        Assert.Equal(LuckyMoney, estimate.PassShare, Tolerance);
        Assert.Equal(1, estimate.KnownClauses);
        Assert.Equal(1, estimate.UnknownClauses);
    }

    /// <summary>Arms at different rates, so this is a Poisson binomial: 1 − (14/15)(1/2).</summary>
    [Fact]
    public void OrOfOne_IsTheChanceAnyArmMatches()
    {
        var group = new OrClause { Clauses = [Lucky(), Business()], Min = 1 };
        var estimate = JamlRarityEstimator.Estimate(Config(must: [group]));

        Assert.Equal(1.0 - (1.0 - LuckyMoney) * (1.0 - Flip), estimate.PassShare, Tolerance);
    }

    /// <summary>
    /// <c>or:</c>'s gate is a count, not "any" — with min 2 the familiar 1−Π(1−p) is simply the
    /// wrong formula, and both arms must land.
    /// </summary>
    [Fact]
    public void OrOfTwo_RequiresBothArms()
    {
        var group = new OrClause { Clauses = [Lucky(), Business()], Min = 2 };
        var estimate = JamlRarityEstimator.Estimate(Config(must: [group]));

        Assert.Equal(LuckyMoney * Flip, estimate.PassShare, Tolerance);
    }

    /// <summary>
    /// An <c>or</c> cannot drop an unmodelled arm the way the top level can: fewer arms make an
    /// <c>or</c> <em>less</em> likely, so a partial answer would sit below the truth and break the
    /// bound. The whole group goes unknown instead.
    /// </summary>
    [Fact]
    public void OrWithAnUnmodelledArm_IsWhollyUnknown()
    {
        var group = new OrClause { Clauses = [Lucky(), new PokerHandClause()], Min = 1 };
        var estimate = JamlRarityEstimator.Estimate(Config(must: [group]));

        Assert.Equal(0, estimate.KnownClauses);
        Assert.Equal(1, estimate.UnknownClauses);
        Assert.Equal(["or"], estimate.UnknownFamilies);
    }

    /// <summary>Groups nest, and a negated group is one factor rather than a flattening.</summary>
    [Fact]
    public void MustNot_OfAGroup_InvertsTheWholeGroup()
    {
        var group = new AndClause { Clauses = [Lucky(), Business()] };
        var estimate = JamlRarityEstimator.Estimate(Config(mustNot: [group]));

        Assert.Equal(1.0 - LuckyMoney * Flip, estimate.PassShare, Tolerance);
        Assert.Equal(1, estimate.KnownClauses);
    }

    // ── invariants ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Skipping an unmodelled clause may only raise the share. Asserted against the same config
    /// with the clause replaced by a modelled one: the bound must sit at or above the truth.
    /// </summary>
    [Fact]
    public void OmittingAnUnknown_OnlyEverRaisesTheShare()
    {
        double bound = JamlRarityEstimator
            .Estimate(Config(must: [Lucky(), new PokerHandClause()]))
            .PassShare;

        double ifItHadBeenModelled = JamlRarityEstimator
            .Estimate(Config(must: [Lucky(), Business()]))
            .PassShare;

        Assert.True(bound >= ifItHadBeenModelled, $"bound {bound} sits below truth {ifItHadBeenModelled}");
    }

    /// <summary>A NaN total tells a caller nothing; a bound plus a count tells it everything.</summary>
    [Fact]
    public void PassShare_IsNeverNaN()
    {
        foreach (var config in new[]
        {
            Config(),
            Config(must: [new PokerHandClause()]),
            Config(must: [Lucky()], mustNot: [new PokerHandClause()]),
            Config(must: [new AndClause()]),
            Config(must: [new OrClause { Clauses = [] }]),
        })
        {
            var estimate = JamlRarityEstimator.Estimate(config);
            Assert.False(double.IsNaN(estimate.PassShare));
            Assert.InRange(estimate.PassShare, 0.0, 1.0);
        }
    }

    /// <summary>
    /// The family names in the report are read off the type name, which is a convention rather than
    /// a declaration. This is what keeps that convention true: every wire name in the schema must
    /// round-trip back to the same clause type.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllDiscriminators))]
    public void WireName_RoundTripsThroughTheSchema(string discriminator)
    {
        var clause = JamlSchema.CreateClause(discriminator);
        string derived = JamlRarityEstimator.WireName(clause);

        Assert.True(JamlSchema.IsKnownDiscriminator(derived), $"{derived} is not a wire name");
        Assert.IsType(clause.GetType(), JamlSchema.CreateClause(derived));
    }

    public static TheoryData<string> AllDiscriminators()
    {
        var data = new TheoryData<string>();
        foreach (var disc in JamlSchema.Discriminators)
            data.Add(disc);
        return data;
    }
}
