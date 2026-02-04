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
                MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag =>
                    FilterCategory.Tag,
                MotelyFilterItemType.Boss => FilterCategory.Boss,
                MotelyFilterItemType.Event => FilterCategory.Event,
                MotelyFilterItemType.ErraticRank => FilterCategory.ErraticRank,
                MotelyFilterItemType.ErraticSuit => FilterCategory.ErraticSuit,
                MotelyFilterItemType.And => FilterCategory.And,
                MotelyFilterItemType.Or => FilterCategory.Or,
                _ => throw new Exception($"Unknown item type: {itemType}"),
            };
        }

        /// <summary>
        /// PROPER SLICING: Groups clauses by FilterCategory for optimal vectorization
        /// </summary>
        public static Dictionary<
            FilterCategory,
            List<MotelyJsonConfig.MotelyJsonFilterClause>
        > GroupClausesByCategory(List<MotelyJsonConfig.MotelyJsonFilterClause> clauses)
        {
            var grouped =
                new Dictionary<FilterCategory, List<MotelyJsonConfig.MotelyJsonFilterClause>>();

            foreach (var clause in clauses)
            {
                var category = GetCategory(clause.ItemTypeEnum);

                // CRITICAL OPTIMIZATION: Split SoulJoker into edition-only vs type-specific
                // Edition-only clauses (no specific joker type + edition specified) create separate filter for instant early-exit!
                if (category == FilterCategory.SoulJoker)
                {
                    // Use pre-parsed enums - no string comparisons!
                    bool hasSpecificJokerType =
                        clause.JokerEnum.HasValue
                        || (clause.JokerEnums != null && clause.JokerEnums.Count > 0);
                    bool hasEditionRequirement =
                        clause.EditionEnum.HasValue
                        && clause.EditionEnum.Value != MotelyItemEdition.None;

                    if (hasEditionRequirement)
                    {
                        // PERFORMANCE FIX: Always add EditionOnly as fast pre-filter when edition is specified
                        // This rejects 99.7% of seeds instantly (no Negative/Polychrome/etc = fail fast)
                        if (!grouped.ContainsKey(FilterCategory.SoulJokerEditionOnly))
                            grouped[FilterCategory.SoulJokerEditionOnly] =
                                new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                        grouped[FilterCategory.SoulJokerEditionOnly].Add(clause);

                        // If also has specific joker type, the full SoulJoker filter will be added below
                        // EditionOnly pre-filters → SoulJoker verifies type (chained as additional filter)
                        if (!hasSpecificJokerType)
                            continue; // Edition-only, no type check needed
                    }
                }

                // CRITICAL OPTIMIZATION: Route rare-edition shop jokers to pre-filter for ultra-fast early-exit!
                // Pre-filter peeks rarity+edition only (no type generation) - rejects 99.985% instantly
                // Then chains to precise JokerFilterDesc for exact slot verification
                if (category == FilterCategory.Joker)
                {
                    // Use pre-parsed enum - no string comparisons!
                    bool shouldUsePreFilter =
                        clause.EditionEnum.HasValue
                        && clause.EditionEnum.Value != MotelyItemEdition.None;

                    if (shouldUsePreFilter)
                    {
                        category = FilterCategory.JokerRarityEditionPreFilter;
                    }
                }

                if (!grouped.ContainsKey(category))
                {
                    grouped[category] = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                }

                grouped[category].Add(clause);
            }

            // CRITICAL OPTIMIZATION: Combine ErraticRank and ErraticSuit into single filter for max performance
            // If we have BOTH rank and suit clauses, merge them into ErraticRankAndSuit to avoid double-looping
            if (
                grouped.ContainsKey(FilterCategory.ErraticRank)
                && grouped.ContainsKey(FilterCategory.ErraticSuit)
            )
            {
                var combinedClauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                combinedClauses.AddRange(grouped[FilterCategory.ErraticRank]);
                combinedClauses.AddRange(grouped[FilterCategory.ErraticSuit]);

                grouped[FilterCategory.ErraticRankAndSuit] = combinedClauses;
                grouped.Remove(FilterCategory.ErraticRank);
                grouped.Remove(FilterCategory.ErraticSuit);
            }

            return grouped;
        }
    }
}
