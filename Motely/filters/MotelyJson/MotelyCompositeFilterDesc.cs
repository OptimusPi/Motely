using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Motely.Filters;
using Motely.Utils;

namespace Motely.Filters;

/// <summary>
/// Composite filter that directly calls multiple filters and combines their results
/// BYPASSES the broken batching system entirely!
/// </summary>
public struct MotelyCompositeFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> mustClauses)
    : IMotelySeedFilterDesc<MotelyCompositeFilterDesc.MotelyCompositeFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _mustClauses = mustClauses;

    public MotelyCompositeFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(_mustClauses);

        // Create individual filters for each category
        var individualFilters = new List<IMotelySeedFilter>();

        foreach (var kvp in clausesByCategory)
        {
            var category = kvp.Key;
            var clauses = kvp.Value;
            IMotelySeedFilter filter = category switch
            {
                FilterCategory.Joker => new MotelyJsonJokerFilterDesc(
                    MotelyJsonJokerFilterClause.CreateCriteria(MotelyJsonJokerFilterClause.ConvertClauses(clauses))).CreateFilter(ref ctx),
                FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(
                    MotelyJsonSpectralFilterClause.CreateCriteria(MotelyJsonSpectralFilterClause.ConvertClauses(clauses))).CreateFilter(ref ctx),
                FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(
                    MotelyJsonSoulJokerFilterClause.CreateCriteria(MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses))).CreateFilter(ref ctx),
                FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(
                    MotelyJsonTarotFilterClause.CreateCriteria(MotelyJsonTarotFilterClause.ConvertClauses(clauses))).CreateFilter(ref ctx),
                FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(
                    MotelyJsonPlanetFilterClause.CreateCriteria(MotelyJsonPlanetFilterClause.ConvertClauses(clauses))).CreateFilter(ref ctx),
                FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreatePlayingCardCriteria(clauses)).CreateFilter(ref ctx),
                FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(
                    MotelyJsonVoucherFilterClause.CreateCriteria(MotelyJsonVoucherFilterClause.ConvertClauses(clauses))).CreateFilter(ref ctx),
                FilterCategory.Boss => new MotelyJsonBossFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateBossCriteria(clauses)).CreateFilter(ref ctx),
                FilterCategory.Tag => new MotelyJsonTagFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateTagCriteria(clauses)).CreateFilter(ref ctx),
                FilterCategory.And => CreateAndFilter(clauses, ref ctx),
                FilterCategory.Or => CreateOrFilter(clauses, ref ctx),
                _ => throw new ArgumentException($"Unsupported filter category: {category}")
            };
            individualFilters.Add(filter);
        }

        return new MotelyCompositeFilter(individualFilters);
    }

    private static IMotelySeedFilter CreateAndFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> andClauses, ref MotelyFilterCreationContext ctx)
    {
        // AND filter: ALL nested clauses must pass
        var nestedFilters = new List<IMotelySeedFilter>();

        foreach (var andClause in andClauses)
        {
            if (andClause.Clauses == null || andClause.Clauses.Count == 0)
                continue; // Skip empty And clause

            // Recursively create a composite filter for the nested clauses
            var nestedComposite = new MotelyCompositeFilterDesc(andClause.Clauses);
            nestedFilters.Add(nestedComposite.CreateFilter(ref ctx));
        }

        return new AndFilter(nestedFilters);
    }

    private static IMotelySeedFilter CreateOrFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> orClauses, ref MotelyFilterCreationContext ctx)
    {
        // OR filter: at least ONE nested clause must pass
        var nestedFilters = new List<IMotelySeedFilter>();

        foreach (var orClause in orClauses)
        {
            if (orClause.Clauses == null || orClause.Clauses.Count == 0)
                continue; // Skip empty Or clause

            // Recursively create a composite filter for the nested clauses
            var nestedComposite = new MotelyCompositeFilterDesc(orClause.Clauses);
            nestedFilters.Add(nestedComposite.CreateFilter(ref ctx));
        }

        return new OrFilter(nestedFilters);
    }

    public struct MotelyCompositeFilter : IMotelySeedFilter
    {
        private readonly List<IMotelySeedFilter> _filters;

        public MotelyCompositeFilter(List<IMotelySeedFilter> filters)
        {
            _filters = filters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            // Start with all bits set
            VectorMask result = VectorMask.AllBitsSet;

            // Call each filter directly and AND the results (Must logic)
            foreach (var filter in _filters)
            {
                var filterMask = filter.Filter(ref ctx);
                result &= filterMask;

                // Early exit if no seeds pass
                if (result.IsAllFalse())
                    return VectorMask.NoBitsSet;
            }

            return result;
        }
    }

    /// <summary>
    /// AND Filter - ALL nested filters must pass
    /// </summary>
    public struct AndFilter : IMotelySeedFilter
    {
        private readonly List<IMotelySeedFilter> _nestedFilters;

        public AndFilter(List<IMotelySeedFilter> nestedFilters)
        {
            _nestedFilters = nestedFilters;
        }

        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_nestedFilters == null || _nestedFilters.Count == 0)
                return VectorMask.NoBitsSet; // Empty AND fails all

            // Start with all bits set, AND together all nested results
            VectorMask result = VectorMask.AllBitsSet;

            foreach (var filter in _nestedFilters)
            {
                var nested = filter.Filter(ref ctx);
                result &= nested; // Bitwise AND

                if (result.IsAllFalse())
                    return VectorMask.NoBitsSet; // Early exit
            }

            return result;
        }
    }

    /// <summary>
    /// OR Filter - at least ONE nested filter must pass
    /// </summary>
    public struct OrFilter : IMotelySeedFilter
    {
        private readonly List<IMotelySeedFilter> _nestedFilters;

        public OrFilter(List<IMotelySeedFilter> nestedFilters)
        {
            _nestedFilters = nestedFilters;
        }

        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_nestedFilters == null || _nestedFilters.Count == 0)
                return VectorMask.NoBitsSet; // Empty OR fails all

            // Start with no bits set, OR together all nested results
            VectorMask result = VectorMask.NoBitsSet;

            foreach (var filter in _nestedFilters)
            {
                var nested = filter.Filter(ref ctx);
                result |= nested; // Bitwise OR
            }

            return result;
        }
    }
}