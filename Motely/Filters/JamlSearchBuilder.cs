using System;

using Motely;
using Motely.Filters.Native;

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

    int Min { get; init; }

    int? Max { get; init; }

}



public abstract class LogicClause : IJamlClause

{

    public string Label { get; init; } = "";

    public int Score { get; init; }

    public int Min { get; init; } = 1;

    public int? Max { get; init; }

}



public sealed class AndClause : LogicClause

{

    public required IJamlClause[] Clauses { get; init; }

}



public sealed class OrClause : LogicClause

{

    public required IJamlClause[] Clauses { get; init; }

    public new int Min { get; init; } = 1;

}



/// <summary>Compiled JAML: runnable settings plus tally width for sinks (matches scoring clause count).</summary>
public sealed record JamlSearchPlan(
    IMotelySearchSettings Settings,
    int ScoreTallyColumnCount,
    /// <summary>RFC-4180 style header line: quoted fields, comma-separated. Empty when <see cref="ScoreTallyColumnCount"/> is 0.</summary>
    string ScoredCsvHeaderQuoted,
    /// <summary>Authoritative tally column labels in evaluation order (must clauses first, then should).</summary>
    string[] TallyLabels
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

        if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)

            throw new InvalidOperationException("JamlConfig has no clauses.");

        ValidateSoulJokerClausesForMustAndShould(config.Must);
        ValidateSoulJokerClausesForMustAndShould(config.Should);

        var allMustDescs = new List<IMotelySeedFilterDesc>();

        // ── MUST: required filters (AND logic) ──

        var mustDescs = new List<IMotelySeedFilterDesc>();

        AddDescsFromSet(mustDescs, config.Must, LegendaryClauseExpansion.SplitLegendaryEdition);

        allMustDescs.AddRange(mustDescs);

        var mustNotDescs = new List<IMotelySeedFilterDesc>();

        AddDescsFromSet(mustNotDescs, config.MustNot, LegendaryClauseExpansion.None);

        for (int i = 0; i < mustNotDescs.Count; i++)

            allMustDescs.Add(new NegationFilterDesc(mustNotDescs[i]));



        // Should-only plans are valid: PassthroughFilterDesc is the base, score provider does the work.



        // Build settings: first must desc = base filter, rest = additional required filters

        var settings = allMustDescs.Count == 0

            ? CreateSettingsFromDesc(new PassthroughFilterDesc())

            : CreateSettingsFromDesc(allMustDescs[0]);



        // Propagate deck and stake from JamlConfig into the search settings

        settings.WithDeck(config.Deck);

        settings.WithStake(config.Stake);



        for (int i = 1; i < allMustDescs.Count; i++)

            settings.WithAdditionalFilter(allMustDescs[i]);



        // ── Scoring: must clauses first (for early-exit), then should clauses

        var shouldClauses = new List<IJamlClause>();
        AddShouldScoringEntriesFromSet(shouldClauses, config.Must);
        int mustClauseCount = shouldClauses.Count;
        AddShouldScoringEntriesFromSet(shouldClauses, config.Should);
        settings.WithSeedScoreProvider(
            new JamlShouldScoreDesc(shouldClauses.ToArray(), null, shouldScoreMinimumTotal, mustClauseCount)
        );

        // Emit only should-clause columns in the CSV header and tally labels; must-clause
        // tallies gate execution internally but no longer appear in outputs (was debug).
        var shouldOnlyClauses = shouldClauses.Skip(mustClauseCount).ToList();
        int shouldOnlyCount = shouldOnlyClauses.Count;

        string headerQuoted = shouldOnlyCount > 0
            ? BuildScoredCsvHeaderQuoted(shouldOnlyClauses)
            : "";

        var tallyLabels = shouldOnlyCount > 0
            ? shouldOnlyClauses.Select((c, i) => string.IsNullOrWhiteSpace(c.Label) ? $"tally_{i}" : c.Label).ToArray()
            : [];

        return new JamlSearchPlan(settings, shouldOnlyCount, headerQuoted, tallyLabels);
    }

    /// <summary>
    /// Runs the same structural checks as <see cref="CreatePlan"/> (impossible soul-joker booster slots, empty config, etc.)
    /// without retaining the plan. Call after <see cref="JamlConfigLoader.TryLoad"/> so WASM/CLI validation matches what search uses.
    /// No-op when <see cref="JamlConfig.HasAnyClauses"/> is false.
    /// </summary>
    public static void EnsureRunnablePlan(JamlConfig config)
    {
        if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)
            return;
        _ = CreatePlan(config);
    }

    /// <summary>
    /// Fails fast on soul joker clauses whose booster sources can never hit arcana/spectral at slot ≥1
    /// (<see cref="JamlSoulJokerStructuralValidation"/>). Skips <c>mustNot</c>: negated dead clauses are vacuously true.
    /// </summary>
    private static void ValidateSoulJokerClausesForMustAndShould(JamlClauseSet set)
    {
        foreach (IJamlClause c in set.OrderedClauses)
            ValidateClauseTreeForSoulJoker(c);
    }

    private static void ValidateClauseTreeForSoulJoker(IJamlClause c)
    {
        switch (c)
        {
            case LegendaryJokerClause lj:
                JamlSoulJokerStructuralValidation.ValidateLegendaryJokerClauseOrThrow(lj);
                return;
            case AndClause and:
                for (int i = 0; i < and.Clauses.Length; i++)
                    ValidateClauseTreeForSoulJoker(and.Clauses[i]);
                return;
            case OrClause or:
                for (int i = 0; i < or.Clauses.Length; i++)
                    ValidateClauseTreeForSoulJoker(or.Clauses[i]);
                return;
            default:
                return;
        }
    }

    /// <summary>Quoted CSV header for stdout sinks: <c>seed</c>, <c>score</c>, then each should-scoring clause label (or <c>tally_i</c>). Built once per plan.</summary>
    public static string BuildScoredCsvHeaderQuoted(IReadOnlyList<IJamlClause> shouldClauses)
    {
        int n = shouldClauses.Count;
        var parts = new string[2 + n];
        parts[0] = CsvQuoteField("seed");
        parts[1] = CsvQuoteField("score");
        for (int i = 0; i < n; i++)
        {
            string col = shouldClauses[i].Label;
            parts[2 + i] = CsvQuoteField(string.IsNullOrWhiteSpace(col) ? $"tally_{i}" : col);
        }

        return string.Join(",", parts);
    }

    private static string CsvQuoteField(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";




    private static void AddDescsFromSet(
        List<IMotelySeedFilterDesc> list,
        JamlClauseSet set,
        LegendaryClauseExpansion legendaryExpansion
    )
    {
        var typed = new List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>();
        AddDescsFromSet(typed, set, legendaryExpansion);

        for (int i = 0; i < typed.Count; i++)
            list.Add(typed[i].desc);
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

    /// <summary>Collects clauses for <see cref="JamlShouldScoreDesc"/> (validates each via <see cref="CreateDesc"/>).</summary>
    private static void AddShouldScoringEntriesFromSet(List<IJamlClause> clauses, JamlClauseSet set)
    {
        foreach (var c in set.OrderedClauses)
        {
            _ = CreateDesc(c);
            clauses.Add(c);
        }
    }

    private static void AddDescsFromSet(
        List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)> list,
        JamlClauseSet set,
        LegendaryClauseExpansion legendaryExpansion
    )
    {
        // Merge consecutive voucher clauses into a single MultiVoucherFilterDesc so the
        // voucher PRNG state is built only once instead of once per clause.
        var voucherClauses = set.OrderedClauses.OfType<VoucherClause>().ToArray();
        IMotelySeedFilterDesc? mergedVoucher = voucherClauses.Length > 1
            ? new MultiVoucherFilterDesc(voucherClauses)
            : null;
        bool mergedVoucherEmitted = false;

        foreach (var c in set.OrderedClauses)
        {
            if (c is VoucherClause vc)
            {
                if (mergedVoucher != null)
                {
                    if (!mergedVoucherEmitted)
                    {
                        mergedVoucherEmitted = true;
                        list.Add((mergedVoucher, vc, vc.Label));
                    }
                    // else: absorbed into merged filter, skip
                }
                else
                {
                    list.Add((new VoucherFilterDesc(vc), vc, vc.Label));
                }
                continue;
            }

            if (
                legendaryExpansion == LegendaryClauseExpansion.SplitLegendaryEdition
                && TryExpandLegendaryEditionPipeline(c, out List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>? expanded)
            )
            {
                for (int i = 0; i < expanded.Count; i++)
                    list.Add(expanded[i]);
            }
            else if (!IsSpecialtySourceOnly(c))
                list.Add((CreateDesc(c), c, c.Label));
        }
    }

    private static bool IsSpecialtySourceOnly(IJamlClause c)
    {
        JokerSourceConfig? sources = c switch
        {
            JokerClause j => j.Sources,
            CommonJokerClause j => j.Sources,
            UncommonJokerClause j => j.Sources,
            RareJokerClause j => j.Sources,
            MixedJokerClause j => j.Sources,
            _ => null,
        };
        if (sources == null) return false;
        if (sources.ShopItems.Length > 0 || sources.BoosterPacks.Length > 0) return false;
        return sources.Judgement.Length > 0
            || sources.Wraith.Length > 0
            || sources.RiffRaff.Length > 0
            || sources.RareTag.Length > 0
            || sources.UncommonTag.Length > 0
            || sources.CommonShopJokers.Length > 0
            || sources.UncommonShopJokers.Length > 0
            || sources.RareShopJokers.Length > 0
            || sources.AllShopJokers.Length > 0;
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

            MultiVoucherFilterDesc d => new MotelySearchSettings<MultiVoucherFilterDesc.MultiVoucherFilter>(d),

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

            SpaceLevelupFilterDesc d =>

                new MotelySearchSettings<SpaceLevelupFilterDesc.SpaceLevelupFilter>(d),

            BusinessPayoutFilterDesc d =>

                new MotelySearchSettings<BusinessPayoutFilterDesc.BusinessPayoutFilter>(d),

            BloodstoneTriggerFilterDesc d =>

                new MotelySearchSettings<BloodstoneTriggerFilterDesc.BloodstoneTriggerFilter>(d),

            ParkingPayoutFilterDesc d =>

                new MotelySearchSettings<ParkingPayoutFilterDesc.ParkingPayoutFilter>(d),

            GlassDestroyFilterDesc d =>

                new MotelySearchSettings<GlassDestroyFilterDesc.GlassDestroyFilter>(d),

            WheelStaysFlippedFilterDesc d =>

                new MotelySearchSettings<WheelStaysFlippedFilterDesc.WheelStaysFlippedFilter>(d),

            Motely.Filters.Jaml.AndFilterDesc d => new MotelySearchSettings<Motely.Filters.Jaml.AndFilterDesc.AndFilter>(d),

            Motely.Filters.Jaml.OrFilterDesc d => new MotelySearchSettings<Motely.Filters.Jaml.OrFilterDesc.OrFilter>(d),

            NegationFilterDesc d => new MotelySearchSettings<NegationFilterDesc.NegationFilter>(d),

            PassthroughFilterDesc d =>

                new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(d),

            StartingDrawFilterDesc d =>

                new MotelySearchSettings<StartingDrawFilterDesc.StartingDrawFilter>(d),

            NegativeSoulJokerSimdFilterDesc d =>

                new MotelySearchSettings<NegativeSoulJokerSimdFilterDesc.FilterStruct>(d),

            SoulJokerShopSoulFilterDesc d =>

                new MotelySearchSettings<SoulJokerShopSoulFilterDesc.FilterStruct>(d),

            _ => throw new NotSupportedException(

                $"Unknown filter desc type: {desc.GetType().Name}"

            ),

        };

    }



    private static IMotelySeedFilterDesc CreateDesc(IJamlClause clause)

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

            SpaceLevelupClause c => new SpaceLevelupFilterDesc(c),

            BusinessPayoutClause c => new BusinessPayoutFilterDesc(c),

            BloodstoneTriggerClause c => new BloodstoneTriggerFilterDesc(c),

            ParkingPayoutClause c => new ParkingPayoutFilterDesc(c),

            GlassDestroyClause c => new GlassDestroyFilterDesc(c),

            WheelStaysFlippedClause c => new WheelStaysFlippedFilterDesc(c),

            StartingDrawClause c => new StartingDrawFilterDesc(c),

            AndClause c => new Motely.Filters.Jaml.AndFilterDesc(c.Clauses.Select(CreateDesc).ToArray()),

            OrClause c => new Motely.Filters.Jaml.OrFilterDesc(c.Clauses.Select(CreateDesc).ToArray(), c.Min),

            _ => throw new NotSupportedException($"Unknown clause type: {clause.GetType().Name}"),

        };

    }

}

