using System.Collections.Generic;
using Motely.Filters;

namespace Motely.Utils
{
    /// <summary>
    /// Shared utility for filter category mapping and slicing
    /// </summary>
    public static class FilterCategoryMapper
    {
        /// <summary>
        /// Maps item type to optimized filter category
        /// </summary>
        public static FilterCategory GetCategory(MotelyFilterItemType itemType)
        {
            return itemType switch
            {
                MotelyFilterItemType.Voucher => FilterCategory.Voucher,
                MotelyFilterItemType.Joker => FilterCategory.Joker,
                MotelyFilterItemType.SoulJoker => FilterCategory.SoulJoker,
                MotelyFilterItemType.TarotCard => FilterCategory.TarotCard,
                MotelyFilterItemType.PlanetCard => FilterCategory.PlanetCard,
                MotelyFilterItemType.SpectralCard => FilterCategory.SpectralCard,
                MotelyFilterItemType.PlayingCard => FilterCategory.PlayingCard,
                MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => FilterCategory.Tag,
                MotelyFilterItemType.Boss => FilterCategory.Boss,
                _ => throw new Exception($"Unknown item type: {itemType}")
            };
        }

        /// <summary>
        /// PROPER SLICING: Groups clauses by FilterCategory for optimal vectorization
        /// </summary>
        public static Dictionary<FilterCategory, List<MotelyJsonConfig.MotleyJsonFilterClause>> GroupClausesByCategory(
            List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
        {
            var grouped = new Dictionary<FilterCategory, List<MotelyJsonConfig.MotleyJsonFilterClause>>();
            
            foreach (var clause in clauses)
            {
                var category = GetCategory(clause.ItemTypeEnum);
                
                if (!grouped.ContainsKey(category))
                {
                    grouped[category] = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                }
                
                grouped[category].Add(clause);
            }
            
            return grouped;
        }
    }
}