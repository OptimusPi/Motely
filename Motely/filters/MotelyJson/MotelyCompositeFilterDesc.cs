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
                FilterCategory.Joker => new MotelyJsonJokerFilterDesc(MotelyJsonJokerFilterClause.ConvertClauses(clauses)).CreateFilter(ref ctx),
                FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(MotelyJsonSpectralFilterClause.ConvertClauses(clauses)).CreateFilter(ref ctx),
                FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)).CreateFilter(ref ctx),
                FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(MotelyJsonTarotFilterClause.ConvertClauses(clauses)).CreateFilter(ref ctx),
                FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(MotelyJsonPlanetFilterClause.ConvertClauses(clauses)).CreateFilter(ref ctx),
                FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(clauses).CreateFilter(ref ctx),
                FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(MotelyJsonVoucherFilterClause.ConvertClauses(clauses)).CreateFilter(ref ctx),
                FilterCategory.Boss => new MotelyJsonBossFilterDesc(clauses).CreateFilter(ref ctx),
                FilterCategory.Tag => new MotelyJsonTagFilterDesc(clauses).CreateFilter(ref ctx),
                _ => throw new ArgumentException($"Unsupported filter category: {category}")
            };
            individualFilters.Add(filter);
        }
        
        return new MotelyCompositeFilter(individualFilters);
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
}