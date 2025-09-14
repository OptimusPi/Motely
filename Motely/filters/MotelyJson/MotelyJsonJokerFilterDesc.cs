using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
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
        private readonly List<MotelyJsonJokerFilterClause> Clauses = clauses;
        private readonly int MinAnte = minAnte;
        private readonly int MaxAnte = maxAnte;
        private readonly int MaxShopSlotsNeeded = CalculateMaxShopSlotsNeeded(clauses);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var _clauses = Clauses;
            int _minAnte = MinAnte;
            int _maxAnte = MaxAnte;
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            

            // Stack-allocated clause masks - accumulate results per clause across all antes
            Span<VectorMask> clauseMasks = stackalloc VectorMask[Clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;

            // Initialize run state for voucher calculations
            var runState = ctx.Deck.GetDefaultRunState();
            
            // Loop antes first, then clauses - ensures one stream per ante!
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                for (int clauseIndex = 0; clauseIndex < Clauses.Count; clauseIndex++)
                {
                    var clause = Clauses[clauseIndex];
                    ulong anteBit = 1UL << (ante - 1);
                    
                    // Skip ante if not in bitmask
                    if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0)
                        continue;

                    VectorMask clauseResult = VectorMask.NoBitsSet;

                    // Check shops only if ShopSlotBitmask is non-zero (has shop slots to check)
                    if (clause.ShopSlotBitmask != 0)
                    {
                        // Use the self-contained shop joker stream - NO SYNCHRONIZATION ISSUES!
                        var shopJokerStream = ctx.CreateShopJokerStreamNew(ante);
                        
                        clauseResult |= CheckShopJokerVectorizedNew(clause, ctx, ref shopJokerStream);
                    }

                    if (clause.PackSlotBitmask != 0)
                    {
                        // Check buffoon packs for jokers
                        var packStream = ctx.CreateBoosterPackStream(ante, isCached: true, generatedFirstPack: ante != 1);
                        var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                        
                        clauseResult |= CheckPackJokersVectorized(clause, ctx, ref packStream, ref buffoonStream, ante);
                    }

                    // Accumulate results for this clause across all antes (OR logic)
                    clauseMasks[clauseIndex] |= clauseResult;
                }
                
                // Early exit check: After each ante, check if we can already determine failure
                // A clause can only succeed if it finds a match in at least one of its specified antes
                // Check if any clause that should have matched by now has no matches
                bool canEarlyExit = false;
                for (int i = 0; i < Clauses.Count; i++)
                {
                    var clause = Clauses[i];
                    // Check if this clause has any antes left to check
                    bool hasAntesRemaining = false;
                    for (int futureAnte = ante + 1; futureAnte <= _maxAnte; futureAnte++)
                    {
                        ulong futureBit = 1UL << (futureAnte - 1);
                        if (clause.AnteBitmask == 0 || (clause.AnteBitmask & futureBit) != 0)
                        {
                            hasAntesRemaining = true;
                            break;
                        }
                    }
                    
                    // If this clause has no matches and no antes left to check, we can exit
                    if (clauseMasks[i].IsAllFalse() && !hasAntesRemaining)
                    {
                        canEarlyExit = true;
                        break;
                    }
                }
                
                if (canEarlyExit)
                    return VectorMask.NoBitsSet;
            }

            // All clauses must be satisfied (AND logic)
            // CRITICAL FIX: If any clause found nothing (NoBitsSet), the entire filter fails!
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                DebugLogger.Log($"[JOKER VECTORIZED] Clause {i} mask: {clauseMasks[i].Value:X}");
                
                // FIX: If this clause found nothing across all antes, fail immediately
                if (clauseMasks[i].IsAllFalse())
                {
                    DebugLogger.Log($"[JOKER VECTORIZED] Clause {i} found no matches - failing all seeds");
                    return VectorMask.NoBitsSet;
                }
                
                resultMask &= clauseMasks[i];
                DebugLogger.Log($"[JOKER VECTORIZED] Result after clause {i}: {resultMask.Value:X}");
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }

            DebugLogger.Log($"[JOKER VECTORIZED] Final result mask: {resultMask.Value:X}");
            
            // ALWAYS verify with individual seed search to avoid SIMD bugs with pack streams
            // The vectorized search acts as a pre-filter, but we verify each passing seed individually
            if (resultMask.IsAllFalse())
            {
                return VectorMask.NoBitsSet;
            }

            // Copy struct fields to local variables for lambda
            var clauses = Clauses; // Copy _clauses to a local variable
            var minAnte = MinAnte;
            var maxAnte = MaxAnte;
            
            // FIX: Ensure we have clauses to check
            if (clauses == null || clauses.Count == 0)
            {
                DebugLogger.Log("[JOKER FILTER] ERROR: No clauses for individual verification!");
                return VectorMask.NoBitsSet; // NO seeds should pass without clauses!
            }
            
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    // Re-check all clauses for this individual seed
                    foreach (var clause in clauses)
                    {
                        bool clauseSatisfied = false;

                        // Check all antes for this clause - use local variables!
                        for (int ante = minAnte; ante <= maxAnte; ante++)
                        {
                            ulong anteBit = 1UL << (ante - 1);
                            if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0)
                                continue;

                            // Check shops only if ShopSlotBitmask is non-zero
                            if (clause.ShopSlotBitmask != 0)
                            {
                                var shopStream = singleCtx.CreateShopItemStream(ante, isCached: false);
                                if (CheckShopJokersSingleStatic(ref singleCtx, clause, ante, ref shopStream))
                                {
                                    clauseSatisfied = true;
                                    break;
                                }
                            }

                            // Check packs
                            if (clause.PackSlotBitmask != 0)
                            {
                                var packStream = singleCtx.CreateBoosterPackStream(ante, generatedFirstPack: ante != 1, isCached: false);
                                if (CheckPackJokersSingleStatic(ref singleCtx, clause, ante, ref packStream))
                                {
                                    clauseSatisfied = true;
                                    break;
                                }
                            }
                        }

                        if (!clauseSatisfied)
                            return false; // This seed doesn't satisfy this clause
                    }

                    return true; // All clauses satisfied
                });
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
                    
                    // Check if any lanes have jokers - ONLY CHECK VALID LANES!
                    uint jokerMask = 0;
                    for (int i = 0; i < 8; i++)
                        if (ctx.IsLaneValid(i) && isJoker[i] == -1) jokerMask |= (1u << i);
                    
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
            
            // ALWAYS iterate through ALL shop slots to maintain stream sync!
            // If no bitmask specified (0), check all slots
            // If bitmask specified, still need to iterate through all slots but only check specified ones
            int maxSlot = MaxShopSlotsNeeded; // Always use the max slots needed, not just up to highest bit!
            
            // Check each shop slot using the self-contained stream
            for (int slot = 0; slot < maxSlot; slot++)
            {
                // ALWAYS get the next item to maintain stream synchronization
                var jokerItem = shopJokerStream.GetNext(ref ctx);
                
                // Only check/score if this slot is in the bitmask (0 = check all slots)
                if (clause.ShopSlotBitmask == 0 || ((clause.ShopSlotBitmask >> slot) & 1) != 0)
                {
                    // PURE VECTORIZED CHECK - no per-lane loops!
                    // Check if it's not JokerExcludedByStream using SIMD compare
                    var excludedValue = Vector256.Create((int)MotelyItemType.JokerExcludedByStream);
                    var isNotExcluded = ~Vector256.Equals(jokerItem.Value, excludedValue);
                    VectorMask isActualJoker = isNotExcluded;

                    if (!isActualJoker.IsAllFalse())
                    {
                        // Check if the joker matches our clause criteria
                        VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                        foundInShop |= (isActualJoker & matches);
                    }
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckPackJokersVectorized(MotelyJsonJokerFilterClause clause, MotelyVectorSearchContext ctx,
            ref MotelyVectorBoosterPackStream packStream, ref MotelyVectorJokerStream buffoonStream, int ante)
        {
            VectorMask foundInPack = VectorMask.NoBitsSet;
            
            // FIXED: Properly limit packs based on ante
            // Ante 1: exactly 4 packs, Ante 2+: exactly 6 packs  
            int actualPackLimit = ante == 1 ? 4 : 6;
            
            // Check enough packs to cover the bitmask, but never exceed actual pack limit
            int maxPacksToCheck = clause.PackSlotBitmask == 0 ? actualPackLimit : 
                Math.Min(actualPackLimit, 64 - System.Numerics.BitOperations.LeadingZeroCount(clause.PackSlotBitmask));
            
            for (int packIndex = 0; packIndex < maxPacksToCheck; packIndex++)
            {
                // Always get next pack to maintain stream sync
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Check if it's a Buffoon pack - we MUST consume jokers if it is!
                VectorMask isBuffoonPack = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Buffoon);
                
                if (!isBuffoonPack.IsAllFalse())
                {
                    // FIXED: Handle different pack sizes properly per-lane
                    // Get pack sizes for each lane that has a Buffoon pack
                    var packSizes = pack.GetPackSize();
                    
                    // Determine max pack size across all lanes to ensure we consume enough from stream
                    // Standard = 2, Jumbo = 3, Mega = 4-5 (let's use 5 to be safe)
                    int maxPackSize = 5; // Maximum possible pack size
                    
                    // Get ALL jokers from the pack (up to max size) to keep stream in sync
                    var packContents = ctx.GetNextBuffoonPackContents(ref buffoonStream, maxPackSize);
                    
                    // Only SCORE if this pack slot is in our bitmask
                    if (((clause.PackSlotBitmask >> packIndex) & 1) != 0)
                    {
                        // Check if it's a Mega pack if required
                        if (clause.Sources?.RequireMega == true)
                        {
                            VectorMask isMegaPack = VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Mega);
                            isBuffoonPack &= isMegaPack;
                        }
                        
                        // Check each joker in the pack for scoring
                        // We check all positions but only count valid ones based on actual pack size
                        for (int i = 0; i < maxPackSize; i++)
                        {
                            // Create mask for lanes where this card position is valid
                            // Standard (2): positions 0,1 valid
                            // Jumbo (3): positions 0,1,2 valid  
                            // Mega (4-5): positions 0,1,2,3,4 valid
                            VectorMask isValidPosition = i switch
                            {
                                0 or 1 => VectorMask.AllBitsSet, // All packs have at least 2 cards
                                2 => ~VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Normal),
                                3 or 4 => VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Mega),
                                _ => VectorMask.NoBitsSet
                            };
                            
                            var jokerItem = packContents[i];
                            VectorMask matches = CheckJokerMatchesClause(jokerItem, clause);
                            foundInPack |= (isBuffoonPack & isValidPosition & matches);
                        }
                    }
                }
            }
            
            return foundInPack;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckJokerMatchesClause(MotelyItemVector item, MotelyJsonJokerFilterClause clause)
        {
            VectorMask matches = VectorMask.AllBitsSet;

            // Check type if specified - PURE SIMD, no loops over lanes!
            if (clause.JokerTypes != null && clause.JokerTypes.Count > 0)
            {
                VectorMask typeMatch = VectorMask.NoBitsSet;
                foreach (var jokerType in clause.JokerTypes)
                {
                    var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)jokerType);
                    typeMatch |= VectorEnum256.Equals(item.Type, targetType);
                }
                matches &= typeMatch;
            }
            else if (clause.JokerType != null)
            {
                var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)clause.JokerType);
                matches &= VectorEnum256.Equals(item.Type, targetType);
            }
            else
            {
                // Match any joker
                matches &= VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker);
            }

            // Check edition if specified - PURE SIMD!
            if (clause.EditionEnum.HasValue)
            {
                VectorMask editionMatches = VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
                DebugLogger.Log($"[JOKER EDITION CHECK] Required: {clause.EditionEnum.Value}, Item editions: {item.Edition[0]},{item.Edition[1]},{item.Edition[2]},{item.Edition[3]},{item.Edition[4]},{item.Edition[5]},{item.Edition[6]},{item.Edition[7]}");
                DebugLogger.Log($"[JOKER EDITION CHECK] Edition matches mask: {editionMatches.Value:X}");
                matches &= editionMatches;
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
        private static bool CheckShopJokersSingleStatic(ref MotelySingleSearchContext ctx, MotelyJsonJokerFilterClause clause, int ante, ref MotelySingleShopItemStream shopStream)
        {
            int maxSlot = clause.ShopSlotBitmask == 0 ? (ante == 1 ? 4 : 6) : 
                64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask);
            
            for (int slot = 0; slot < maxSlot; slot++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                
                // Check if this slot is in our bitmask (0 = check all)
                if (clause.ShopSlotBitmask == 0 || ((clause.ShopSlotBitmask >> slot) & 1) != 0)
                {
                    if (item.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        var joker = item.GetJoker();
                        bool matches = !clause.IsWildcard ?
                            joker == clause.JokerType :
                            CheckWildcardMatch(joker, clause.WildcardEnum);
                        
                        if (matches && CheckEditionAndStickersSingle(item, clause))
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckPackJokersSingleStatic(ref MotelySingleSearchContext ctx, MotelyJsonJokerFilterClause clause, int ante, ref MotelySingleBoosterPackStream packStream)
        {
            int maxPacks = ante == 1 ? 4 : 6;
            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
            
            for (int packIndex = 0; packIndex < maxPacks; packIndex++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                var packSize = pack.GetPackSize();
                
                if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                {

                    var packContents = ctx.GetNextBuffoonPackContents(ref buffoonStream, packSize);

                    // Check if this pack slot is in our bitmask
                    if (((clause.PackSlotBitmask >> packIndex) & 1) != 0)
                    {
                        if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                            continue;

                        for (int i = 0; i < packContents.Length; i++)
                        {
                            var item = packContents[i];
                            var joker = item.GetJoker();
                            bool matches = !clause.IsWildcard ?
                                joker == clause.JokerType :
                                CheckWildcardMatch(joker, clause.WildcardEnum);

                            if (matches && CheckEditionAndStickersSingle(item, clause))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckEditionAndStickersSingle(in MotelyItem item, MotelyJsonJokerFilterClause clause)
        {
            if (clause.EditionEnum.HasValue && item.Edition != clause.EditionEnum.Value)
                return false;
            
            if (clause.StickerEnums?.Count > 0)
            {
                foreach (var sticker in clause.StickerEnums)
                {
                    var hasSticker = sticker switch
                    {
                        MotelyJokerSticker.Eternal => item.IsEternal,
                        MotelyJokerSticker.Perishable => item.IsPerishable,
                        MotelyJokerSticker.Rental => item.IsRental,
                        _ => true
                    };
                    if (!hasSticker) return false;
                }
            }
            
            return true;
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
