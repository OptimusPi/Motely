namespace Motely.Tests;

/// <summary>
/// Pins the SIMD cost-model laws in <see cref="JamlCostModelSimdExtensions"/>: a flat clause
/// costs its ante span (empty span = all 8), logic clauses cost the worst-case sum of their
/// children, the batch cost amortizes across the vector lanes, and CheapestFirst is a stable
/// cheapest-first ordering. Estimates never change results — these tests keep them honest
/// arithmetic instead of drifting folklore.
/// </summary>
public sealed class JamlCostModelTests
{
    [Fact]
    public void FlatClause_CostsItsAnteSpan()
    {
        Assert.Equal(3, new JokerClause { IsWildcard = true, Antes = [1, 2, 3] }.EstimateCrunches());
        Assert.Equal(1, new JokerClause { IsWildcard = true, Antes = [4] }.EstimateCrunches());
    }

    [Fact]
    public void FlatClause_EmptyAntes_NormalizesToAllEight()
    {
        Assert.Equal(8, new JokerClause { IsWildcard = true, Antes = [] }.EstimateCrunches());
    }

    [Fact]
    public void LogicClause_CostsSumOfChildren()
    {
        var or = new OrClause
        {
            Clauses =
            [
                new JokerClause { IsWildcard = true, Antes = [1, 2] },
                new TagClause { Tags = [MotelyTag.RareTag], Rolls = [0], Antes = [1, 2, 3] },
            ],
        };
        Assert.Equal(5, or.EstimateCrunches());

        var nested = new AndClause
        {
            Clauses = [or, new VoucherClause { Vouchers = [MotelyVoucher.Overstock], Rolls = [0], Antes = [1] }],
        };
        Assert.Equal(6, nested.EstimateCrunches());
    }

    [Fact]
    public void EmptyLogicClause_StillCostsOneCrunch()
    {
        Assert.Equal(1, new AndClause().EstimateCrunches());
    }

    [Fact]
    public void Config_SumsMustAndMustNot_FloorOne()
    {
        var config = new JamlConfig { Id = "cost", Deck = MotelyDeck.Red, Stake = MotelyStake.White };
        Assert.Equal(1, config.EstimateFilterCrunches());

        config.Must.Add(new JokerClause { IsWildcard = true, Antes = [1, 2] });
        config.MustNot.Add(new TagClause { Tags = [MotelyTag.RareTag], Rolls = [0], Antes = [3] });
        Assert.Equal(3, config.EstimateFilterCrunches());
    }

    [Fact]
    public void SimdCost_AmortizesAcrossLanes()
    {
        var config = new JamlConfig { Id = "cost", Deck = MotelyDeck.Red, Stake = MotelyStake.White };
        config.Must.Add(new JokerClause { IsWildcard = true, Antes = [1, 2, 3, 4] });

        Assert.Equal(4.0 / MotelyGlobals.MaxVectorWidth, config.SimdCostPerSeed());
        Assert.Equal(
            1000.0 / 4 * MotelyGlobals.MaxVectorWidth,
            config.ProjectedSeedsPerSecond(1000.0)
        );
        Assert.Equal(
            4.0 / MotelyGlobals.MaxVectorWidth,
            ((IJamlClause)config.Must[0]).SimdProjectedCost()
        );
    }

    [Fact]
    public void CheapestFirst_OrdersByCost_StableForTies()
    {
        IJamlClause wide = new JokerClause { IsWildcard = true, Antes = [1, 2, 3, 4, 5] };
        IJamlClause narrowA = new JokerClause { IsWildcard = true, Antes = [1], Label = "A" };
        IJamlClause narrowB = new JokerClause { IsWildcard = true, Antes = [2], Label = "B" };

        IReadOnlyList<IJamlClause> clauses = [wide, narrowA, narrowB];
        var ordered = clauses.CheapestFirst().ToArray();

        Assert.Equal(new[] { "A", "B" }, ordered.Take(2).Select(c => c.Label ?? "").ToArray());
        Assert.Same(wide, ordered[2]);
    }

    [Fact]
    public void CreateSettings_WiresMustAndMustNot_CheapestFirst()
    {
        // wide must (5 antes) then narrow mustNot (1 ante) → filter chain installs narrow first.
        var wide = new JokerClause { IsWildcard = true, Antes = [1, 2, 3, 4, 5], Label = "wide" };
        var narrow = new TagClause
        {
            Tags = [MotelyTag.RareTag],
            Rolls = [0],
            Antes = [1],
            Label = "narrow",
        };

        var config = new JamlConfig
        {
            Id = "cheap-first",
            Deck = MotelyDeck.Red,
            Stake = MotelyStake.White,
        };
        config.Must.Add(wide);
        config.MustNot.Add(narrow);

        var settings = JamlSearchBuilder.CreateSettings(config);
        Assert.NotNull(settings.AdditionalFilters);
        Assert.Equal(2, settings.AdditionalFilters!.Count);

        // First filter is mustNot (NegationFilterDesc wrapping Tag); second is must (Joker).
        Assert.IsType<NegationFilterDesc>(settings.AdditionalFilters[0]);
        Assert.IsType<JokerFilterDesc>(settings.AdditionalFilters[1]);
    }
}
