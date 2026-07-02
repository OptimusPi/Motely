using Motely.Filters;

namespace Motely.Filters.Jaml;

/// <summary>
/// Projects <see cref="JamlCostModel"/>'s raw crunch estimates onto the SIMD execution model:
/// one crunch retires <see cref="MotelyGlobals.MaxVectorWidth"/> seeds at once, so a config's
/// wall-clock speed is the crunch rate spread across the lanes. Everything here is worst-case
/// (every filter runs on every batch, no lane short-circuit credit) — a floor on speed, never
/// a promise. Estimates never change results.
///
/// Deliberately extension methods over existing types — no new instance member and no new type
/// crosses the Bootsharp / NativeAOT-LLVM boundary. The declaration scanner only sees static
/// call sites, so the marshaled interop surface is byte-identical with or without this file.
/// </summary>
public static class JamlCostModelSimdExtensions
{
    /// <summary>
    /// Raw crunches for one clause, recursing through and/or groups: a logic clause costs the
    /// sum of its children (worst case: nothing short-circuits), never less than one crunch.
    /// Flat clauses defer to <see cref="JamlCostModel.Estimate"/>.
    /// </summary>
    public static int EstimateCrunches(this IJamlClause clause)
    {
        if (clause is not LogicClause logic)
            return JamlCostModel.Estimate(clause);

        int total = 0;
        foreach (var child in logic.Clauses)
            total += child.EstimateCrunches();
        return Math.Max(1, total);
    }

    /// <summary>
    /// Crunches one 8-seed batch spends in the SIMD filter pass: every must and mustNot clause,
    /// worst case. This is the cost the searcher pays per batch before scoring ever runs.
    /// </summary>
    public static int EstimateFilterCrunches(this JamlConfig config)
    {
        int total = 0;
        foreach (var clause in config.Must)
            total += clause.EstimateCrunches();
        foreach (var clause in config.MustNot)
            total += clause.EstimateCrunches();
        return Math.Max(1, total);
    }

    /// <summary>
    /// Effective crunches per *seed*: the batch cost amortized across the lanes that retire
    /// together. This is the number to compare between two configs — "config A costs 3.5
    /// crunches a seed, config B costs 41" — independent of any machine's crunch rate.
    /// </summary>
    public static double SimdCostPerSeed(this JamlConfig config) =>
        (double)config.EstimateFilterCrunches() / MotelyGlobals.MaxVectorWidth;

    /// <summary>
    /// Projected search speed in seeds/second given a measured crunch rate (crunches/second —
    /// calibrate once per machine by timing any known filter). Worst-case floor: real searches
    /// run faster as early clauses kill whole batches and skip the rest of the chain.
    /// </summary>
    public static double ProjectedSeedsPerSecond(this JamlConfig config, double crunchesPerSecond) =>
        crunchesPerSecond / config.EstimateFilterCrunches() * MotelyGlobals.MaxVectorWidth;

    /// <summary>Estimate a clause and amortize it across the SIMD lanes in one call.</summary>
    public static double SimdProjectedCost(this IJamlClause clause) =>
        (double)clause.EstimateCrunches() / MotelyGlobals.MaxVectorWidth;

    /// <summary>
    /// The must/mustNot chains reordered cheapest-first, without mutating the config: the order
    /// <see cref="JamlSearchBuilder"/> should feed <c>WithAdditionalFilter</c> so trivial SIMD
    /// clauses kill lanes before expensive scalar ones run — the ordering JamlCostModel's
    /// contract describes. Stable sort: equal-cost clauses keep their authored order.
    /// </summary>
    public static IEnumerable<IJamlClause> CheapestFirst(this IReadOnlyList<IJamlClause> clauses) =>
        clauses.OrderBy(EstimateCrunches);
}
