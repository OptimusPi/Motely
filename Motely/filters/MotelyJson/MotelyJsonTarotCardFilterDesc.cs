using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on tarot card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonTarotCardFilterDesc(List<MotelyJsonTarotFilterClause> tarotClauses)
    : IMotelySeedFilterDesc<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>
{
    private readonly List<MotelyJsonTarotFilterClause> _tarotClauses = tarotClauses;

    public MotelyJsonTarotCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        if (_tarotClauses == null || _tarotClauses.Count == 0)
            return new MotelyJsonTarotCardFilter(null, 0, 0);
        
        int minAnte = _tarotClauses.Min(c => c.AnteBitmask == 0 ? 1 : BitOperations.TrailingZeroCount(c.AnteBitmask) + 1);
        int maxAnte = _tarotClauses.Max(c => c.AnteBitmask == 0 ? 8 : 64 - BitOperations.LeadingZeroCount(c.AnteBitmask));
        
        return new MotelyJsonTarotCardFilter(_tarotClauses, minAnte, maxAnte);
    }
    
    private int GetMaxShopSlotsNeeded()
    {
        if (_tarotClauses == null || _tarotClauses.Count == 0)
            return 0;
            
        int maxSlot = 0;
        foreach (var clause in _tarotClauses)
        {
            if (clause.ShopSlotBitmask == ~0UL)
                return 10; // Default max if all slots enabled
                
            for (int i = 63; i >= 0; i--)
            {
                if ((clause.ShopSlotBitmask & (1UL << i)) != 0)
                {
                    maxSlot = Math.Max(maxSlot, i + 1); // Convert back to 1-based
                    break;
                }
            }
        }
        return maxSlot;
    }

    public struct MotelyJsonTarotCardFilter(List<MotelyJsonTarotFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonTarotFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // Quick vectorized check for potential tarot cards
            VectorMask hasPotential = VectorMask.NoBitsSet;
            
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                // Check shop slots for any tarot cards
                var shopStream = ctx.CreateShopItemStream(ante);
                // Use proper shop slot limits based on clause configuration
                int maxShopSlots = 10; // Default max shop slots for potential check
                for (int shopSlot = 0; shopSlot < maxShopSlots; shopSlot++)
                {
                    var shopItem = ctx.GetNextShopItem(ref shopStream);
                    var shopPotential = VectorEnum256.Equals(shopItem.TypeCategory, MotelyItemTypeCategory.TarotCard);
                    hasPotential |= shopPotential;
                }
                
                // Check arcana packs for any tarot cards
                var packStream = ctx.CreateBoosterPackStream(ante);
                var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                int totalPacks = ante == 1 ? 4 : 6;
                for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    VectorMask isArcanaPack = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Arcana);
                    
                    if (isArcanaPack.IsPartiallyTrue())
                    {
                        var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize()[0]);
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var packPotential = VectorEnum256.Equals(contents[j].TypeCategory, MotelyItemTypeCategory.TarotCard);
                            hasPotential |= (packPotential & isArcanaPack);
                        }
                    }
                }
            }
            
            // Early exit if no potential matches
            if (hasPotential.IsAllFalse())
                return VectorMask.NoBitsSet;
            
            // Copy struct fields to local variables for lambda
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            int maxShopSlotsNeeded = 10; // Default max shop slots
            
            // Now do full individual processing for seeds with potential
            return ctx.SearchIndividualSeeds(hasPotential, (ref MotelySingleSearchContext singleCtx) =>
            {
                VectorMask[] clauseMasks = new VectorMask[clauses.Count];
                for (int i = 0; i < clauseMasks.Length; i++) clauseMasks[i] = VectorMask.NoBitsSet;
                
                // ANTE LOOP FIRST - using pre-calculated range
                for (int ante = minAnte; ante <= maxAnte; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    // Create streams ONCE per ante for performance and PRNG correctness
                    var packStream = singleCtx.CreateBoosterPackStream(ante);
                    var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                    var shopStream = singleCtx.CreateShopItemStream(ante);
                    
                    // Process packs - MUST iterate ALL packs to maintain PRNG state
                    int totalPacks = ante == 1 ? 4 : 6;
                    for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                    {
                        var pack = singleCtx.GetNextBoosterPack(ref packStream);
                        
                        ulong packSlotBit = 1UL << packSlot;
                        
                        // Only process if it's an Arcana pack
                        if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                        {
                            var contents = singleCtx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                            
                            // Check each clause to see if it wants this pack slot
                            for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                            {
                                var clause = clauses[clauseIndex];
                                // Check ante bitmask
                                if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                                
                                // Check pack slot bitmask
                                if ((clause.PackSlotBitmask & packSlotBit) == 0) continue;
                                
                                // Check contents
                                for (int j = 0; j < contents.Length; j++)
                                {
                                    if (DebugLogger.IsEnabled)
                                    {
                                        DebugLogger.Log($"Pack slot {packSlot}: Item {j} = {contents[j].Type} (Category: {contents[j].TypeCategory})");
                                        if (clause.TarotType.HasValue)
                                        {
                                            DebugLogger.Log($"  Looking for: {(MotelyItemType)clause.TarotType.Value}");
                                        }
                                    }
                                    
                                    bool typeMatches = false;
                                    
                                    // Check for multi-value OR match
                                    if (clause.TarotTypes != null && clause.TarotTypes.Count > 0)
                                    {
                                        foreach (var tarotType in clause.TarotTypes)
                                        {
                                            if (contents[j].Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType))
                                            {
                                                typeMatches = true;
                                                break;
                                            }
                                        }
                                    }
                                    // Fallback to single-value match
                                    else if (clause.TarotType.HasValue)
                                    {
                                        typeMatches = contents[j].Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
                                    }
                                    // Wildcard match
                                    else
                                    {
                                        typeMatches = contents[j].TypeCategory == MotelyItemTypeCategory.TarotCard;
                                    }
                                    
                                    if (typeMatches)
                                    {
                                        if (DebugLogger.IsEnabled)
                                        {
                                            DebugLogger.Log($"  MATCH FOUND! {contents[j].Type}");
                                        }
                                        clauseMasks[clauseIndex] = VectorMask.AllBitsSet;
                                    }
                                }
                            }
                        }
                    }
                    
                    // Process shop slots - MUST iterate ALL slots to maintain PRNG state
                    int maxShopSlots = maxShopSlotsNeeded;
                    
                    for (int shopSlot = 0; shopSlot < maxShopSlots; shopSlot++)
                    {
                        var shopItem = singleCtx.GetNextShopItem(ref shopStream);
                        
                        ulong shopSlotBit = 1UL << shopSlot;
                        
                        // Check each clause to see if it wants this shop slot
                        for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                        {
                            var clause = clauses[clauseIndex];
                            // Check ante bitmask
                            if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                            
                            // Check shop slot bitmask
                            if ((clause.ShopSlotBitmask & shopSlotBit) == 0) continue;
                            
                            if (DebugLogger.IsEnabled)
                            {
                                DebugLogger.Log($"Shop slot {shopSlot}: Item = {shopItem.Type} (Category: {shopItem.TypeCategory})");
                                if (clause.TarotType.HasValue)
                                {
                                    DebugLogger.Log($"  Looking for: {(MotelyItemType)clause.TarotType.Value}");
                                }
                            }
                            
                            bool typeMatches = false;
                            
                            // Check for multi-value OR match
                            if (clause.TarotTypes != null && clause.TarotTypes.Count > 0)
                            {
                                foreach (var tarotType in clause.TarotTypes)
                                {
                                    if (shopItem.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType))
                                    {
                                        typeMatches = true;
                                        break;
                                    }
                                }
                            }
                            // Fallback to single-value match
                            else if (clause.TarotType.HasValue)
                            {
                                typeMatches = shopItem.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
                            }
                            // Wildcard match
                            else
                            {
                                typeMatches = shopItem.TypeCategory == MotelyItemTypeCategory.TarotCard;
                            }
                            
                            if (typeMatches)
                            {
                                if (DebugLogger.IsEnabled)
                                {
                                    DebugLogger.Log($"  SHOP MATCH FOUND! {shopItem.Type}");
                                }
                                clauseMasks[clauseIndex] = VectorMask.AllBitsSet;
                            }
                        }
                    }
                }
                
                // AND all criteria together - return true if all clauses satisfied
                for (int i = 0; i < clauseMasks.Length; i++)
                {
                    if (clauseMasks[i].IsAllFalse()) return false;
                }
                
                return true;
            });
        }
    }
}