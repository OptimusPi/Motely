using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on spectral card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonSpectralCardFilterDesc(List<MotelyJsonSpectralFilterClause> spectralClauses)
    : IMotelySeedFilterDesc<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>
{
    private readonly List<MotelyJsonSpectralFilterClause> _spectralClauses = spectralClauses;

    public MotelyJsonSpectralCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Calculate ante range from bitmasks
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_spectralClauses);

        // Cache streams for all antes we'll check
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheShopStream(ante);
        }

        return new MotelyJsonSpectralCardFilter(_spectralClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonSpectralCardFilter(List<MotelyJsonSpectralFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonSpectralFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;
        private readonly int _maxShopSlotsNeeded = CalculateMaxShopSlotsNeeded(clauses);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;

            // Initialize run state for voucher calculations  
            var runState = ctx.Deck.GetDefaultRunState();

            // Stack-allocated clause masks - accumulate results per clause across all antes
            Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Count];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;

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

                    // Check shops if specified
                    if (clause.ShopSlotBitmask != 0)
                    {
                        // Use the self-contained shop spectral stream - NO SYNCHRONIZATION ISSUES!
                        var shopSpectralStream = ctx.CreateShopSpectralStreamNew(ante);
                        clauseResult |= CheckShopSpectralVectorizedNew(clause, ctx, ref shopSpectralStream);
                    }

                    // Check packs if specified
                    if (clause.PackSlotBitmask != 0)
                    {
                        clauseResult |= CheckPacksVectorized(clause, ctx, ante);
                    }

                    // Accumulate results for this clause across all antes (OR logic)
                    clauseMasks[clauseIndex] |= clauseResult;
                }
            }
            
            // AND all clause masks together - ALL clauses must match (like other filters)
            // CRITICAL FIX: If any clause found nothing (NoBitsSet), the entire filter fails!
            VectorMask finalResult = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                // FIX: If this clause found nothing across all antes, fail immediately
                if (clauseMasks[i].IsAllFalse())
                {
                    return VectorMask.NoBitsSet;
                }
                
                finalResult &= clauseMasks[i];
                if (finalResult.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            // ALWAYS verify with individual seed search to avoid SIMD bugs with pack streams
            var clauses = _clauses;
            return ctx.SearchIndividualSeeds(finalResult, (ref MotelySingleSearchContext singleCtx) =>
            {
                return CheckSpectralIndividualStatic(ref singleCtx, clauses);
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckShopVectorizedPrecomputed(MotelyJsonSpectralFilterClause clause, MotelyItemVector[] shopItems)
        {
            VectorMask shopResult = VectorMask.NoBitsSet;
            
            // Check each shop slot specified in the bitmask
            for (int shopSlot = 0; shopSlot < shopItems.Length; shopSlot++)
            {
                ulong shopSlotBit = 1UL << shopSlot;
                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & shopSlotBit) == 0)
                    continue;
                
                var shopItem = shopItems[shopSlot];
                
                // Check type match
                VectorMask typeMatches = VectorMask.AllBitsSet;
                if (clause.SpectralType.HasValue)
                {
                    // First check if it's a spectral card category
                    VectorMask isSpectralCard = VectorEnum256.Equals(shopItem.TypeCategory, MotelyItemTypeCategory.SpectralCard);
                    
                    // Construct the correct MotelyItemType for the spectral card
                    var targetSpectralType = (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralType.Value);
                    VectorMask correctSpectralType = VectorEnum256.Equals(shopItem.Type, targetSpectralType);
                    
                    typeMatches = isSpectralCard & correctSpectralType;
                }
                else
                {
                    // Wildcard match - any spectral card
                    typeMatches = VectorEnum256.Equals(shopItem.TypeCategory, MotelyItemTypeCategory.SpectralCard);
                }
                
                // Check edition match
                VectorMask editionMatches = VectorMask.AllBitsSet;
                if (clause.EditionEnum.HasValue)
                {
                    editionMatches = VectorEnum256.Equals(shopItem.Edition, clause.EditionEnum.Value);
                }
                
                // Combine type and edition
                VectorMask slotMatches = typeMatches & editionMatches;
                shopResult |= slotMatches;
            }
            
            return shopResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateMaxShopSlotsNeeded(List<MotelyJsonSpectralFilterClause> clauses)
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
                    // If no slot restrictions, check a reasonable number of slots (e.g., 10)
                    maxSlotNeeded = Math.Max(maxSlotNeeded, 10);
                }
            }
            return maxSlotNeeded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopSpectralVectorizedNew(MotelyJsonSpectralFilterClause clause, MotelyVectorSearchContext ctx, 
            ref MotelyVectorShopSpectralStream shopSpectralStream)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;
            
            // Calculate max slot we need to check
            int maxSlot = clause.ShopSlotBitmask == 0 ? _maxShopSlotsNeeded :
                (64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask));
            
            // Check each shop slot using the self-contained stream
            for (int slot = 0; slot < maxSlot; slot++)
            {
                // ALWAYS get spectral for this slot to maintain stream synchronization!
                var spectralItem = shopSpectralStream.GetNext(ref ctx);
                
                // Only SCORE/MATCH if this slot is in the bitmask (0 = check all slots)
                ulong slotBit = 1UL << slot;
                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & slotBit) == 0)
                    continue; // Don't score this slot, but we already consumed from stream
                
                // Check if item is SpectralExcludedByStream (not a spectral slot) using SIMD
                var excludedValue = Vector256.Create((int)MotelyItemType.SpectralExcludedByStream);
                var isNotExcluded = ~Vector256.Equals(spectralItem.Value, excludedValue);
                VectorMask isActualSpectral = isNotExcluded;
                
                if (isActualSpectral.IsPartiallyTrue())
                {
                    // Check if the spectral matches our clause criteria
                    VectorMask typeMatches = VectorMask.AllBitsSet;
                    if (clause.SpectralType.HasValue)
                    {
                        // FIX: Properly construct the MotelyItemType by combining category and spectral type
                        var targetSpectralType = (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralType.Value);
                        typeMatches = VectorEnum256.Equals(spectralItem.Type, targetSpectralType);
                    }
                    
                    VectorMask editionMatches = VectorMask.AllBitsSet;
                    if (clause.EditionEnum.HasValue)
                    {
                        editionMatches = VectorEnum256.Equals(spectralItem.Edition, clause.EditionEnum.Value);
                    }
                    
                    VectorMask matches = typeMatches & editionMatches;
                    foundInShop |= (isActualSpectral & matches);
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPacksVectorized(MotelyJsonSpectralFilterClause clause, MotelyVectorSearchContext ctx, int ante)
        {
            VectorMask foundInPacks = VectorMask.NoBitsSet;
            
            // Create pack streams
            var packStream = ctx.CreateBoosterPackStream(ante);
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
            
            // Determine max pack slot to check
            int maxPackSlot = clause.PackSlotBitmask == 0 ? (ante == 1 ? 4 : 6) : 
                (64 - System.Numerics.BitOperations.LeadingZeroCount(clause.PackSlotBitmask));
            
            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Check if this pack slot should be evaluated
                bool shouldCheckThisSlot = true;
                if (clause.PackSlotBitmask != 0)
                {
                    ulong packSlotBit = 1UL << packSlot;
                    shouldCheckThisSlot = (clause.PackSlotBitmask & packSlotBit) != 0;
                }
                
                var packType = pack.GetPackType();
                
                // Check Spectral packs with vectorized method
                VectorMask isSpectralPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Spectral);
                // ALWAYS consume spectral stream if it's a spectral pack to maintain sync
                if (isSpectralPack.IsPartiallyTrue())
                {
                    // Get pack sizes for proper stream consumption
                    var packSizes = pack.GetPackSize();

                    // Use the vectorized version that consumes correct number per lane!
                    var contents = ctx.GetNextSpectralPackContents(ref spectralStream, 5);

                    
                    // Only evaluate if we should check this slot
                    if (!shouldCheckThisSlot) continue;
                    
                    // Check requireMega constraint if specified
                    VectorMask packSizeOk = VectorMask.AllBitsSet;
                    if (clause.Sources?.RequireMega == true)
                    {
                        packSizeOk = VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Mega);
                    }
                    
                    // Check each card in the pack (up to max possible)
                    for (int cardIndex = 0; cardIndex < MotelyVectorItemSet.MaxLength; cardIndex++)
                    {
                        var card = contents[cardIndex];
                        
                        // Create mask for lanes where this card position actually exists in the pack
                        // We still need this check, but simpler: just check if the cardIndex is within pack size
                        VectorMask cardExistsInPack = cardIndex switch
                        {
                            0 or 1 => VectorMask.AllBitsSet, // All packs have at least 2 cards
                            2 => ~VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Normal), // Jumbo and Mega have 3rd card
                            3 or 4 => VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Mega), // Only Mega has 4th and 5th cards
                            _ => VectorMask.NoBitsSet
                        };
                        
                        // Check if this is a spectral card that matches our clause
                        VectorMask isSpectralCard = VectorEnum256.Equals(card.TypeCategory, MotelyItemTypeCategory.SpectralCard);
                        
                        if (isSpectralCard.IsPartiallyTrue())
                        {
                            VectorMask typeMatches = VectorMask.AllBitsSet;
                            if (clause.SpectralTypes?.Count > 0)
                            {
                                VectorMask anyTypeMatch = VectorMask.NoBitsSet;
                                foreach (var spectralType in clause.SpectralTypes)
                                {
                                    var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)spectralType);
                                    anyTypeMatch |= VectorEnum256.Equals(card.Type, targetType);
                                }
                                typeMatches = anyTypeMatch;
                            }
                            else if (clause.SpectralType.HasValue)
                            {
                                var targetSpectralType = (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralType.Value);
                                typeMatches = VectorEnum256.Equals(card.Type, targetSpectralType);
                            }
                            
                            VectorMask editionMatches = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatches = VectorEnum256.Equals(card.Edition, clause.EditionEnum.Value);
                            }
                            
                            // Only match if card exists in this pack size AND matches our criteria
                            VectorMask matches = (isSpectralPack & packSizeOk & cardExistsInPack & isSpectralCard & typeMatches & editionMatches);
                            foundInPacks |= matches;
                        }
                    }
                }
            }
            
            return ctx.SearchIndividualSeeds(foundInPacks, (ref MotelySingleSearchContext singleCtx) =>
            {
                return CheckPackSpectralsSingle(ref singleCtx, ante, clause);
            });
        }

        private static bool CheckSpectralIndividualStatic(ref MotelySingleSearchContext ctx, List<MotelyJsonSpectralFilterClause> clauses)
        {
            // Check each clause - all must be satisfied
            foreach (var clause in clauses)
            {
                bool clauseSatisfied = false;
                
                // Check all antes in the clause's bitmask
                for (int ante = 1; ante <= 64; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0)
                        continue;
                        
                    // Check shops if specified
                    if (clause.ShopSlotBitmask != 0)
                    {
                        var shopSpectralStream = ctx.CreateShopSpectralStream(ante);
                        if (CheckShopSpectralsSingle(ref ctx, ref shopSpectralStream, clause))
                        {
                            clauseSatisfied = true;
                            break;
                        }
                    }
                    
                    // Check packs if specified
                    if (clause.PackSlotBitmask != 0)
                    {
                        if (CheckPackSpectralsSingle(ref ctx, ante, clause))
                        {
                            clauseSatisfied = true;
                            break;
                        }
                    }
                }
                
                if (!clauseSatisfied)
                    return false; // This clause wasn't satisfied
            }
            
            return true; // All clauses satisfied
        }
        
        private static bool CheckShopSpectralsSingle(ref MotelySingleSearchContext ctx, ref MotelySingleSpectralStream stream, MotelyJsonSpectralFilterClause clause)
        {
            // Calculate max slot to check
            int maxSlot = clause.ShopSlotBitmask == 0 ? 16 : 
                (64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask));
            
            for (int slot = 0; slot < maxSlot; slot++)
            {
                // ALWAYS get spectral to maintain stream synchronization!
                var spectral = ctx.GetNextSpectral(ref stream);
                
                // Only SCORE/MATCH if this slot is in the bitmask (0 = check all)
                ulong slotBit = 1UL << slot;
                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & slotBit) == 0)
                    continue; // Don't score this slot, but we already consumed from stream
                
                // Skip if not a spectral slot
                if (spectral.Type == MotelyItemType.SpectralExcludedByStream)
                    continue;
                
                // Check if it matches our criteria
                bool matches = true;
                
                // Check type
                if (clause.SpectralTypes?.Count > 0)
                {
                    bool typeMatch = false;
                    foreach (var spectralType in clause.SpectralTypes)
                    {
                        if (spectral.Type == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)spectralType))
                        {
                            typeMatch = true;
                            break;
                        }
                    }
                    matches &= typeMatch;
                }
                else if (clause.SpectralType.HasValue)
                {
                    matches &= spectral.Type == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralType.Value);
                }
                
                // Check edition
                if (clause.EditionEnum.HasValue)
                {
                    matches &= spectral.Edition == clause.EditionEnum.Value;
                }
                
                if (matches)
                    return true;
            }
            
            return false;
        }
        
        private static bool CheckPackSpectralsSingle(ref MotelySingleSearchContext ctx, int ante, MotelyJsonSpectralFilterClause clause)
        {
            var packStream = ctx.CreateBoosterPackStream(ante);
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
            
            // Determine max pack slot to check
            int maxPackSlot = clause.PackSlotBitmask == 0 ? (ante == 1 ? 4 : 6) : 
                (64 - System.Numerics.BitOperations.LeadingZeroCount(clause.PackSlotBitmask));
            
            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Check if this pack slot should be evaluated
                bool shouldCheckThisSlot = true;
                if (clause.PackSlotBitmask != 0)
                {
                    ulong packSlotBit = 1UL << packSlot;
                    shouldCheckThisSlot = (clause.PackSlotBitmask & packSlotBit) != 0;
                }
                
                // Check if it's a Spectral pack
                bool isSpectralPack = pack.GetPackType() == MotelyBoosterPackType.Spectral;
                
                // ALWAYS consume spectral stream if it's a spectral pack to maintain sync
                if (isSpectralPack)
                {
                    // Get the actual pack size for this individual seed
                    var packSize = pack.GetPackSize();
                    
                    // Always consume max size (5) to maintain consistency with vectorized path
                    var contents = ctx.GetNextSpectralPackContents(ref spectralStream, MotelyBoosterPackSize.Mega);
                    
                    // Only evaluate if we should check this slot
                    if (!shouldCheckThisSlot) continue;
                    
                    // Check requireMega if specified in sources
                    if (clause.Sources?.RequireMega == true && packSize != MotelyBoosterPackSize.Mega)
                        continue; // Skip non-Mega packs if Mega is required
                    
                    int actualPackSize = packSize switch
                {
                    MotelyBoosterPackSize.Normal => 2,
                    MotelyBoosterPackSize.Jumbo => 3,
                    MotelyBoosterPackSize.Mega => 5,
                    _ => 2
                };
                
                // Check each card in the pack (only up to actual size)
                for (int cardIndex = 0; cardIndex < actualPackSize; cardIndex++)
                {
                    var card = contents[cardIndex];
                    
                    if (card.TypeCategory != MotelyItemTypeCategory.SpectralCard)
                        continue;
                    
                    bool matches = true;
                    
                    // Check type
                    if (clause.SpectralTypes?.Count > 0)
                    {
                        bool typeMatch = false;
                        foreach (var spectralType in clause.SpectralTypes)
                        {
                            if (card.Type == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)spectralType))
                            {
                                typeMatch = true;
                                break;
                            }
                        }
                        matches &= typeMatch;
                    }
                    else if (clause.SpectralType.HasValue)
                    {
                        matches &= card.Type == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)clause.SpectralType.Value);
                    }
                    
                    // Check edition
                    if (clause.EditionEnum.HasValue)
                    {
                        matches &= card.Edition == clause.EditionEnum.Value;
                    }
                    
                    if (matches)
                        return true;
                }
                } // Close the if (isSpectralPack) block
            }
            
            return false;
        }
    }
}