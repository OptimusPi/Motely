using System;
using System.Collections.Generic;
using System.Linq;
using Motely.Filters;

namespace Motely.Utils;

/// <summary>
/// Factory for creating specialized vectorized filters from JSON configurations
/// </summary>
public static class SpecializedFilterFactory
{
    /// <summary>
    /// Creates appropriate specialized filter based on category and clauses
    /// </summary>
    public static IMotelySeedFilterDesc CreateSpecializedFilter(FilterCategory category, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
    {
        if (clauses == null || clauses.Count == 0)
            throw new ArgumentException("Clauses cannot be null or empty");
            
        return category switch
        {
            FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)),
            FilterCategory.Joker => new MotelyJsonJokerFilterDesc(MotelyJsonJokerFilterClause.ConvertClauses(clauses)),
            FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(MotelyJsonVoucherFilterClause.ConvertClauses(clauses)),
            FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(MotelyJsonPlanetFilterClause.ConvertClauses(clauses)),
            FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(MotelyJsonTarotFilterClause.ConvertClauses(clauses)),
            FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(MotelyJsonSpectralFilterClause.ConvertClauses(clauses)),
            FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(clauses),
            FilterCategory.Boss => new MotelyJsonBossFilterDesc(clauses), // RE-ENABLED with proper structure
            FilterCategory.Tag => new MotelyJsonTagFilterDesc(clauses),
            _ => throw new ArgumentException($"Specialized filter not implemented for: {category}")
        };
    }
    
    /// <summary>
    /// Creates appropriate search settings for specialized filter
    /// </summary>
    public static dynamic CreateSearchSettings(IMotelySeedFilterDesc filterDesc)
    {
        return filterDesc switch
        {
            MotelyJsonSoulJokerFilterDesc soulJokerDesc => new MotelySearchSettings<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>(soulJokerDesc),
            MotelyJsonJokerFilterDesc jokerDesc => new MotelySearchSettings<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>(jokerDesc),
            MotelyJsonVoucherFilterDesc voucherDesc => new MotelySearchSettings<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>(voucherDesc),
            MotelyJsonPlanetFilterDesc planetDesc => new MotelySearchSettings<MotelyJsonPlanetFilterDesc.MotelyJsonPlanetFilter>(planetDesc),
            MotelyJsonTarotCardFilterDesc tarotDesc => new MotelySearchSettings<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>(tarotDesc),
            MotelyJsonSpectralCardFilterDesc spectralDesc => new MotelySearchSettings<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>(spectralDesc),
            MotelyJsonPlayingCardFilterDesc playingCardDesc => new MotelySearchSettings<MotelyJsonPlayingCardFilterDesc.MotelyJsonPlayingCardFilter>(playingCardDesc),
            MotelyJsonBossFilterDesc bossDesc => new MotelySearchSettings<MotelyJsonBossFilterDesc.MotelyJsonBossFilter>(bossDesc),
            MotelyJsonTagFilterDesc tagDesc => new MotelySearchSettings<MotelyJsonTagFilterDesc.MotelyJsonTagFilter>(tagDesc),
            _ => throw new ArgumentException($"Search settings not implemented for: {filterDesc.GetType()}")
        };
    }
    
    /// <summary>
    /// Complete JSON filter pipeline: group by category, create base + chained filters
    /// </summary>
    public static dynamic CreateJsonFilterPipeline(List<MotelyJsonConfig.MotleyJsonFilterClause> mustClauses, int threads, int batchSize)
    {
        var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(mustClauses);
        
        if (clausesByCategory.Count == 0)
            throw new Exception("No valid MUST clauses found for filtering");
        
        // Create base filter with first category
        var categories = clausesByCategory.Keys.ToList();
        var primaryCategory = categories[0];
        var primaryClauses = clausesByCategory[primaryCategory];
        
        var baseFilter = CreateSpecializedFilter(primaryCategory, primaryClauses);
        var searchSettings = CreateSearchSettings(baseFilter);
        
        // Chain additional filters
        for (int i = 1; i < categories.Count; i++)
        {
            var category = categories[i];
            var clauses = clausesByCategory[category];
            var additionalFilter = CreateSpecializedFilter(category, clauses);
            searchSettings = searchSettings.WithAdditionalFilter(additionalFilter);
            
        }
        
        return searchSettings;
    }
}