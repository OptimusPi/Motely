using System;
using System.Collections.Generic;
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
    string[] ShouldLabels
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

        // If no must but we have should, use a passthrough base filter
        if (mustDescs.Count == 0 && config.Should.HasAnyClauses)
        {
            mustDescs.Add(new PassthroughFilterDesc());
            mustLabels.Add("(passthrough)");
        }

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
            var shouldDescs = new List<IMotelySeedFilterDesc>();
            var shouldLabels = new List<string>();
            AddDescsFromSet(shouldDescs, shouldLabels, config.Should);

            (IMotelySeedFilterDesc desc, int score, string label)[] scored = shouldDescs
                .Zip(shouldLabels, (desc, label) => (desc, 1, label))
                .ToArray();
            (IMotelySeedFilterDesc desc, string label)[] mustArr = mustDescs
                .Zip(mustLabels, (desc, label) => (desc, label))
                .ToArray();
            settings.WithSeedScoreProvider(new JamlShouldScoreDesc(mustArr, scored));
            shouldLabelsArray = shouldLabels.ToArray();
        }

        return new JamlSearchPlan(settings, mustLabels.ToArray(), shouldLabelsArray);
    }

    private static void AddDescsFromSet(
        List<IMotelySeedFilterDesc> descs,
        List<string> labels,
        JamlClauseSet set
    )
    {
        foreach (var c in set.Jokers)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.CommonJokers)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.UncommonJokers)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.RareJokers)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.MixedJokers)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.LegendaryJokers)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.Vouchers)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.TarotCards)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.SpectralCards)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.PlanetCards)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.Bosses)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.Tags)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.StandardCards)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.ErraticRanks)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.ErraticSuits)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.ErraticCards)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.LuckyMoney)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.LuckyMult)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.MisprintMult)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.WheelOfFortune)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.CavendishExtinct)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.GrosMichelExtinct)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
        }
        foreach (var c in set.StartingDraw)
        {
            descs.Add(CreateDesc(c));
            labels.Add(c.Label);
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
