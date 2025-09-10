using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on joker criteria from JSON configuration.
/// UPDATED: Now uses centralized shop type detection for efficiency
/// </summary>
public struct MotelyJsonJokerFilterDesc(List<MotelyJsonJokerFilterClause> jokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>
{
    private readonly List<MotelyJsonJokerFilterClause> _jokerClauses = jokerClauses;

    public MotelyJsonJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Calculate ante range from bitmasks
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_jokerClauses);
        
        // Cache streams for needed antes
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }
        
        return new MotelyJsonJokerFilter(_jokerClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonJokerFilter(List<MotelyJsonJokerFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonJokerFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // Copy struct fields to local variables for lambda
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            
            // Process all seeds directly with efficient bitmask filtering
            return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Stack-allocated to avoid heap allocation in hot path!
                Span<bool> clauseMatches = stackalloc bool[clauses.Count];
                for (int i = 0; i < clauseMatches.Length; i++) clauseMatches[i] = false;
                
                // ANTE LOOP FIRST - using pre-calculated range
                for (int ante = minAnte; ante <= maxAnte; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    
                    // ✅ NEW: Use centralized shop type detection
                    int maxShopSlotsNeeded = GetMaxShopSlotsNeeded(clauses);
                    var shopTypes = ctx.GetShopSlotTypes(ante, maxShopSlotsNeeded);
                    
                    // CLAUSE LOOP SECOND
                    for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                    {
                        var clause = clauses[clauseIndex];
                        // Check ante bitmask
                        if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                        
                        // ✅ NEW: Check shop slots using pre-detected types
                        if (clause.ShopSlotBitmask != 0)
                        {
                            CheckShopSlots(ref singleCtx, clause, shopTypes, anteBit, ref clauseMatches[clauseIndex], ante);
                        }
                        
                        // Check pack slots using bitmask (unchanged)
                        if (clause.PackSlotBitmask != 0)
                        {
                            CheckPackSlots(ref singleCtx, clause, ante, ref clauseMatches[clauseIndex]);
                        }
                    }
                }
                
                // Combine all clause results - ALL clauses must match
                bool allClausesMatched = true;
                for (int i = 0; i < clauseMatches.Length; i++)
                {
                    if (!clauseMatches[i])
                    {
                        allClausesMatched = false;
                        break;
                    }
                }
                
                return allClausesMatched;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckShopSlots(ref MotelySingleSearchContext singleCtx, 
            MotelyJsonJokerFilterClause clause, 
            VectorEnum256<MotelyItemTypeCategory>[] shopTypes,
            ulong anteBit,
            ref bool clauseMatched,
            int ante)
        {
            // Only create shop stream if we find joker slots
            bool hasJokerSlots = false;
            int maxSlot = shopTypes.Length;
            
            for (int i = 0; i < maxSlot; i++)
            {
                if (((clause.ShopSlotBitmask >> i) & 1) != 0)
                {
                    // Check if this slot contains a joker type (vectorized check would be here)
                    // For now, assume we need to check - in full vectorized version this would be optimized
                    hasJokerSlots = true;
                    break;
                }
            }
            
            if (!hasJokerSlots) return;
            
            // NOW create the shop stream only for joker processing
            var shopStream = singleCtx.CreateShopItemStream(ante, isCached: false);
            
            for (int i = 0; i < maxSlot; i++)
            {
                var item = singleCtx.GetNextShopItem(ref shopStream);
                if (((clause.ShopSlotBitmask >> i) & 1) != 0 && item.TypeCategory == MotelyItemTypeCategory.Joker)
                {
                    var joker = new MotelyItem(item.Value).GetJoker();
                    bool typeMatches = CheckJokerTypeMatch(joker, clause);
                    bool editionMatches = !clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value;
                    
                    if (typeMatches && editionMatches)
                    {
                        clauseMatched = true;
                        #if DEBUG
                        DebugLogger.Log($"[Joker] Found {joker} at shop slot {i} in ante {ante}");
                        #endif
                        return;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckPackSlots(ref MotelySingleSearchContext singleCtx,
            MotelyJsonJokerFilterClause clause,
            int ante,
            ref bool clauseMatched)
        {
            var packStream = singleCtx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
            var buffoonStream = singleCtx.CreateBuffoonPackJokerStream(ante);
            
            // Find the highest bit set to know how many packs to check
            int maxPack = 64 - System.Numerics.BitOperations.LeadingZeroCount(clause.PackSlotBitmask);
            for (int i = 0; i < maxPack; i++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref packStream);
                if (((clause.PackSlotBitmask >> i) & 1) != 0 && pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;
                    
                    var contents = singleCtx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents[j];
                        var joker = item.GetJoker();
                        bool typeMatches = CheckJokerTypeMatch(joker, clause);
                        bool editionMatches = !clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value;
                        
                        if (typeMatches && editionMatches)
                        {
                            clauseMatched = true;
                            return;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckJokerTypeMatch(MotelyJoker joker, MotelyJsonJokerFilterClause clause)
        {
            if (!clause.IsWildcard)
            {
                if (clause.JokerTypes?.Count > 0)
                {
                    // Multi-value: OR logic - match any joker in the list
                    return clause.JokerTypes.Contains(joker);
                }
                else
                {
                    // Single value
                    return joker == clause.JokerType;
                }
            }
            else
            {
                return CheckWildcardMatch(joker, clause.WildcardEnum);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMaxShopSlotsNeeded(List<MotelyJsonJokerFilterClause> clauses)
        {
            if (clauses == null || clauses.Count == 0)
                return 6; // Default to 6 for ante 2+ extended slots
            
            int maxSlot = 0;
            bool hasUnspecifiedSlots = false;
            
            foreach (var clause in clauses)
            {
                if (clause.ShopSlotBitmask == 0)
                {
                    hasUnspecifiedSlots = true;
                    continue;
                }
                
                // Find highest bit set in bitmask
                ulong mask = clause.ShopSlotBitmask;
                int slot = 0;
                while (mask > 0)
                {
                    if ((mask & 1) != 0)
                        maxSlot = Math.Max(maxSlot, slot);
                    mask >>= 1;
                    slot++;
                }
            }
            
            // If any clause doesn't specify slots, use the 4/6 concept
            if (hasUnspecifiedSlots)
            {
                return Math.Max(6, maxSlot + 1); // At least 6, more if specific high slots requested
            }
            else
            {
                return Math.Max(4, maxSlot + 1);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckWildcardMatch(MotelyJoker joker, MotelyJsonConfigWildcards? wildcard)
    {
        if (!wildcard.HasValue) return false;
        if (wildcard == MotelyJsonConfigWildcards.AnyJoker) return true;

        var rarity = (MotelyJokerRarity)((int)joker & Motely.JokerRarityMask);
        return wildcard switch
        {
            MotelyJsonConfigWildcards.AnyCommon => rarity == MotelyJokerRarity.Common,
            MotelyJsonConfigWildcards.AnyUncommon => rarity == MotelyJokerRarity.Uncommon,
            MotelyJsonConfigWildcards.AnyRare => rarity == MotelyJokerRarity.Rare,
            _ => false
        };
    }
}
