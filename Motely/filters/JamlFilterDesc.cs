using System;

using System.Collections.Generic;

using System.Diagnostics;

using System.Linq;

using Motely;



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

        CreatePlan(config).Settings;



    public static JamlSearchPlan CreatePlan(JamlConfig config)

    {

        if (!config.HasAnyClauses)

            throw new InvalidOperationException("JamlConfig has no clauses.");



        // ── MUST: required filters (AND logic) ──

        var mustDescs = new List<IMotelySeedFilterDesc>();

        var mustLabels = new List<string>();

        AddDescsFromSet(mustDescs, mustLabels, config.Must);



        Debug.Assert(

            mustDescs.Count > 0 || !config.Should.HasAnyClauses,

            "Should-only JAML plans must provide a real base filter."

        );



        if (mustDescs.Count == 0)

            throw new InvalidOperationException("JamlConfig produced no filter descriptors.");



        // Build settings: first must desc = base filter, rest = additional required filters

        var settings = CreateSettingsFromDesc(mustDescs[0]);



        // Propagate deck and stake from JamlConfig into the search settings

        settings.WithDeck(config.Deck);

        settings.WithStake(config.Stake);



        for (int i = 1; i < mustDescs.Count; i++)

            settings.WithAdditionalFilter(mustDescs[i]);



        // ── SHOULD: score provider (optional, contributes to seed score) ──

        string[] shouldLabelsArray = [];

        if (config.Should.HasAnyClauses)
        {
            var shouldDescs = new List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>();
            AddDescsFromSet(shouldDescs, config.Should);

            (IMotelySeedFilterDesc desc, int score, string label)[] scored = shouldDescs
                .Select(x => (x.desc, x.clause.Score, x.label))
                .ToArray();

            (IMotelySeedFilterDesc desc, string label)[] mustArr = mustDescs
                .Zip(mustLabels, (desc, label) => (desc, label))
                .ToArray();

            settings.WithSeedScoreProvider(new JamlShouldScoreDesc(mustArr, scored));

            shouldLabelsArray = shouldDescs.Select(x => x.label).ToArray();
        }



        return new JamlSearchPlan(settings, mustLabels.ToArray(), shouldLabelsArray, shouldLabelsArray.Length);

    }



    private static void AddDescsFromSet(List<IMotelySeedFilterDesc> descs, List<string> labels, JamlClauseSet set)
    {
        var list = new List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>();
        AddDescsFromSet(list, set);
        foreach (var item in list)
        {
            descs.Add(item.desc);
            labels.Add(item.label);
        }
    }

    private static void AddDescsFromSet(
        List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)> list,
        JamlClauseSet set
    )
    {

        foreach (var c in set.Jokers)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.CommonJokers)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.UncommonJokers)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.RareJokers)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.MixedJokers)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.LegendaryJokers)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.Vouchers)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.TarotCards)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.SpectralCards)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.PlanetCards)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.Bosses)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.Tags)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.StandardCards)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.ErraticRanks)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.ErraticSuits)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.ErraticCards)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.LuckyMoney)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.LuckyMult)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.MisprintMult)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.WheelOfFortune)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.CavendishExtinct)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.GrosMichelExtinct)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

        foreach (var c in set.StartingDraw)
        {
            list.Add((CreateDesc(c), c, c.Label));
        }

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

            PassthroughFilterDesc d =>

                new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(d),

            StartingDrawFilterDesc d =>

                new MotelySearchSettings<StartingDrawFilterDesc.StartingDrawFilter>(d),

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

            _ => throw new NotSupportedException($"Unknown clause type: {clause.GetType().Name}"),

        };

    }

}

