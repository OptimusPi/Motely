using System;

using Motely;
using Motely.Filters.Native;

using System.Collections.Generic;

using System.Diagnostics;

using System.Diagnostics.CodeAnalysis;

using System.Linq;

using System.Text;

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

    public static string ExplainPlan(JamlConfig config)

    {

        if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)

            throw new InvalidOperationException("JamlConfig has no clauses.");

        ValidateLegendaryJokerClausesForMustAndShould(config.Must);
        ValidateLegendaryJokerClausesForMustAndShould(config.Should);

        var sb = new StringBuilder();
        sb.AppendLine("# JAML filter eval plan");
        sb.AppendLine();
        sb.AppendLine("Contract: must clauses evaluate top-to-bottom and short-circuit on first fail. mustNot clauses reject on match. should clauses contribute score but the current scorer evaluates all should clauses.");

        AppendClauseSection(sb, "must", config.Must.OrderedClauses);
        AppendClauseSection(sb, "mustNot", config.MustNot.OrderedClauses);
        AppendClauseSection(sb, "should", config.Should.OrderedClauses);

        return sb.ToString().TrimEnd();
    }

    public static IMotelySearchSettings CreateSettings(JamlConfig config) =>

        CreatePlan(config, 0).Settings;

    public static JamlSearchPlan CreatePlan(JamlConfig config, int shouldScoreMinimumTotal = 0)

    {

        if (!config.Must.HasAnyClauses && !config.Should.HasAnyClauses && !config.MustNot.HasAnyClauses)

            throw new InvalidOperationException("JamlConfig has no clauses.");

        ValidateLegendaryJokerClausesForMustAndShould(config.Must);
        ValidateLegendaryJokerClausesForMustAndShould(config.Should);

        var orderedMustClauses = OrderClausesByEstimatedCost(config.Must.OrderedClauses);
        var orderedShouldClauses = config.Should.OrderedClauses;
        var orderedMustNotClauses = OrderClausesByEstimatedCost(config.MustNot.OrderedClauses);

        var allMustDescs = new List<IMotelySeedFilterDesc>();

        // ── MUST: required filters (AND logic) ──

        var mustDescs = new List<IMotelySeedFilterDesc>();

        AddDescsFromSet(mustDescs, orderedMustClauses, LegendaryClauseExpansion.SplitLegendaryEdition);

        allMustDescs.AddRange(mustDescs);

        var mustNotDescs = new List<IMotelySeedFilterDesc>();

        AddDescsFromSet(mustNotDescs, orderedMustNotClauses, LegendaryClauseExpansion.None);

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
        AddShouldScoringEntriesFromSet(shouldClauses, orderedMustClauses);
        int mustClauseCount = shouldClauses.Count;
        AddShouldScoringEntriesFromSet(shouldClauses, orderedShouldClauses);
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
    /// (<see cref="JamlLegendaryJokerStructuralValidation"/>). Skips <c>mustNot</c>: negated dead clauses are vacuously true.
    /// </summary>
    private static void ValidateLegendaryJokerClausesForMustAndShould(JamlClauseSet set)
    {
        foreach (IJamlClause c in set.OrderedClauses)
            ValidateClauseTreeForLegendaryJoker(c);
    }

    private static void ValidateClauseTreeForLegendaryJoker(IJamlClause c)
    {
        switch (c)
        {
            case LegendaryJokerClause lj:
                JamlLegendaryJokerStructuralValidation.ValidateLegendaryJokerClauseOrThrow(lj);
                return;
            case AndClause and:
                for (int i = 0; i < and.Clauses.Length; i++)
                    ValidateClauseTreeForLegendaryJoker(and.Clauses[i]);
                return;
            case OrClause or:
                for (int i = 0; i < or.Clauses.Length; i++)
                    ValidateClauseTreeForLegendaryJoker(or.Clauses[i]);
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

    private static void AppendClauseSection(
        StringBuilder sb,
        string sectionName,
        IReadOnlyList<IJamlClause> originalClauses
    )
    {
        sb.AppendLine();
        sb.Append("## ");
        sb.Append(sectionName);
        sb.Append(" (");
        sb.Append(originalClauses.Count);
        sb.AppendLine(originalClauses.Count == 1 ? " clause)" : " clauses)");

        if (originalClauses.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No clauses.");
            return;
        }

        sb.AppendLine();
        sb.AppendLine("Written order:");
        for (int i = 0; i < originalClauses.Count; i++)
        {
            sb.Append(i + 1);
            sb.Append(". ");
            sb.AppendLine(DescribeClausePlanEntry(originalClauses[i]));
        }

        var orderedClauses = OrderClausesByEstimatedCost(originalClauses);
        bool changed = !originalClauses.SequenceEqual(orderedClauses);

        sb.AppendLine();
        sb.AppendLine(changed ? "Runtime order (estimated cheapest-first):" : "Already in runtime order:");
        for (int i = 0; i < orderedClauses.Count; i++)
        {
            sb.Append(i + 1);
            sb.Append(". ");
            sb.AppendLine(DescribeClausePlanEntry(orderedClauses[i]));
        }
    }

    private static string DescribeClausePlanEntry(IJamlClause clause)
    {
        var label = string.IsNullOrWhiteSpace(clause.Label) ? string.Empty : $" \u2014 label: {clause.Label}";
        return $"[cost {EstimateClauseCost(clause)}] {DescribeClause(clause)}{label}";
    }

    private static string DescribeClause(IJamlClause clause)
    {
        return clause switch
        {
            JokerClause c => $"joker {DescribeJokerNames(c.IsWildcard, c.Jokers.Select(static j => j.ToString()))}",
            CommonJokerClause c => $"commonJoker {DescribeJokerNames(c.IsWildcard, c.Jokers.Select(static j => j.ToString()))}",
            UncommonJokerClause c => $"uncommonJoker {DescribeJokerNames(c.IsWildcard, c.Jokers.Select(static j => j.ToString()))}",
            RareJokerClause c => $"rareJoker {DescribeJokerNames(c.IsWildcard, c.Jokers.Select(static j => j.ToString()))}",
            MixedJokerClause c => $"jokers {DescribeJokerNames(c.IsWildcard, c.Jokers.Select(static j => j.ToString()))}",
            LegendaryJokerClause c => $"legendaryJoker {DescribeJokerNames(c.IsWildcard, c.Jokers.Select(static j => j.ToString()))}",
            VoucherClause c => $"voucher {string.Join(", ", c.Vouchers.Select(static v => v.ToString()))}",
            TarotCardClause c => $"tarotCard {string.Join(", ", c.Tarots.Select(static t => t.ToString()))}",
            SpectralCardClause c => $"spectralCard {string.Join(", ", c.Spectrals.Select(static s => s.ToString()))}",
            PlanetCardClause c => $"planetCard {string.Join(", ", c.Planets.Select(static p => p.ToString()))}",
            BossClause c => $"boss {string.Join(", ", c.Bosses.Select(static b => b.ToString()))}",
            TagClause c => $"tag {string.Join(", ", c.Tags.Select(static t => t.ToString()))}",
            StandardCardClause c => $"standardCard {DescribeStandardCard(c)}",
            ErraticRankClause c => $"erraticRank {c.Rank}",
            ErraticSuitClause c => $"erraticSuit {c.Suit}",
            ErraticCardClause c => $"erraticCard {DescribeErraticCard(c)}",
            StartingDrawClause c => $"startingDraw {DescribeStartingDraw(c)}",
            LuckyMoneyClause => "event LuckyMoney",
            LuckyMultClause => "event LuckyMult",
            MisprintMultClause => "event MisprintMult",
            WheelOfFortuneClause => "event WheelOfFortune",
            CavendishExtinctClause => "event CavendishExtinct",
            GrosMichelExtinctClause => "event GrosMichelExtinct",
            SpaceLevelupClause => "event SpaceLevelup",
            BusinessPayoutClause => "event BusinessPayout",
            BloodstoneTriggerClause => "event BloodstoneTrigger",
            ParkingPayoutClause => "event ParkingPayout",
            GlassDestroyClause => "event GlassDestroy",
            WheelStaysFlippedClause => "event WheelStaysFlipped",
            AndClause c => $"and({c.Clauses.Length})",
            OrClause c => $"or({c.Clauses.Length})",
            _ => clause.GetType().Name,
        };
    }

    private static string DescribeJokerNames(bool isWildcard, IEnumerable<string> names) =>
        isWildcard ? "Any" : string.Join(", ", names);

    private static string DescribeStandardCard(StandardCardClause clause)
    {
        if (clause.Cards.Length == 0)
            return "Any";

        return string.Join(", ", clause.Cards.Select(card =>
        {
            var parts = new List<string>();
            if (card.Rank.HasValue) parts.Add(card.Rank.Value.ToString());
            if (card.Suit.HasValue) parts.Add(card.Suit.Value.ToString());
            return parts.Count == 0 ? "Any" : string.Join(" ", parts);
        }));
    }

    private static string DescribeErraticCard(ErraticCardClause clause)
    {
        var parts = new List<string>();
        if (clause.Rank.HasValue) parts.Add(clause.Rank.Value.ToString());
        if (clause.Suit.HasValue) parts.Add(clause.Suit.Value.ToString());
        return parts.Count == 0 ? "Any" : string.Join(" ", parts);
    }

    private static string DescribeStartingDraw(StartingDrawClause clause)
    {
        var parts = new List<string>();
        if (clause.Rank.HasValue) parts.Add(clause.Rank.Value.ToString());
        if (clause.Suit.HasValue) parts.Add(clause.Suit.Value.ToString());
        return parts.Count == 0 ? "Any" : string.Join(" ", parts);
    }




    private static void AddDescsFromSet(
        List<IMotelySeedFilterDesc> list,
        IReadOnlyList<IJamlClause> clauses,
        LegendaryClauseExpansion legendaryExpansion
    )
    {
        var typed = new List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)>();
        AddDescsFromSet(typed, clauses, legendaryExpansion);

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
    private static void AddShouldScoringEntriesFromSet(
        List<IJamlClause> clauses,
        IReadOnlyList<IJamlClause> orderedClauses
    )
    {
        for (int i = 0; i < orderedClauses.Count; i++)
        {
            _ = CreateDesc(orderedClauses[i]);
            clauses.Add(orderedClauses[i]);
        }
    }

    private static void AddDescsFromSet(
        List<(IMotelySeedFilterDesc desc, IJamlClause clause, string label)> list,
        IReadOnlyList<IJamlClause> clauses,
        LegendaryClauseExpansion legendaryExpansion
    )
    {
        // Merge consecutive voucher clauses into a single MultiVoucherFilterDesc so the
        // voucher PRNG state is built only once instead of once per clause.
        var voucherClauses = clauses.OfType<VoucherClause>().ToArray();
        IMotelySeedFilterDesc? mergedVoucher = voucherClauses.Length > 1
            ? new MultiVoucherFilterDesc(voucherClauses)
            : null;
        bool mergedVoucherEmitted = false;

        for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
        {
            var c = clauses[clauseIndex];
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

    private static IReadOnlyList<IJamlClause> OrderClausesByEstimatedCost(
        IReadOnlyList<IJamlClause> clauses
    )
    {
        if (clauses.Count <= 1)
            return clauses;

        return clauses
            .Select((clause, index) => (clause, cost: EstimateClauseCost(clause), index))
            .OrderBy(item => item.cost)
            .ThenBy(item => item.index)
            .Select(item => item.clause)
            .ToArray();
    }

    private static int EstimateClauseCost(IJamlClause clause)
    {
        int baseCost = clause switch
        {
            BossClause => 2,
            LuckyMoneyClause => 3,
            LuckyMultClause => 3,
            MisprintMultClause => 3,
            WheelOfFortuneClause => 3,
            CavendishExtinctClause => 3,
            GrosMichelExtinctClause => 3,
            SpaceLevelupClause => 3,
            BusinessPayoutClause => 3,
            BloodstoneTriggerClause => 3,
            ParkingPayoutClause => 3,
            GlassDestroyClause => 3,
            WheelStaysFlippedClause => 3,
            TagClause => 3,
            VoucherClause => 4,
            ErraticRankClause => 4,
            ErraticSuitClause => 4,
            LegendaryJokerClause => 5,
            RareJokerClause => 5,
            ErraticCardClause => 5,
            JokerClause => 6,
            CommonJokerClause => 6,
            UncommonJokerClause => 6,
            MixedJokerClause => 6,
            TarotCardClause => 7,
            SpectralCardClause => 7,
            PlanetCardClause => 7,
            StartingDrawClause => 7,
            StandardCardClause => 8,
            AndClause c => 1 + SumNestedClauseCosts(c.Clauses),
            OrClause c => 1 + SumNestedClauseCosts(c.Clauses),
            _ => 10,
        };

        return baseCost + GetMaxAnte(clause);
    }

    private static int SumNestedClauseCosts(IJamlClause[] clauses)
    {
        int total = 0;
        for (int i = 0; i < clauses.Length; i++)
            total += EstimateClauseCost(clauses[i]);
        return total;
    }

    private static int GetMaxAnte(IJamlClause clause)
    {
        return clause switch
        {
            JokerClause c => ArrayMax(c.Antes),
            CommonJokerClause c => ArrayMax(c.Antes),
            UncommonJokerClause c => ArrayMax(c.Antes),
            RareJokerClause c => ArrayMax(c.Antes),
            MixedJokerClause c => ArrayMax(c.Antes),
            LegendaryJokerClause c => ArrayMax(c.Antes),
            VoucherClause c => ArrayMax(c.Antes),
            TarotCardClause c => ArrayMax(c.Antes),
            SpectralCardClause c => ArrayMax(c.Antes),
            PlanetCardClause c => ArrayMax(c.Antes),
            BossClause c => ArrayMax(c.Antes),
            TagClause c => ArrayMax(c.Antes),
            StandardCardClause c => ArrayMax(c.Antes),
            ErraticRankClause c => ArrayMax(c.Antes),
            ErraticSuitClause c => ArrayMax(c.Antes),
            ErraticCardClause c => ArrayMax(c.Antes),
            StartingDrawClause c => ArrayMax(c.Antes),
            IRollClause c => ArrayMax(c.Rolls),
            AndClause c => MaxNestedAnte(c.Clauses),
            OrClause c => MaxNestedAnte(c.Clauses),
            _ => 0,
        };
    }

    private static int MaxNestedAnte(IJamlClause[] clauses)
    {
        int max = 0;
        for (int i = 0; i < clauses.Length; i++)
        {
            int nestedMax = GetMaxAnte(clauses[i]);
            if (nestedMax > max)
                max = nestedMax;
        }

        return max;
    }

    private static int ArrayMax(int[] array)
    {
        if (array.Length == 0)
            return 0;

        int max = array[0];
        for (int i = 1; i < array.Length; i++)
        {
            if (array[i] > max)
                max = array[i];
        }

        return max;
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

            NegativeLegendaryJokerSimdFilterDesc d =>

                new MotelySearchSettings<NegativeLegendaryJokerSimdFilterDesc.FilterStruct>(d),

            LegendaryJokerShopSoulFilterDesc d =>

                new MotelySearchSettings<LegendaryJokerShopSoulFilterDesc.FilterStruct>(d),

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

