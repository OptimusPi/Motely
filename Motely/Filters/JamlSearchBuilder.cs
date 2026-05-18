using System;

using Motely;
using Motely.Filters.Native;

using System.Collections.Generic;

using System.Diagnostics;

using System.Diagnostics.CodeAnalysis;

using System.Linq;

using System.Text;

namespace Motely.Filters;

/// <summary>Compiled JAML: runnable settings plus tally width for sinks (matches scoring clause count).</summary>
public sealed record JamlSearchPlan(
    int ScoreTallyColumnCount,
    /// <summary>RFC-4180 style header line: quoted fields, comma-separated. Empty when <see cref="ScoreTallyColumnCount"/> is 0.</summary>
    string ScoredCsvHeaderQuoted,
    /// <summary>Authoritative tally column labels in evaluation order (must clauses first, then should).</summary>
    string[] TallyLabels
)
{
    internal IMotelySearchSettings Settings { get; init; } = null!;

    /// <summary>
    /// Non-null when the plan could not be built (invalid JAML or builder validation failure).
    /// Populated only by the Motely.Wasm export wrappers — direct callers of
    /// <see cref="JamlSearchBuilder.CreatePlan(JamlConfig, int)"/> get the exception path.
    /// Why: under NativeAOT-LLVM trim mode, exceptions crossing the JSExport boundary lose their
    /// .Message and surface as the opaque "C# exception from NativeAOT" husk. Carrying the error
    /// as a field is the only path that preserves the diagnostic to JS callers.
    /// </summary>
    public string? Error { get; init; }
}

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
        CreateSettings(config, 0);
    public static IMotelySearchSettings CreateSettings(
        JamlConfig config,
        int shouldScoreMinimumTotal
    ) => CreatePlan(config, shouldScoreMinimumTotal).Settings;
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
            ? shouldOnlyClauses.Select(static c => c.Label).ToArray()
            : [];
        return new JamlSearchPlan(shouldOnlyCount, headerQuoted, tallyLabels) { Settings = settings };
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
    /// Fails fast on soul joker clauses whose booster sources can never hit arcana/Spectral at slot ≥1
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

    /// <summary>Quoted CSV header for stdout sinks: <c>seed</c>, <c>score</c>, then each should-scoring clause label. Built once per plan.</summary>
    public static string BuildScoredCsvHeaderQuoted(IReadOnlyList<IJamlClause> shouldClauses)
    {
        int n = shouldClauses.Count;
        var parts = new string[2 + n];
        parts[0] = CsvQuoteField("seed");
        parts[1] = CsvQuoteField("score");
        for (int i = 0; i < n; i++)
        {
            parts[2 + i] = CsvQuoteField(shouldClauses[i].Label);
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
        return $"[cost {clause.EstimatedCost}] {clause.Describe()}{label}";
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

    /// <summary>Collects clauses for <see cref="JamlShouldScoreDesc"/> (validates each by constructing its desc).</summary>
    private static void AddShouldScoringEntriesFromSet(
        List<IJamlClause> clauses,
        IReadOnlyList<IJamlClause> orderedClauses
    )
    {
        for (int i = 0; i < orderedClauses.Count; i++)
        {
            _ = orderedClauses[i].CreateDesc();
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
                list.Add((c.CreateDesc(), c, c.Label));
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

        int n = clauses.Count;
        var pairs = new (int cost, int index, IJamlClause clause)[n];
        for (int i = 0; i < n; i++)
            pairs[i] = (clauses[i].EstimatedCost, i, clauses[i]);

        Array.Sort(pairs, static (a, b) =>
        {
            int c = a.cost.CompareTo(b.cost);
            return c != 0 ? c : a.index.CompareTo(b.index);
        });

        var ordered = new IJamlClause[n];
        for (int i = 0; i < n; i++)
            ordered[i] = pairs[i].clause;
        return ordered;
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
            AndFilterDesc d => new MotelySearchSettings<AndFilterDesc.AndFilter>(d),
            OrFilterDesc d => new MotelySearchSettings<OrFilterDesc.OrFilter>(d),
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

}

