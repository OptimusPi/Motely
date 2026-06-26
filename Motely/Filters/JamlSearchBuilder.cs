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

        // FilterDesc-side fallback (the loader injects no defaults): an ante-less clause means
        // "any ante" and a source-less joker/card clause means "the usual places" — fill those in
        // once here, where every must/should/mustNot clause is about to become a filter/score input.
        // Left empty, each `foreach (ante in clause.Antes)` / `if (Sources.X.Length > 0)` would
        // simply never fire and the clause would silently match nothing.
        foreach (var clause in config.Must)
            NormalizeDefaults(clause);
        foreach (var clause in config.Should)
            NormalizeDefaults(clause);
        foreach (var clause in config.MustNot)
            NormalizeDefaults(clause);

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

    // ── Default fallbacks (per the loader's "FilterDesc owns fallback" contract) ──
    private static readonly int[] DefaultAntes = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly int[] DefaultShopItems = [0, 1, 2, 3, 4, 5, 6, 7]; // 8 shop slots (reroll headroom)
    private static readonly int[] DefaultBoosterPacks = [0, 1, 2, 3, 4, 5];     // 6 packs every ante

    private static void NormalizeDefaults(IJamlClause clause)
    {
        // Logic clauses (and/or) hold no antes/sources of their own — recurse into the children.
        if (clause is LogicClause logic)
        {
            foreach (var child in logic.Clauses)
                NormalizeDefaults(child);
            return;
        }

        // An ante-targeted clause that named no ante (and inherited none from an enclosing
        // and/or) means "any ante" — fill the full 1..8 span.
        // Set antes default for all ante-targeted clause types.
        switch (clause)
        {
            case JokerClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case CommonJokerClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case UncommonJokerClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case RareJokerClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case LegendaryJokerClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case TarotCardClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case SpectralCardClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case PlanetCardClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case StandardCardClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case BossClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case TagClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case VoucherClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case StartingDrawClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case ErraticRankClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
            case ErraticSuitClause c when c.Antes.Length == 0: c.Antes = DefaultAntes; break;
        }

        // A joker/card clause that named no source at all gets the everyday shop+pack default.
        // If the user named ANY source (even a specialty one), leave their choice untouched.
        switch (clause)
        {
            case JokerClause c when JokerSourcesEmpty(c.Sources) && LegendarySourcesEmpty(c.LegendarySources):
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
            case CommonJokerClause c when JokerSourcesEmpty(c.Sources):
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
            case UncommonJokerClause c when JokerSourcesEmpty(c.Sources):
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
            case RareJokerClause c when JokerSourcesEmpty(c.Sources):
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
            case TarotCardClause c
                when c.Sources.ShopItems.Length == 0 && c.Sources.BoosterPacks.Length == 0
                    && c.Sources.Emperor.Length == 0 && c.Sources.PurpleSealOrEightBall.Length == 0:
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
            case SpectralCardClause c
                when c.Sources.ShopItems.Length == 0 && c.Sources.BoosterPacks.Length == 0
                    && c.Sources.SixthSense.Length == 0 && c.Sources.Seance.Length == 0:
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
            case PlanetCardClause c
                when c.Sources.ShopItems.Length == 0 && c.Sources.BoosterPacks.Length == 0:
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
            case StandardCardClause c
                when c.Sources.ShopItems.Length == 0 && c.Sources.BoosterPacks.Length == 0
                    && c.Sources.Certificate.Length == 0 && c.Sources.Incantation.Length == 0
                    && c.Sources.Familiar.Length == 0 && c.Sources.Grim.Length == 0
                    && c.Sources.DeckDraw.Length == 0:
                c.Sources.ShopItems = DefaultShopItems;
                c.Sources.BoosterPacks = DefaultBoosterPacks;
                break;
        }
    }

    private static bool JokerSourcesEmpty(JokerSourceConfig s) =>
        s.ShopItems.Length == 0 && s.BoosterPacks.Length == 0
        && s.Judgement.Length == 0 && s.Wraith.Length == 0 && s.RiffRaff.Length == 0
        && s.RareTag.Length == 0 && s.UncommonTag.Length == 0
        && s.CommonShopJokers.Length == 0 && s.UncommonShopJokers.Length == 0
        && s.RareShopJokers.Length == 0 && s.AllShopJokers.Length == 0;

    private static bool LegendarySourcesEmpty(LegendaryJokerSourceConfig s) =>
        s.ShopItems.Length == 0 && s.BoosterPacks.Length == 0
        && s.ArcanaPacks.Length == 0 && s.SpectralPacks.Length == 0 && s.SoulCard.Length == 0;

    private static IMotelySeedFilterDesc ClauseToFilterDesc(IJamlClause clause) =>
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
