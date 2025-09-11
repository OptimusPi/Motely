using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on tarot card criteria from JSON configuration.
/// REVERTED: Simple version that compiles - shop detection removed for now
/// </summary>
public partial struct MotelyJsonTarotCardFilterDesc(List<MotelyJsonTarotFilterClause> tarotClauses)
    : IMotelySeedFilterDesc<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>
{
    private readonly List<MotelyJsonTarotFilterClause> _tarotClauses = tarotClauses;

    public MotelyJsonTarotCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_tarotClauses);
        
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }
        
        return new MotelyJsonTarotCardFilter(_tarotClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonTarotCardFilter(List<MotelyJsonTarotFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonTarotFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;
        private readonly int _maxShopSlotsNeeded = CalculateMaxShopSlotsNeeded(clauses);

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

                    // Check shops if specified
                    if (clause.ShopSlotBitmask != 0)
                    {
                        // Use the self-contained shop tarot stream - NO SYNCHRONIZATION ISSUES!
                        var shopTarotStream = ctx.CreateShopTarotStreamNew(ante);
                        clauseResult |= CheckShopTarotVectorizedNew(clause, ctx, ref shopTarotStream);
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

            // All clauses must be satisfied (AND logic)
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                resultMask &= clauseMasks[i];
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }

            return resultMask;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopVectorized(ref MotelyVectorSearchContext ctx, int ante, MotelyJsonTarotFilterClause clause, ref MotelyVectorShopItemStream shopStream)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            // Check each shop slot based on the bitmask
            for (int slot = 0; slot < 64; slot++) // Check up to 64 slots (bitmask size)
            {
                ulong slotBit = 1UL << slot;
                
                // Skip if this slot isn't in the bitmask
                if ((clause.ShopSlotBitmask & slotBit) == 0)
                    continue;
                
                // Get the shop item using the shared tarot-only stream
                var item = ctx.GetNextShopItem(ref shopStream);
                
                // Check if this slot has a tarot
                var isTarot = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
                
                // Check if any lanes have tarots (result is -1 for true, 0 for false)
                uint tarotMask = 0;
                for (int i = 0; i < 8; i++)
                    if (isTarot[i] == -1) tarotMask |= (1u << i);
                
                if (tarotMask != 0) // Any lanes have tarots
                {
                    // Check if it matches our clause
                    VectorMask matches = CheckTarotMatchesClause(item, clause);
                    foundInShop |= matches;
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopVectorizedPrecomputed(MotelyJsonTarotFilterClause clause, MotelyItemVector[] shopItems)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            if (clause.ShopSlotBitmask == 0)
            {
                // No slot restrictions - check all available slots
                for (int slot = 0; slot < shopItems.Length; slot++)
                {
                    var item = shopItems[slot];
                    DebugLogger.Log($"[TAROT VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}");
                    
                    // Check if this slot has a tarot
                    var isTarot = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
                    
                    // Check if any lanes have tarots
                    uint tarotMask = 0;
                    for (int i = 0; i < 8; i++)
                        if (isTarot[i] == -1) tarotMask |= (1u << i);
                    
                    if (tarotMask != 0) // Any lanes have tarots
                    {
                        DebugLogger.Log($"[TAROT VECTORIZED] Found tarot at shop slot {slot}: {item.Type[0]}, expecting: {clause.TarotType}");
                        // Check if it matches our clause
                        VectorMask matches = CheckTarotMatchesClause(item, clause);
                        DebugLogger.Log($"[TAROT VECTORIZED] Matches mask={matches.Value:X}");
                        foundInShop |= matches;
                        if (!foundInShop.IsAllFalse()) break; // Found a match, can stop
                    }
                }
            }
            else
            {
                // Calculate the highest slot we need to check
                int maxSlot = 64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask);
                
                // Check only the slots we precomputed
                for (int slot = 0; slot < Math.Min(maxSlot, shopItems.Length); slot++)
                {
                    ulong slotBit = 1UL << slot;
                    
                    // Check if this slot is in the bitmask
                    if ((clause.ShopSlotBitmask & slotBit) != 0)
                    {
                        var item = shopItems[slot];
                        DebugLogger.Log($"[TAROT VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}");
                        
                        // Check if this slot has a tarot
                        var isTarot = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
                        
                        // Check if any lanes have tarots
                        uint tarotMask = 0;
                        for (int i = 0; i < 8; i++)
                            if (isTarot[i] == -1) tarotMask |= (1u << i);
                        
                        if (tarotMask != 0) // Any lanes have tarots
                        {
                            DebugLogger.Log($"[TAROT VECTORIZED] Found tarot at shop slot {slot}: {item.Type[0]}, expecting: {clause.TarotType}");
                            // Check if it matches our clause
                            VectorMask matches = CheckTarotMatchesClause(item, clause);
                            DebugLogger.Log($"[TAROT VECTORIZED] Matches mask={matches.Value:X}");
                            foundInShop |= matches;
                        }
                    }
                }
            }

            return foundInShop;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckTarotMatchesClause(MotelyItemVector item, MotelyJsonTarotFilterClause clause)
        {
            VectorMask matches = VectorMask.AllBitsSet;

            // Check type if specified
            if (clause.TarotTypes != null && clause.TarotTypes.Count > 0)
            {
                VectorMask typeMatch = VectorMask.NoBitsSet;
                foreach (var tarotType in clause.TarotTypes)
                {
                    var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType);
                    var eqResult = VectorEnum256.Equals(item.Type, targetType);
                    uint mask = 0;
                    for (int i = 0; i < 8; i++)
                        if (eqResult[i] == -1) mask |= (1u << i);
                    typeMatch |= new VectorMask(mask);
                }
                matches &= typeMatch;
            }
            else if (clause.TarotType.HasValue)
            {
                var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
                var eqResult = VectorEnum256.Equals(item.Type, targetType);
                uint mask = 0;
                for (int i = 0; i < 8; i++)
                    if (eqResult[i] == -1) mask |= (1u << i);
                matches &= new VectorMask(mask);
            }
            else
            {
                // Match any tarot
                var eqResult = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
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
        private static bool CheckTarotTypeMatch(MotelyItem item, MotelyJsonTarotFilterClause clause)
        {
            if (clause.TarotTypes?.Count > 0)
            {
                foreach (var tarotType in clause.TarotTypes)
                {
                    if (item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType))
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (clause.TarotType.HasValue)
            {
                return item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
            }
            else
            {
                return item.TypeCategory == MotelyItemTypeCategory.TarotCard;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPacksVectorized(MotelyJsonTarotFilterClause clause, MotelyVectorSearchContext ctx, int ante)
        {
            VectorMask foundInPacks = VectorMask.NoBitsSet;
            
            // Create pack streams
            var packStream = ctx.CreateBoosterPackStream(ante);
            var arcanaStream = ctx.CreateArcanaPackTarotStream(ante);
            
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
                
                // Check Arcana packs with vectorized method
                VectorMask isArcanaPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Arcana);
                if (isArcanaPack.IsPartiallyTrue())
                {
                    // GetPackSize returns a vector, need to get the first value
                    var packSize = pack.GetPackSize()[0];
                    var contents = ctx.GetNextArcanaPackContents(ref arcanaStream, packSize);
                    
                    // Check each card in the pack
                    for (int cardIndex = 0; cardIndex < contents.Length; cardIndex++)
                    {
                        var card = contents[cardIndex];
                        
                        // Check if this is a tarot card that matches our clause
                        VectorMask isTarotCard = VectorEnum256.Equals(card.TypeCategory, MotelyItemTypeCategory.TarotCard);
                        
                        if (isTarotCard.IsPartiallyTrue())
                        {
                            VectorMask typeMatches = VectorMask.AllBitsSet;
                            if (clause.TarotType.HasValue)
                            {
                                var targetTarotType = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
                                typeMatches = VectorEnum256.Equals(card.Type, targetTarotType);
                            }
                            
                            VectorMask editionMatches = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatches = VectorEnum256.Equals(card.Edition, clause.EditionEnum.Value);
                            }
                            
                            VectorMask matches = (isArcanaPack & isTarotCard & typeMatches & editionMatches);
                            foundInPacks |= matches;
                        }
                    }
                }
            }
            
            return foundInPacks;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateMaxShopSlotsNeeded(List<MotelyJsonTarotFilterClause> clauses)
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
                    // If no slot restrictions, check all available shop slots (16 is generous max)
                    maxSlotNeeded = Math.Max(maxSlotNeeded, 16);
                }
            }
            return maxSlotNeeded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopTarotVectorizedNew(MotelyJsonTarotFilterClause clause, MotelyVectorSearchContext ctx, 
            ref MotelyVectorShopTarotStream shopTarotStream)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;
            
            // Calculate max slot we need to check
            int maxSlot = clause.ShopSlotBitmask == 0 ? _maxShopSlotsNeeded : 
                (64 - System.Numerics.BitOperations.LeadingZeroCount(clause.ShopSlotBitmask));
            
            // Check each shop slot using the self-contained stream
            for (int slot = 0; slot < maxSlot; slot++)
            {
                ulong slotBit = 1UL << slot;
                
                // Get tarot for this slot using self-contained stream - handles slot types internally!
                var tarotItem = shopTarotStream.GetNext(ref ctx);
                
                // Skip if this slot isn't in the bitmask (0 = check all slots)
                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & slotBit) == 0)
                    continue;
                
                // Check if item is TarotExcludedByStream (not a tarot slot)
                VectorMask isActualTarot = VectorMask.AllBitsSet;
                for (int lane = 0; lane < 8; lane++)
                {
                    if (tarotItem.Value[lane] == (int)MotelyItemType.TarotExcludedByStream)
                        isActualTarot[lane] = false;
                }
                
                if (isActualTarot.IsPartiallyTrue())
                {
                    // Check if the tarot matches our clause criteria
                    VectorMask matches = CheckTarotMatchesClause(tarotItem, clause);
                    foundInShop |= (isActualTarot & matches);
                }
            }

            return foundInShop;
        }
    }
}
