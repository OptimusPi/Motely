using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters;

/// <summary>
/// ULTRA-FAST rarity+edition pre-filter for shop jokers
/// Only peeks rarity + edition streams (2 cheap PRNG calls per slot)
/// </summary>
public readonly struct MotelyJsonJokerRarityEditionPreFilterDesc(
    MotelyJsonJokerFilterCriteria criteria
)
    : IMotelySeedFilterDesc<MotelyJsonJokerRarityEditionPreFilterDesc.MotelyJsonJokerRarityEditionPreFilter>
{
    private readonly MotelyJsonJokerFilterCriteria _criteria = criteria;

    public MotelyJsonJokerRarityEditionPreFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache shop streams for all antes (rarity+edition only - ultra fast!)
        for (int ante = _criteria.MinAnte; ante <= _criteria.MaxAnte; ante++)
        {
            // Cache shop stream - we'll create edition-only joker streams dynamically
            ctx.CacheShopStream(ante);
        }

        return new MotelyJsonJokerRarityEditionPreFilter(
            _criteria.Clauses,
            _criteria.MinAnte,
            _criteria.MaxAnte
        );
    }

    public struct MotelyJsonJokerRarityEditionPreFilter : IMotelySeedFilter
    {
        private readonly List<MotelyJsonJokerFilterClause> _clauses;
        private readonly int _minAnte;
        private readonly int _maxAnte;

        public MotelyJsonJokerRarityEditionPreFilter(
            List<MotelyJsonJokerFilterClause> clauses,
            int minAnte,
            int maxAnte
        )
        {
            _clauses = clauses;
            _minAnte = minAnte;
            _maxAnte = maxAnte;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            VectorMask resultMask = VectorMask.NoBitsSet;

            // For each clause, check if ANY ante has matching rarity+edition
            foreach (var clause in _clauses)
            {
                VectorMask clauseMatched = VectorMask.NoBitsSet;

                // Determine target rarity from clause
                MotelyJokerRarity? targetRarity = null;
                if (clause.JokerType.HasValue)
                {
                    // Extract rarity from specific joker type (e.g., Blueprint is Rare)
                    targetRarity = (MotelyJokerRarity)((int)clause.JokerType.Value & 0xF00);
                }
                else if (clause.JokerTypes != null && clause.JokerTypes.Count > 0)
                {
                    // Multiple types - extract rarity from first one (assume same rarity)
                    targetRarity = (MotelyJokerRarity)((int)clause.JokerTypes[0] & 0xF00);
                }
                // If no specific type (AnyRare/AnyCommon/etc.), targetRarity stays null

                // Check each ante
                for (int ante = _minAnte; ante <= _maxAnte; ante++)
                {
                    if (ante >= clause.WantedAntes.Length || !clause.WantedAntes[ante])
                        continue;

                    // Determine how many shop slots to check
                    int maxShopSlots = MotelyJsonScoring.GetDefaultShopSlotsForAnte(ante);

                    // Create shop joker stream with RARITY+EDITION only (ExcludeJokerType!)
                    var shopJokerStream = ctx.CreateShopJokerStream(
                        ante,
                        MotelyJokerStreamFlags.ExcludeJokerType
                            | MotelyJokerStreamFlags.ExcludeStickers
                    );

                    // Walk shop slots and peek rarity + edition
                    for (int slot = 0; slot < maxShopSlots; slot++)
                    {
                        // Get joker with rarity+edition (no type generation - FAST!)
                        var joker = ctx.GetNextJoker(ref shopJokerStream);

                        VectorMask slotMatches = VectorMask.AllBitsSet;

                        // Check rarity if we have a target rarity
                        if (targetRarity.HasValue)
                        {
                            // Extract rarity from joker (bits 8-11)
                            var jokerRarity = new VectorEnum256<MotelyJokerRarity>(
                                Vector256.BitwiseAnd(joker.Value, Vector256.Create(0xF00))
                            );
                            slotMatches &= VectorEnum256.Equals(jokerRarity, targetRarity.Value);
                        }
                        // If no targetRarity (AnyJoker), skip rarity check

                        // Check edition if specified (ULTRA RARE - 0.3% for Negative!)
                        if (clause.EditionEnum.HasValue)
                        {
                            slotMatches &= VectorEnum256.Equals(
                                joker.Edition,
                                clause.EditionEnum.Value
                            );
                        }

                        // If this slot matched, mark this clause as matched
                        clauseMatched |= slotMatches;

                        // EARLY EXIT: If all lanes matched, no need to check more slots
                        if (clauseMatched.IsAllTrue())
                            break;
                    }

                    // EARLY EXIT: If all lanes matched, no need to check more antes
                    if (clauseMatched.IsAllTrue())
                        break;
                }

                // All clauses must match (AND logic)
                if (clauseMatched.IsAllFalse())
                    return VectorMask.NoBitsSet; // Early exit if any clause failed

                resultMask |= clauseMatched;
            }

            return resultMask;
        }
    }
}
