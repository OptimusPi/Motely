using System;
using System.Linq;
using Motely.Filters.Jaml;
using Motely.Filters.Native;

namespace Motely.Filters;

public sealed class JamlSearchPlan
{
    internal IMotelySearchSettings Settings { get; set; } = null!;
    public int ScoreTallyColumnCount { get; set; }
    public IReadOnlyList<string> TallyLabels { get; set; } = [];
}

public static class JamlSearchBuilder
{
    public static JamlSearchPlan CreatePlan(JamlConfig config, int engineCutoff = 0)
    {
        var settings = CreateSettings(config, engineCutoff);
        return new JamlSearchPlan
        {
            Settings = settings,
            ScoreTallyColumnCount = config.Should.Count,
            TallyLabels = [.. config.Should.Select((c, i) => c.Label ?? $"score{i}")],
        };
    }

    public static IMotelySearchSettings CreateSettings(JamlConfig config, int engineCutoff = 0)
    {
        if (!config.HasAnyClauses())
            throw new InvalidOperationException(
                $"JAML filter '{config.Id}' has no must/should/mustNot clauses."
            );

        IMotelySearchSettings settings = new MotelySearchSettings<
            PassthroughFilterDesc.PassthroughFilter
        >(new PassthroughFilterDesc());

        foreach (var clause in config.Must)
            settings = settings.WithAdditionalFilter(ClauseToFilterDesc(clause));

        foreach (var clause in config.MustNot)
            settings = settings.WithAdditionalFilter(
                new NegationFilterDesc(ClauseToFilterDesc(clause))
            );

        if (config.Must.Count + config.Should.Count > 0)
        {
            settings = settings.WithSeedScoreProvider(
                new JamlShouldScoreDesc(
                    [.. config.Must],
                    [.. config.Should],
                    minimumTotalScore: engineCutoff
                )
            );
        }

        return settings;
    }

    private static IMotelySeedFilterDesc ClauseToFilterDesc(JamlClauseBase clause) =>
        clause switch
        {
            JokerClause c => new JokerFilterDesc(c),
            CommonJokerClause c => new CommonJokerFilterDesc(c),
            UncommonJokerClause c => new UncommonJokerFilterDesc(c),
            RareJokerClause c => new RareJokerFilterDesc(c),
            LegendaryJokerClause c => new LegendaryJokerFilterDesc(c),
            VoucherClause c => new VoucherFilterDesc(c),
            TarotCardClause c => new TarotCardFilterDesc(c),
            SpectralCardClause c => new SpectralCardFilterDesc(c),
            PlanetCardClause c => new PlanetCardFilterDesc(c),
            BossClause c => new BossFilterDesc(c),
            TagClause c => new TagFilterDesc(c),
            StandardCardClause c => new StandardCardFilterDesc(c),
            ErraticRankClause c => new ErraticRankFilterDesc(c),
            ErraticSuitClause c => new ErraticSuitFilterDesc(c),
            LuckyMoneyClause c => new LuckyMoneyFilterDesc(c),
            LuckyMultClause c => new LuckyMultFilterDesc(c),
            MisprintMultClause c => new MisprintMultFilterDesc(c),
            WheelOfFortuneClause c => new WheelOfFortuneFilterDesc(c),
            CavendishExtinctClause c => new CavendishExtinctFilterDesc(c),
            GrosMichelExtinctClause c => new GrosMichelExtinctFilterDesc(c),
            SpaceLevelupClause c => new SpaceLevelupFilterDesc(c),
            BusinessPayoutClause c => new BusinessPayoutFilterDesc(c),
            BloodstoneTriggerClause c => new BloodstoneTriggerFilterDesc(c),
            ParkingPayoutClause c => new ParkingPayoutFilterDesc(c),
            GlassDestroyClause c => new GlassDestroyFilterDesc(c),
            WheelStaysFlippedClause c => new WheelStaysFlippedFilterDesc(c),
            StartingDrawClause c => new StartingDrawFilterDesc(c),
            AndClause c => new AndFilterDesc(c.Clauses.Select(ClauseToFilterDesc).ToArray()),
            OrClause c => new OrFilterDesc(c.Clauses.Select(ClauseToFilterDesc).ToArray(), c.Min),
            _ => throw new InvalidOperationException(
                $"JamlSearchBuilder: clause '{clause.GetType().Name}' is not supported in the SIMD filter pass."
            ),
        };
}
