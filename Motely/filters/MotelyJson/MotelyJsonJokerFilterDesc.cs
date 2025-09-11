using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on joker criteria from JSON configuration.
/// REVERTED: Simple version that compiles with fixed slot range
/// </summary>
public partial struct MotelyJsonJokerFilterDesc(List<MotelyJsonJokerFilterClause> jokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>
{
    private readonly List<MotelyJsonJokerFilterClause> _jokerClauses = jokerClauses;

    public MotelyJsonJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_jokerClauses);
        
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
        private readonly int _maxShopSlotsNeeded = CalculateMaxShopSlotsNeeded(clauses);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;

            // Stack-allocated clause masks - accumulate results per clause across all antes
            Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;

            // Initialize run state for voucher calculations
            var runState = ctx.Deck.GetDefaultRunState();
            
            // Loop antes first, then clauses - ensures one stream per ante!
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
                {
                    var clause = _clauses[clauseIndex];
                    ulong anteBit = 1UL << (ante - 1);
                    
                    // Skip ante if not in bitmask
                    if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0)
                        continue;

                    VectorMask clauseResult = VectorMask.NoBitsSet;

                    if (clause.ShopSlotBitmask != 0)
                    {
                        // Use the self-contained shop joker stream - NO SYNCHRONIZATION ISSUES!
                        var shopJokerStream = ctx.CreateShopJokerStreamNew(ante);
                        
                        clauseResult |= CheckShopJokerVectorizedNew(clause, ctx, ref shopJokerStream);
                    }

                    if (clause.PackSlotBitmask != 0)
                    {
                        // Skip pack processing for now - focus on shops
                        // TODO: Implement pack filtering
                    }

                    // Accumulate results for this clause across all antes (OR logic)
                    clauseMasks[clauseIndex] |= clauseResult;
                }
            }

            // All clauses must be satisfied (AND logic)
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                DebugLogger.Log($"[JOKER VECTORIZED] Clause {i} mask: {clauseMasks[i].Value:X}");
                resultMask &= clauseMasks[i];
                DebugLogger.Log($"[JOKER VECTORIZED] Result after clause {i}: {resultMask.Value:X}");
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }

            DebugLogger.Log($"[JOKER VECTORIZED] Final result mask: {resultMask.Value:X}");
            return resultMask;
            
            // OLD SearchIndividualSeeds code below (removed for brevity)
            /*
            return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
                Span<bool> clauseMatches = stackalloc bool[clauses.Count];
                for (int i = 0; i < clauseMatches.Length; i++) clauseMatches[i] = false;
                
                for (int ante = minAnte; ante <= maxAnte; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    
                    for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                    {
                        var clause = clauses[clauseIndex];
                        if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                        
                        var shopStream = singleCtx.CreateShopItemStream(ante, isCached: false);
                        int maxSlot = GetMaxShopSlot(clause.ShopSlotBitmask, ante);
                        
                        for (int i = 0; i < maxSlot; i++)
                        {
                            var item = singleCtx.GetNextShopItem(ref shopStream);
                            bool shouldCheckSlot = clause.ShopSlotBitmask == 0 || ((clause.ShopSlotBitmask >> i) & 1) != 0;
                            
                            if (shouldCheckSlot && item.TypeCategory == MotelyItemTypeCategory.Joker)
                            {
                                var joker = new MotelyItem(item.Value).GetJoker();
                                bool typeMatches = CheckJokerTypeMatch(joker, clause);
                                bool editionMatches = !clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value;
                                
                                if (typeMatches && editionMatches)
                                {
                                    clauseMatches[clauseIndex] = true;
                                    break; // Found a match for this clause
                                }
                            }
                        }
                        
                        // Check pack slots
                        if (clause.PackSlotBitmask != 0)
                        {
                            var packStream = singleCtx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
                            var buffoonStream = singleCtx.CreateBuffoonPackJokerStream(ante);
                            
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
                                            clauseMatches[clauseIndex] = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
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
            */
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopVectorized(ref MotelyVectorSearchContext ctx, int ante, MotelyJsonJokerFilterClause clause, ref MotelyVectorShopItemStream shopStream, MotelyRunState runState)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;


            // Calculate the highest slot we need to check
            int maxSlot = clause.ShopSlotBitmask == 0 ? 0 : 64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask);
            
            // Read through slots sequentially up to maxSlot, checking only those in the bitmask
            for (int slot = 0; slot < maxSlot; slot++)
            {
                ulong slotBit = 1UL << slot;
                
                // Get the shop item - the stream handles all rate calculations internally
                var item = ctx.GetNextShopItem(ref shopStream);
                
                // Check if this slot is in the bitmask
                if ((clause.ShopSlotBitmask & slotBit) != 0)
                {
                    DebugLogger.Log($"[JOKER VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}");
                    
                    // Check if this slot has a joker
                    var isJoker = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker);
                    
                    // Check if any lanes have jokers
                    uint jokerMask = 0;
                    for (int i = 0; i < 8; i++)
                        if (isJoker[i] == -1) jokerMask |= (1u << i);
                    
                    if (jokerMask != 0) // Any lanes have jokers
                    {
                        DebugLogger.Log($"[JOKER VECTORIZED] Found joker at shop slot {slot}: {item.Type[0]}, expecting: {clause.JokerType}");
                        // Check if it matches our clause
                        VectorMask matches = CheckJokerMatchesClause(item, clause);
                        DebugLogger.Log($"[JOKER VECTORIZED] Matches mask={matches.Value:X}");
                        foundInShop |= matches;
                    }
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopJokerVectorizedNew(MotelyJsonJokerFilterClause clause, MotelyVectorSearchContext ctx, 
            ref MotelyVectorShopJokerStream shopJokerStream)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;
            
            // Calculate max slot we need to check
            int maxSlot = clause.ShopSlotBitmask == 0 ? _maxShopSlotsNeeded : 
                (64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask));
            
            // Check each shop slot using the self-contained stream
            for (int slot = 0; slot < maxSlot; slot++)
            {
                ulong slotBit = 1UL << slot;
                
                // Get joker for this slot using self-contained stream - handles slot types internally!
                var jokerItem = shopJokerStream.GetNext(ref ctx);
                
                // Skip if this slot isn't in the bitmask (0 = check all slots)
                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & slotBit) == 0)
                    continue;
                
                // Check if item is JokerExcludedByStream (not a joker slot)
                VectorMask isActualJoker = VectorMask.AllBitsSet;
                for (int lane = 0; lane < 8; lane++)
                {
                    if (jokerItem.Value[lane] == (int)MotelyItemType.JokerExcludedByStream)
                        isActualJoker[lane] = false;
                }
                
                if (isActualJoker.IsPartiallyTrue())
                {
                    // Check if the joker matches our clause criteria
                    VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                    foundInShop |= (isActualJoker & matches);
                }
            }

            return foundInShop;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckJokerMatchesClause(MotelyItemVector item, MotelyJsonJokerFilterClause clause)
        {
            VectorMask matches = VectorMask.AllBitsSet;

            // Check type if specified
            if (clause.JokerTypes != null && clause.JokerTypes.Count > 0)
            {
                VectorMask typeMatch = VectorMask.NoBitsSet;
                foreach (var jokerType in clause.JokerTypes)
                {
                    var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)jokerType);
                    var eqResult = VectorEnum256.Equals(item.Type, targetType);
                    uint mask = 0;
                    for (int i = 0; i < 8; i++)
                        if (eqResult[i] == -1) mask |= (1u << i);
                    typeMatch |= new VectorMask(mask);
                }
                matches &= typeMatch;
            }
            else if (clause.JokerType != null)
            {
                var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)clause.JokerType);
                var eqResult = VectorEnum256.Equals(item.Type, targetType);
                uint mask = 0;
                for (int i = 0; i < 8; i++)
                    if (eqResult[i] == -1) mask |= (1u << i);
                matches &= new VectorMask(mask);
            }
            else
            {
                // Match any joker
                var eqResult = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker);
                uint mask = 0;
                for (int i = 0; i < 8; i++)
                    if (eqResult[i] == -1) mask |= (1u << i);
                matches &= new VectorMask(mask);
            }

            // Check edition if specified
            if (clause.EditionEnum.HasValue)
            {
                var eqResult = VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
                uint mask = 0;
                for (int i = 0; i < 8; i++)
                    if (eqResult[i] == -1) mask |= (1u << i);
                matches &= new VectorMask(mask);
            }

            return matches;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMaxShopSlot(ulong bitmask, int ante)
        {
            if (bitmask == 0) return ante == 1 ? 4 : 6; // Default 4/6 concept
            
            // Find highest bit + 1, but ensure minimum of 6 for ante 2+ to handle extended slots
            int maxSpecified = 64 - System.Numerics.BitOperations.LeadingZeroCount(bitmask);
            return ante == 1 ? Math.Max(4, maxSpecified) : Math.Max(6, maxSpecified);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckJokerTypeMatch(MotelyJoker joker, MotelyJsonJokerFilterClause clause)
        {
            if (!clause.IsWildcard)
            {
                if (clause.JokerTypes?.Count > 0)
                {
                    return clause.JokerTypes.Contains(joker);
                }
                else
                {
                    return joker == clause.JokerType;
                }
            }
            else
            {
                return CheckWildcardMatch(joker, clause.WildcardEnum);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateMaxShopSlotsNeeded(List<MotelyJsonJokerFilterClause> clauses)
        {
            int maxSlotNeeded = 0;
            foreach (var clause in clauses)
            {
                if (clause.ShopSlotBitmask != 0)
                {
                    int clauseMaxSlot = 64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask);
                    maxSlotNeeded = Math.Max(maxSlotNeeded, clauseMaxSlot);
                }
                else
                {
                    // If no slot restrictions, check a reasonable number of slots (e.g., 8)
                    maxSlotNeeded = Math.Max(maxSlotNeeded, 8);
                }
            }
            return maxSlotNeeded;
        }
    }
}
