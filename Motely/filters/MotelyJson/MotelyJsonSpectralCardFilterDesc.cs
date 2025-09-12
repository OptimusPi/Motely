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

                    // TODO: Pack slot checking can be added later if needed

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
            
            return finalResult;
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
    }
}