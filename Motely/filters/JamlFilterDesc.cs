using System;

using Motely;

using System.Collections.Generic;

using System.Diagnostics;

using System.Diagnostics.CodeAnalysis;

using System.Linq;



namespace Motely.Filters;



// ── Logic combinator clauses ──



public interface IJamlClause

{

    string Label { get; init; }

    int Score { get; init; }

}



public abstract class LogicClause : IJamlClause

{

    public string Label { get; init; } = "";

    public int Score { get; init; }

}



public sealed class AndClause : LogicClause

{

    public required IJamlClause[] Clauses { get; init; }

}



public sealed class OrClause : LogicClause

{

    public required IJamlClause[] Clauses { get; init; }

    public int Min { get; init; } = 1;

}



/// <summary>

/// Holds the built search settings plus ordered labels for must and should columns.

/// </summary>

public record JamlSearchPlan(

    IMotelySearchSettings Settings,

    string[] MustLabels,

    string[] ShouldLabels,

    int ShouldClauseCount

);



/// <summary>

/// Builds MotelySearchSettings from a JamlConfig by adding one filter per clause

/// via WithAdditionalFilter. Iterates typed lists and dispatches to specific descriptors.

/// </summary>

public static class JamlSearchBuilder

{

    public static IMotelySearchSettings CreateSettings(JamlConfig config) =>

        CreatePlan(config, 0).Settings;



    public static JamlSearchPlan CreatePlan(JamlConfig config, int shouldScoreMinimumTotal = 0)

    {

        if (!config.HasAnyClauses)

            throw new InvalidOperationException("JamlConfig has no clauses.");



        var allMustDescs = new List<IMotelySeedFilterDesc>();

        // ── MUST: required filters (AND logic) ──

        var mustDescs = new List<IMotelySeedFilterDesc>();

        var mustLabels = new List<string>();

        AddDescsFromSet(mustDescs, mustLabels, config.Must, LegendaryClauseExpansion.SplitLegendaryEdition);

        allMustDescs.AddRange(mustDescs);

        var mustNotDescs = new List<IMotelySeedFilterDesc>();

        var mustNotLabels = new List<string>();

        AddDescsFromSet(mustNotDescs, mustNotLabels, config.MustNot, LegendaryClauseExpansion.None);

        for (int i = 0; i < mustNotDescs.Count; i++)

            allMustDescs.Add(new NegationFilterDesc(mustNotDescs[i]));



        Debug.Assert(

            allMustDescs.Count > 0 || !config.Should.HasAnyClauses,

            "Should-only JAML plans must provide a real base filter."

        );



        // Build settings: first must desc = base filter, rest = additional required filters

        var settings = allMustDescs.Count == 0

            ? CreateSettingsFromDesc(new PassthroughFilterDesc())

            : CreateSettingsFromDesc(allMustDescs[0]);



        // Propagate deck and stake from JamlConfig into the search settings

        settings.WithDeck(config.Deck);

        settings.WithStake(config.Stake);



        for (int i = 1; i < allMustDescs.Count; i++)

            settings.WithAdditionalFilter(allMustDescs[i]);



        // ── SHOULD: score provider (optional, contributes to seed score) ──

        string[] shouldLabelsArray = [];

        if (config.Should.HasAnyClauses)
        {
            var shouldLabels = new List<string>();
            var shouldClauses = new List<IJamlClause>();
            AddShouldScoringEntriesFromSet(shouldLabels, shouldClauses, config.Should);
            shouldLabelsArray = shouldLabels.ToArray();
            settings.WithSeedScoreProvider(
                new JamlShouldScoreDesc(shouldClauses.ToArray(), null, shouldScoreMinimumTotal)
            );
        }



        return new JamlSearchPlan(settings, mustLabels.ToArray(), shouldLabelsArray, shouldLabelsArray.Length);

    }




    private static void AddDescsFromSet(
        List<IMotelySeedFilterDesc> list,
        List<string> labels,
        JamlClauseSet set,
        LegendaryClauseExpansion legendaryExpansion
    )
    {
        var typed = new List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>();
        AddDescsFromSet(typed, set, legendaryExpansion);

        for (int i = 0; i < typed.Count; i++)
        {
            list.Add(typed[i].desc);
            labels.Add(typed[i].label);
        }
    }

    /// <summary>
    /// Whether a legendary+joker+edition clause becomes two SIMD filters (must only). Must not use
    /// <see cref="LegendaryClauseExpansion.SplitLegendaryEdition"/> — negation would change meaning.
    /// </summary>
    private enum LegendaryClauseExpansion
    {
        None,
        SplitLegendaryEdition,
    }

    /// <summary>
    /// Should-scoring only needs clause + label (filter descs are not attached to the search settings).
    /// </summary>
    private static void AddShouldScoringEntriesFromSet(
        List<string> labels,
        List<IJamlClause> clauses,
        JamlClauseSet set
    )
    {
        foreach (var c in set.OrderedClauses)
        {
            _ = CreateDesc(c);
            labels.Add(c.Label ?? "");
            clauses.Add(c);
        }
    }

    private static void AddDescsFromSet(
        List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)> list,
        JamlClauseSet set,
        LegendaryClauseExpansion legendaryExpansion
    )
    {
        foreach (var c in set.OrderedClauses)
        {
            if (
                legendaryExpansion == LegendaryClauseExpansion.SplitLegendaryEdition
                && TryExpandLegendaryEditionPipeline(c, out List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>? expanded)
            )
            {
                for (int i = 0; i < expanded.Count; i++)
                    list.Add(expanded[i]);
            }
            else
                list.Add((CreateDesc(c), c, c.Label));
        }
    }

    /// <summary>
    /// Splits "legendary joker + edition + min==1" into a fast edition vector filter followed by the
    /// full pack / The Soul / joker path. Not used for mustNot (would break negation semantics) or should.
    /// </summary>
    private static bool TryExpandLegendaryEditionPipeline(
        IJamlClause c,
        [NotNullWhen(true)] out List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>? expanded
    )
    {
        expanded = null;
        if (c is not LegendaryJokerClause lj)
            return false;
        if (
            lj.IsWildcard
            || !lj.Edition.HasValue
            || lj.Jokers.Length == 0
            || lj.Min != 1
            || lj.SoulCardOnly
        )
            return false;

        string baseLabel = c.Label ?? "";
        string labelEdition = string.IsNullOrEmpty(baseLabel)
            ? "legendary edition"
            : $"{baseLabel} [edition]";
        string labelPath = string.IsNullOrEmpty(baseLabel)
            ? "legendary soul path"
            : $"{baseLabel} [soul path]";

        expanded =
        [
            (new LegendarySoulEditionFilterDesc(lj), c, labelEdition),
            (new LegendaryJokerFilterDesc(lj, LegendaryJokerPipelineKind.FullPathOnly), c, labelPath),
        ];
        return true;
    }



    private static IMotelySearchSettings CreateSettingsFromDesc(IMotelySeedFilterDesc desc)

    {

        return desc switch

        {

            JokerFilterDesc d => new MotelySearchSettings<JokerFilterDesc.JokerFilter>(d),

            CommonJokerFilterDesc d =>

                new MotelySearchSettings<CommonJokerFilterDesc.CommonJokerFilter>(d),

            UncommonJokerFilterDesc d =>

                new MotelySearchSettings<UncommonJokerFilterDesc.UncommonJokerFilter>(d),

            RareJokerFilterDesc d => new MotelySearchSettings<RareJokerFilterDesc.RareJokerFilter>(

                d

            ),

            MixedJokerFilterDesc d =>

                new MotelySearchSettings<MixedJokerFilterDesc.MixedJokerFilter>(d),

            LegendaryJokerFilterDesc d =>

                new MotelySearchSettings<LegendaryJokerFilterDesc.LegendaryJokerFilter>(d),

            LegendarySoulEditionFilterDesc d =>

                new MotelySearchSettings<LegendarySoulEditionFilterDesc.LegendarySoulEditionFilter>(d),

            VoucherFilterDesc d => new MotelySearchSettings<VoucherFilterDesc.VoucherFilter>(d),

            TarotCardFilterDesc d => new MotelySearchSettings<TarotCardFilterDesc.TarotCardFilter>(

                d

            ),

            SpectralCardFilterDesc d =>

                new MotelySearchSettings<SpectralCardFilterDesc.SpectralCardFilter>(d),

            PlanetCardFilterDesc d =>

                new MotelySearchSettings<PlanetCardFilterDesc.PlanetCardFilter>(d),

            BossFilterDesc d => new MotelySearchSettings<BossFilterDesc.BossFilter>(d),

            TagFilterDesc d => new MotelySearchSettings<TagFilterDesc.TagFilter>(d),

            StandardCardFilterDesc d =>

                new MotelySearchSettings<StandardCardFilterDesc.StandardCardFilter>(d),

            ErraticRankFilterDesc d =>

                new MotelySearchSettings<ErraticRankFilterDesc.ErraticRankFilter>(d),

            ErraticSuitFilterDesc d =>

                new MotelySearchSettings<ErraticSuitFilterDesc.ErraticSuitFilter>(d),

            ErraticCardFilterDesc d =>

                new MotelySearchSettings<ErraticCardFilterDesc.ErraticCardFilter>(d),

            LuckyMoneyFilterDesc d =>

                new MotelySearchSettings<LuckyMoneyFilterDesc.LuckyMoneyFilter>(d),

            LuckyMultFilterDesc d => new MotelySearchSettings<LuckyMultFilterDesc.LuckyMultFilter>(

                d

            ),

            MisprintMultFilterDesc d =>

                new MotelySearchSettings<MisprintMultFilterDesc.MisprintMultFilter>(d),

            WheelOfFortuneFilterDesc d =>

                new MotelySearchSettings<WheelOfFortuneFilterDesc.WheelOfFortuneFilter>(d),

            CavendishExtinctFilterDesc d =>

                new MotelySearchSettings<CavendishExtinctFilterDesc.CavendishExtinctFilter>(d),

            GrosMichelExtinctFilterDesc d =>

                new MotelySearchSettings<GrosMichelExtinctFilterDesc.GrosMichelExtinctFilter>(d),

            AndFilterDesc d => new MotelySearchSettings<AndFilterDesc.AndFilter>(d),

            OrFilterDesc d => new MotelySearchSettings<OrFilterDesc.OrFilter>(d),

            NegationFilterDesc d => new MotelySearchSettings<NegationFilterDesc.NegationFilter>(d),

            PassthroughFilterDesc d =>

                new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(d),

            StartingDrawFilterDesc d =>

                new MotelySearchSettings<StartingDrawFilterDesc.StartingDrawFilter>(d),

            NegativePerkeoSimdFilterDesc d =>

                new MotelySearchSettings<NegativePerkeoSimdFilterDesc.FilterStruct>(d),

            NegativePerkeoShopSoulFilterDesc d =>

                new MotelySearchSettings<NegativePerkeoShopSoulFilterDesc.FilterStruct>(d),

            NegativePerkeoFilterDescNew d =>

                new MotelySearchSettings<NegativePerkeoFilterDescNew.FilterStruct>(d),

            _ => throw new NotSupportedException(

                $"Unknown filter desc type: {desc.GetType().Name}"

            ),

        };

    }



    private static IMotelySeedFilterDesc CreateDesc(object clause)

    {

        // Dispatch typed clauses to their descriptors

        return clause switch

        {

            JokerClause c => new JokerFilterDesc(c),

            CommonJokerClause c => new CommonJokerFilterDesc(c),

            UncommonJokerClause c => new UncommonJokerFilterDesc(c),

            RareJokerClause c => new RareJokerFilterDesc(c),

            MixedJokerClause c => new MixedJokerFilterDesc(c),

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

            ErraticCardClause c => new ErraticCardFilterDesc(c),

            LuckyMoneyClause c => new LuckyMoneyFilterDesc(c),

            LuckyMultClause c => new LuckyMultFilterDesc(c),

            MisprintMultClause c => new MisprintMultFilterDesc(c),

            WheelOfFortuneClause c => new WheelOfFortuneFilterDesc(c),

            CavendishExtinctClause c => new CavendishExtinctFilterDesc(c),

            GrosMichelExtinctClause c => new GrosMichelExtinctFilterDesc(c),

            StartingDrawClause c => new StartingDrawFilterDesc(c),

            AndClause c => new AndFilterDesc(c.Clauses.Select(CreateDesc).ToArray()),

            OrClause c => new OrFilterDesc(c.Clauses.Select(CreateDesc).ToArray(), c.Min),

            _ => throw new NotSupportedException($"Unknown clause type: {clause.GetType().Name}"),

        };

    }

}

