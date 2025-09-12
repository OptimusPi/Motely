using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
            VectorMask finalResult = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
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
                ulong slotBit = 1UL << slot;
                
                // Get spectral for this slot using self-contained stream - handles slot types internally!
                var spectralItem = shopSpectralStream.GetNext(ref ctx);
                
                // Skip if this slot isn't in the bitmask (0 = check all slots)
                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & slotBit) == 0)
                    continue;
                
                // Check if item is SpectralExcludedByStream (not a spectral slot)
                VectorMask isActualSpectral = VectorMask.AllBitsSet;
                for (int lane = 0; lane < 8; lane++)
                {
                    if (spectralItem.Value[lane] == (int)MotelyItemType.SpectralExcludedByStream)
                        isActualSpectral[lane] = false;
                }
                
                if (isActualSpectral.IsPartiallyTrue())
                {
                    // Check if the spectral matches our clause criteria
                    VectorMask typeMatches = VectorMask.AllBitsSet;
                    if (clause.SpectralType.HasValue)
                    {
                        var targetSpectralType = (MotelyItemType)clause.SpectralType.Value;
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
                
                // Skip if this pack slot isn't in our filter
                if (clause.PackSlotBitmask != 0)
                {
                    ulong packSlotBit = 1UL << packSlot;
                    if ((clause.PackSlotBitmask & packSlotBit) == 0) continue;
                }
                
                var packType = pack.GetPackType();
                
                // Check Spectral packs with vectorized method
                VectorMask isSpectralPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Spectral);
                if (isSpectralPack.IsPartiallyTrue())
                {
                    // FIXED: Always consume maximum pack size (5) to avoid stream desync
                    var contents = ctx.GetNextSpectralPackContents(ref spectralStream, MotelyBoosterPackSize.Mega);
                    
                    // Check each card in the pack
                    for (int cardIndex = 0; cardIndex < contents.Length; cardIndex++)
                    {
                        var card = contents[cardIndex];
                        
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
                            
                            VectorMask matches = (isSpectralPack & isSpectralCard & typeMatches & editionMatches);
                            foundInPacks |= matches;
                        }
                    }
                }
            }
            
            return foundInPacks;
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
                ulong slotBit = 1UL << slot;
                
                // Skip if this slot isn't in the bitmask (0 = check all)
                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & slotBit) == 0)
                    continue;
                
                var spectral = ctx.GetNextSpectral(ref stream);
                
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
                
                // Skip if this pack slot isn't in our filter
                if (clause.PackSlotBitmask != 0)
                {
                    ulong packSlotBit = 1UL << packSlot;
                    if ((clause.PackSlotBitmask & packSlotBit) == 0) continue;
                }
                
                // Check if it's a Spectral pack
                if (pack.GetPackType() != MotelyBoosterPackType.Spectral)
                    continue;
                
                // Check requireMega if specified in sources
                if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                    continue; // Skip non-Mega packs if Mega is required
                
                // Get the actual pack size for this individual seed
                var packSize = pack.GetPackSize();
                
                var contents = ctx.GetNextSpectralPackContents(ref spectralStream, packSize);
                
                int actualPackSize = packSize switch
                {
                    MotelyBoosterPackSize.Normal => 2,
                    MotelyBoosterPackSize.Jumbo => 3,
                    MotelyBoosterPackSize.Mega => 5,
                    _ => 2
                };
                
                // Check each card in the pack
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
            }
            
            return false;
        }
    }
}