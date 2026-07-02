using System.Collections.Concurrent;
using System.Reflection;

namespace Motely.Filters.Jaml;

/// <summary>
/// Resolves a clause's estimated crunch cost — one crunch ≈ one vectorized PRNG pull (2 vector
/// divisions + a full LuaRandom reseed covering 8 seeds). Flat clauses read their
/// <see cref="JamlClauseAttribute.Cost"/> (the co-located source of truth); shop/pack clauses
/// override <see cref="IJamlClause.EstimateCost"/> with source-aware formulas built from the
/// helpers here. JamlSearchBuilder orders the must/mustNot chains by this so trivial SIMD
/// clauses kill lanes before expensive scalar ones run. Estimates never change results.
/// </summary>
public static class JamlCostModel
{
    /// <summary>Fallback for a clause type with no attribute cost: mid-tier (shop/pack family).</summary>
    public const int DefaultCost = 100;

    private static readonly ConcurrentDictionary<Type, (int Cost, bool PerAnte)> Cache = new();

    public static int Estimate(IJamlClause clause)
    {
        var (cost, perAnte) = Cache.GetOrAdd(
            clause.GetType(),
            static type =>
            {
                var attr = type.GetCustomAttribute<JamlClauseAttribute>();
                return attr is { Cost: > 0 } ? (attr.Cost, attr.CostPerAnte) : (DefaultCost, true);
            }
        );

        if (perAnte && clause is IAnteScopedClause anteScoped)
            return cost * AnteCount(anteScoped);

        return cost;
    }

    /// <summary>
    /// Antes a clause will actually walk: an empty list means "anywhere", which
    /// JamlSearchBuilder normalizes to all 8 antes — cost estimates must agree.
    /// </summary>
    public static int AnteCount(IAnteScopedClause clause) =>
        clause.Antes.Length > 0 ? clause.Antes.Length : 8;

    /// <summary>
    /// Slots a sequential PRNG stream must walk to satisfy the referenced indices: streams only
    /// advance forward, so <c>[7]</c> costs 8 walks and <c>[0]</c> costs 1. Empty = 0.
    /// </summary>
    public static int SlotWalk(int[] slots)
    {
        int max = -1;
        foreach (var slot in slots)
            if (slot > max)
                max = slot;
        return max + 1;
    }

    /// <summary>
    /// Per-ante crunches for a shop+pack source layout: ~8 pulls per assembled shop slot
    /// (<c>GetNextShopItem</c> polls joker/tarot/planet/spectral unconditionally), ~7 per
    /// booster slot (pack poll + contents), ~2 per specialty-stream roll (fixed-rarity tag
    /// jokers, emperor, seals, …). Floor of 2 covers stream setup on empty layouts.
    /// </summary>
    public static int ShopPackAnteCost(
        int[] shopItems,
        int[] boosterPacks,
        params int[][] specialtyRolls
    )
    {
        int cost = SlotWalk(shopItems) * 8 + SlotWalk(boosterPacks) * 7;
        foreach (var rolls in specialtyRolls)
            cost += SlotWalk(rolls) * 2;
        return Math.Max(2, cost);
    }
}
