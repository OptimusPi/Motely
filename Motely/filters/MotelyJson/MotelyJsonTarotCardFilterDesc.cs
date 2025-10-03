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
public partial struct MotelyJsonTarotCardFilterDesc(MotelyJsonTarotFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>
{
    private readonly MotelyJsonTarotFilterCriteria _criteria = criteria;

    public MotelyJsonTarotCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Use pre-calculated values from criteria
        int minAnte = _criteria.MinAnte;
        int maxAnte = _criteria.MaxAnte;

        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        return new MotelyJsonTarotCardFilter(_criteria.Clauses, minAnte, maxAnte, _criteria.MaxShopSlotsNeeded);
    }

    public struct MotelyJsonTarotCardFilter(List<MotelyJsonTarotFilterClause> clauses, int minAnte, int maxAnte, int maxShopSlotsNeeded) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonTarotFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;
        private readonly int _maxShopSlotsNeeded = maxShopSlotsNeeded;

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
                    
                    // Skip ante if not wanted
                    if (!clause.WantedAntes[ante])
                        continue;

                    VectorMask clauseResult = VectorMask.NoBitsSet;

                    // Check shops if specified
                    if (HasShopSlots(clause.WantedShopSlots))
                    {
                        // Use the self-contained shop tarot stream - NO SYNCHRONIZATION ISSUES!
                        var shopTarotStream = ctx.CreateShopTarotStreamNew(ante);
                        clauseResult |= CheckShopTarotVectorizedNew(clause, ctx, ref shopTarotStream);
                    }

                    // Check packs if specified  
                    if (HasPackSlots(clause.WantedPackSlots))
                    {
                        clauseResult |= CheckPacksVectorized(clause, ctx, ante);
                    }

                    // Accumulate results for this clause across all antes (OR logic)
                    clauseMasks[clauseIndex] |= clauseResult;
                }
            }

            // All clauses must be satisfied (AND logic)
            // CRITICAL FIX: If any clause found nothing (NoBitsSet), the entire filter fails!
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                // FIX: If this clause found nothing across all antes, fail immediately
                if (clauseMasks[i].IsAllFalse())
                {
                    return VectorMask.NoBitsSet;
                }
                
                resultMask &= clauseMasks[i];
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }

            // USE THE SHARED FUNCTION - same logic as scoring!
            var clauses = _clauses;
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // Check all clauses using the SAME shared function used in scoring
                foreach (var clause in clauses)
                {
                    bool matched = false;
                    for (int ante = 0; ante < clause.WantedAntes.Length; ante++)
                    {
                        if (!clause.WantedAntes[ante]) continue;
                        
                        var state = new MotelyRunState();
                        var genericClause = ConvertToGeneric(clause);
                        if (MotelyJsonScoring.TarotCardsTally(ref singleCtx, genericClause, ante, ref state, earlyExit: true) > 0)
                        {
                            matched = true;
                            break;
                        }
                    }
                    
                    if (!matched) return false;
                }
                
                return true;
            });
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopVectorized(ref MotelyVectorSearchContext ctx, int ante, MotelyJsonTarotFilterClause clause, ref MotelyVectorShopItemStream shopStream)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            // Check each shop slot based on the bitmask
            for (int slot = 0; slot < 64; slot++) // Check up to 64 slots (bitmask size)
            {
                
                // Skip if this slot isn't in the wanted slots
                if (!clause.WantedShopSlots[slot])
                    continue;
                
                // Get the shop item using the shared tarot-only stream
                var item = ctx.GetNextShopItem(ref shopStream);
                
                // Check if this slot has a tarot
                var isTarot = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
                
                // Check if any lanes have tarots (result is -1 for true, 0 for false) - ONLY CHECK VALID LANES!
                uint tarotMask = 0;
                for (int i = 0; i < 8; i++)
                    if (ctx.IsLaneValid(i) && isTarot[i] == -1) tarotMask |= (1u << i);
                
                if (tarotMask != 0) // Any lanes have tarots
                {
                    // Check if it matches our clause
                    VectorMask matches = CheckTarotMatchesClause(item, clause, ref ctx);
                    foundInShop |= matches;
                }
            }

            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopVectorizedPrecomputed(MotelyJsonTarotFilterClause clause, MotelyItemVector[] shopItems, ref MotelyVectorSearchContext ctx)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;

            if (!HasShopSlots(clause.WantedShopSlots))
            {
                // No slot restrictions - check all available slots
                for (int slot = 0; slot < shopItems.Length; slot++)
                {
                    var item = shopItems[slot];
                    DebugLogger.Log($"[TAROT VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}");
                    
                    // Check if this slot has a tarot
                    var isTarot = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
                    
                    // Check if any lanes have tarots - ONLY CHECK VALID LANES!
                    uint tarotMask = 0;
                    for (int i = 0; i < 8; i++)
                        if (ctx.IsLaneValid(i) && isTarot[i] == -1) tarotMask |= (1u << i);
                    
                    if (tarotMask != 0) // Any lanes have tarots
                    {
                        DebugLogger.Log($"[TAROT VECTORIZED] Found tarot at shop slot {slot}: {item.Type[0]}, expecting: {clause.TarotType}");
                        // Check if it matches our clause
                        VectorMask matches = CheckTarotMatchesClause(item, clause, ref ctx);
                        DebugLogger.Log($"[TAROT VECTORIZED] Matches mask={matches.Value:X}");
                        foundInShop |= matches;
                        if (!foundInShop.IsAllFalse()) break; // Found a match, can stop
                    }
                }
            }
            else
            {
                // Calculate the highest slot we need to check
                int maxSlot = 0;
                for (int i = clause.WantedShopSlots.Length - 1; i >= 0; i--)
                {
                    if (clause.WantedShopSlots[i])
                    {
                        maxSlot = i + 1;
                        break;
                    }
                }
                
                // Check only the slots we precomputed
                for (int slot = 0; slot < Math.Min(maxSlot, shopItems.Length); slot++)
                {
                    // Check if this slot is wanted
                    if (clause.WantedShopSlots[slot])
                    {
                        var item = shopItems[slot];
                        DebugLogger.Log($"[TAROT VECTORIZED] Checking shop slot {slot}: item type category={item.TypeCategory}");
                        
                        // Check if this slot has a tarot
                        var isTarot = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
                        
                        // Check if any lanes have tarots - ONLY CHECK VALID LANES!
                        uint tarotMask = 0;
                        for (int i = 0; i < 8; i++)
                            if (ctx.IsLaneValid(i) && isTarot[i] == -1) tarotMask |= (1u << i);
                        
                        if (tarotMask != 0) // Any lanes have tarots
                        {
                            DebugLogger.Log($"[TAROT VECTORIZED] Found tarot at shop slot {slot}: {item.Type[0]}, expecting: {clause.TarotType}");
                            // Check if it matches our clause
                            VectorMask matches = CheckTarotMatchesClause(item, clause, ref ctx);
                            DebugLogger.Log($"[TAROT VECTORIZED] Matches mask={matches.Value:X}");
                            foundInShop |= matches;
                        }
                    }
                }
            }

            return foundInShop;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckTarotMatchesClause(MotelyItemVector item, MotelyJsonTarotFilterClause clause, ref MotelyVectorSearchContext ctx)
        {
            VectorMask matches = VectorMask.AllBitsSet;

            // Check type if specified
            if (clause.TarotTypes != null && clause.TarotTypes.Count > 0)
            {
                VectorMask typeMatch = VectorMask.NoBitsSet;
                foreach (var tarotType in clause.TarotTypes)
                {
                    var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType);
                    typeMatch |= VectorEnum256.Equals(item.Type, targetType);
                }
                matches &= typeMatch;
            }
            else if (clause.TarotType.HasValue)
            {
                var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
                matches &= VectorEnum256.Equals(item.Type, targetType);
            }
            else
            {
                // Match any tarot
                matches &= VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard);
            }

            // Check edition if specified
            if (clause.EditionEnum.HasValue)
            {
                matches &= VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
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

            // Determine max pack slot to check - use config if provided
            bool hasSpecificSlots = HasPackSlots(clause.WantedPackSlots);
            int maxPackSlot = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : (ante == 1 ? 4 : 6);
            
            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Check if this pack slot should be evaluated for scoring
                bool shouldEvaluateThisSlot = !hasSpecificSlots || clause.WantedPackSlots[packSlot];
                
                var packType = pack.GetPackType();
                
                // Check Arcana packs with vectorized method
                VectorMask isArcanaPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Arcana);
                if (isArcanaPack.IsPartiallyTrue())
                {
                    // FIXED: Always consume maximum pack size (5) to avoid stream desync
                    var contents = ctx.GetNextArcanaPackContents(ref arcanaStream, MotelyBoosterPackSize.Mega);
                    
                    // Only evaluate/score if this slot should be checked
                    if (!shouldEvaluateThisSlot) continue;
                    
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
        private static bool HasShopSlots(bool[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]) return true;
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasPackSlots(bool[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]) return true;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask CheckShopTarotVectorizedNew(MotelyJsonTarotFilterClause clause, MotelyVectorSearchContext ctx, 
            ref MotelyVectorShopTarotStream shopTarotStream)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;
            
            // Calculate max slot we need to check
            int maxSlot;
            if (!HasShopSlots(clause.WantedShopSlots))
            {
                maxSlot = _maxShopSlotsNeeded;
            }
            else
            {
                maxSlot = 0;
                for (int i = clause.WantedShopSlots.Length - 1; i >= 0; i--)
                {
                    if (clause.WantedShopSlots[i])
                    {
                        maxSlot = i + 1;
                        break;
                    }
                }
            }
            
            // Check each shop slot using the self-contained stream
            for (int slot = 0; slot < maxSlot; slot++)
            {
                
                // Get tarot for this slot using self-contained stream - handles slot types internally!
                var tarotItem = shopTarotStream.GetNext(ref ctx);
                
                // Skip if this slot isn't wanted (no slots = check all slots)
                if (HasShopSlots(clause.WantedShopSlots) && !clause.WantedShopSlots[slot])
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
                    VectorMask matches = CheckTarotMatchesClause(tarotItem, clause, ref ctx);
                    foundInShop |= (isActualTarot & matches);
                }
            }

            return foundInShop;
        }

        private static bool CheckTarotIndividualStatic(ref MotelySingleSearchContext ctx, List<MotelyJsonTarotFilterClause> clauses)
        {
            // Check each clause - all must be satisfied
            foreach (var clause in clauses)
            {
                bool clauseSatisfied = false;
                
                // Check all antes in the clause's bitmask
                for (int ante = 1; ante <= 64; ante++)
                {
                    if (!clause.WantedAntes[ante])
                        continue;
                        
                    // Check shops if specified
                    if (HasShopSlots(clause.WantedShopSlots))
                    {
                        var shopTarotStream = ctx.CreateShopTarotStream(ante);
                        if (CheckShopTarotsSingle(ref ctx, ref shopTarotStream, clause))
                        {
                            clauseSatisfied = true;
                            break;
                        }
                    }
                    
                    // Check packs if specified
                    if (HasPackSlots(clause.WantedPackSlots))
                    {
                        if (CheckPackTarotsSingle(ref ctx, ante, clause))
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
        
        private static bool CheckShopTarotsSingle(ref MotelySingleSearchContext ctx, ref MotelySingleTarotStream stream, MotelyJsonTarotFilterClause clause)
        {
            // Calculate max slot to check
            int maxSlot;
            if (!HasShopSlots(clause.WantedShopSlots))
            {
                maxSlot = 16;
            }
            else
            {
                maxSlot = 0;
                for (int i = clause.WantedShopSlots.Length - 1; i >= 0; i--)
                {
                    if (clause.WantedShopSlots[i])
                    {
                        maxSlot = i + 1;
                        break;
                    }
                }
            }
            
            for (int slot = 0; slot < maxSlot; slot++)
            {
                
                // Skip if this slot isn't wanted (no slots = check all)
                if (HasShopSlots(clause.WantedShopSlots) && !clause.WantedShopSlots[slot])
                    continue;
                
                var tarot = ctx.GetNextTarot(ref stream);
                
                // Skip if not a tarot slot
                if (tarot.Type == MotelyItemType.TarotExcludedByStream)
                    continue;
                
                // Check if it matches our criteria
                bool matches = true;
                
                // Check type
                if (clause.TarotTypes?.Count > 0)
                {
                    bool typeMatch = false;
                    foreach (var tarotType in clause.TarotTypes)
                    {
                        if (tarot.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType))
                        {
                            typeMatch = true;
                            break;
                        }
                    }
                    matches &= typeMatch;
                }
                else if (clause.TarotType.HasValue)
                {
                    matches &= tarot.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
                }
                
                // Check edition
                if (clause.EditionEnum.HasValue)
                {
                    matches &= tarot.Edition == clause.EditionEnum.Value;
                }
                
                if (matches)
                    return true;
            }
            
            return false;
        }
        
        private static bool CheckPackTarotsSingle(ref MotelySingleSearchContext ctx, int ante, MotelyJsonTarotFilterClause clause)
        {
            var packStream = ctx.CreateBoosterPackStream(ante);
            var arcanaStream = ctx.CreateArcanaPackTarotStream(ante);

            // Determine max pack slot to check - use config if provided
            bool hasSpecificSlots = HasPackSlots(clause.WantedPackSlots);
            int maxPackSlot = clause.MaxPackSlot.HasValue
                ? clause.MaxPackSlot.Value + 1
                : (ante == 1 ? 4 : 6);
            
            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Check if this pack slot should be evaluated for scoring
                bool shouldEvaluateThisSlot = !hasSpecificSlots || clause.WantedPackSlots[packSlot];
                
                // Check if it's an Arcana pack
                bool isArcanaPack = pack.GetPackType() == MotelyBoosterPackType.Arcana;
                
                // ALWAYS consume arcana stream if it's an arcana pack to maintain sync
                if (isArcanaPack)
                {
                    // Get the actual pack size for this individual seed
                    var packSize = pack.GetPackSize();
                    var contents = ctx.GetNextArcanaPackContents(ref arcanaStream, packSize);
                    
                    // Only evaluate/score if this slot should be checked
                    if (!shouldEvaluateThisSlot) continue;
                    
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
                
                // Check each card in the pack
                for (int cardIndex = 0; cardIndex < actualPackSize; cardIndex++)
                {
                    var card = contents[cardIndex];
                    
                    if (card.TypeCategory != MotelyItemTypeCategory.TarotCard)
                        continue;
                    
                    bool matches = true;
                    
                    // Check type
                    if (clause.TarotTypes?.Count > 0)
                    {
                        bool typeMatch = false;
                        foreach (var tarotType in clause.TarotTypes)
                        {
                            if (card.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarotType))
                            {
                                typeMatch = true;
                                break;
                            }
                        }
                        matches &= typeMatch;
                    }
                    else if (clause.TarotType.HasValue)
                    {
                        matches &= card.Type == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.TarotType.Value);
                    }
                    
                    // Check edition
                    if (clause.EditionEnum.HasValue)
                    {
                        matches &= card.Edition == clause.EditionEnum.Value;
                    }
                    
                    if (matches)
                        return true;
                }
                } // Close the if (isArcanaPack) block
            }
            
            return false;
        }
        
        private static MotelyJsonConfig.MotleyJsonFilterClause ConvertToGeneric(MotelyJsonTarotFilterClause clause)
        {
            var shopSlots = new List<int>();
            for (int i = 0; i < clause.WantedShopSlots.Length; i++)
                if (clause.WantedShopSlots[i]) shopSlots.Add(i);
                
            var packSlots = new List<int>();
            for (int i = 0; i < clause.WantedPackSlots.Length; i++)
                if (clause.WantedPackSlots[i]) packSlots.Add(i);
            
            return new MotelyJsonConfig.MotleyJsonFilterClause
            {
                Type = "TarotCard",
                Value = clause.TarotType?.ToString(),
                TarotEnum = clause.TarotType,
                Sources = new MotelyJsonConfig.SourcesConfig
                {
                    ShopSlots = shopSlots.ToArray(),
                    PackSlots = packSlots.ToArray()
                }
            };
        }
    }
}
