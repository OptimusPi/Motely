using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on planet card criteria from JSON configuration.
/// REVERTED: Simple version that compiles - shop detection removed for now
/// </summary>
public struct MotelyJsonPlanetFilterDesc(List<MotelyJsonPlanetFilterClause> planetClauses)
    : IMotelySeedFilterDesc<MotelyJsonPlanetFilterDesc.MotelyJsonPlanetFilter>
{
    private readonly List<MotelyJsonPlanetFilterClause> _planetClauses = planetClauses;

    public MotelyJsonPlanetFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_planetClauses);
        
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }
        
        return new MotelyJsonPlanetFilter(_planetClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonPlanetFilter(List<MotelyJsonPlanetFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonPlanetFilterClause> _clauses = clauses;
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
                    
                    // Skip ante if not wanted
                    if (clause.WantedAntes.Any(x => x) && !clause.WantedAntes[ante])
                        continue;

                    VectorMask clauseResult = VectorMask.NoBitsSet;

                    // Check shops if specified
                    if (clause.ShopSlotBitmask != 0)
                    {
                        // Use the self-contained shop planet stream - NO SYNCHRONIZATION ISSUES!
                        var shopPlanetStream = ctx.CreateShopPlanetStream(ante);
                        clauseResult |= CheckShopPlanetVectorized(clause, ctx, ref shopPlanetStream);
                    }

                    // Check packs if specified  
                    if (clause.WantedPackSlots.Any(x => x))
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

            // ALWAYS verify with individual seed search to avoid SIMD bugs with pack streams
            var clausesCopy = _clauses;
            var minAnteCopy = _minAnte;
            var maxAnteCopy = _maxAnte;
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                Span<bool> clauseMatches = stackalloc bool[clausesCopy.Count];
                for (int i = 0; i < clauseMatches.Length; i++)
                    clauseMatches[i] = false;
                
                for (int ante = minAnteCopy; ante <= maxAnteCopy; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    
                    var packStream = singleCtx.CreateBoosterPackStream(ante);
                    var celestialStream = singleCtx.CreateCelestialPackPlanetStream(ante);
                    var shopStream = singleCtx.CreateShopItemStream(ante, isCached: false);
                    
                    int totalPacks = ante == 1 ? 4 : 6;
                    for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                    {
                        var pack = singleCtx.GetNextBoosterPack(ref packStream);
                        ulong packSlotBit = 1UL << packSlot;
                        
                        if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                        {
                            // ALWAYS consume celestial stream first to maintain sync
                            var contents = singleCtx.GetNextCelestialPackContents(ref celestialStream, pack.GetPackSize());
                            
                            // Then check if we should evaluate this pack
                            bool skipPack = false;
                            for (int clauseIndex = 0; clauseIndex < clausesCopy.Count; clauseIndex++)
                            {
                                var clause = clausesCopy[clauseIndex];
                                if (clause.WantedPackSlots[packSlot] && 
                                    clause.Sources?.RequireMega == true && 
                                    pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                                {
                                    skipPack = true;
                                    break;
                                }
                            }
                            if (skipPack) continue;
                            
                            for (int clauseIndex = 0; clauseIndex < clausesCopy.Count; clauseIndex++)
                            {
                                var clause = clausesCopy[clauseIndex];
                                
                                if (clause.WantedAntes.Any(x => x) && !clause.WantedAntes[ante]) 
                                    continue;
                                
                                if (!clause.WantedPackSlots[packSlot]) 
                                    continue;
                               
                               for (int j = 0; j < contents.Length; j++)
                               {
                                   bool typeMatches = CheckPlanetTypeMatch(contents[j], clause);
                                   bool editionMatches = !clause.EditionEnum.HasValue ||
                                                       contents[j].Edition == clause.EditionEnum.Value;
                                   
                                   if (typeMatches && editionMatches)
                                   {
                                       clauseMatches[clauseIndex] = true;
                                   }
                               }
                           }
                       }
                   }

                    // ✅ NEW: Add shop consumable support - fixes ALEEB Saturn in shop slot 0\
                   // TODO dont use hard coded "6" here, what the fuck?
                   for (int shopSlot = 0; shopSlot < 6; shopSlot++)
                    {
                        var shopItem = singleCtx.GetNextShopItem(ref shopStream);

                        if (shopItem.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                        {
                            ulong shopSlotBit = 1UL << shopSlot;

                            for (int clauseIndex = 0; clauseIndex < clausesCopy.Count; clauseIndex++)
                            {
                                var clause = clausesCopy[clauseIndex];

                                if (clause.WantedAntes.Any(x => x) && !clause.WantedAntes[ante])
                                    continue;

                                if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & shopSlotBit) == 0)
                                    continue;

                                bool typeMatches = CheckPlanetTypeMatch(shopItem, clause);
                                bool editionMatches = !clause.EditionEnum.HasValue ||
                                                    shopItem.Edition == clause.EditionEnum.Value;

                                if (typeMatches && editionMatches)
                                {
                                    clauseMatches[clauseIndex] = true;
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckShopPlanetVectorized(MotelyJsonPlanetFilterClause clause, MotelyVectorSearchContext ctx, 
            ref MotelyVectorPlanetStream shopPlanetStream)
        {
            VectorMask foundInShop = VectorMask.NoBitsSet;
            
            // TODO: Implement shop planet checking when stream is available
            // For now just return no matches
            return foundInShop;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPacksVectorized(MotelyJsonPlanetFilterClause clause, MotelyVectorSearchContext ctx, int ante)
        {
            VectorMask foundInPacks = VectorMask.NoBitsSet;
            
            // Create pack streams
            var packStream = ctx.CreateBoosterPackStream(ante);
            var celestialStream = ctx.CreateCelestialPackPlanetStream(ante);
            
            // Determine max pack slot to check
            bool hasSpecificSlots = clause.WantedPackSlots.Any(x => x);
            int maxPackSlot = hasSpecificSlots ? 6 : (ante == 1 ? 4 : 6);
            
            for (int packSlot = 0; packSlot < maxPackSlot; packSlot++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                // Check if this pack slot should be evaluated for scoring
                bool shouldEvaluateThisSlot = !hasSpecificSlots || clause.WantedPackSlots[packSlot];
                
                var packType = pack.GetPackType();
                
                // Check Celestial packs with vectorized method
                VectorMask isCelestialPack = VectorEnum256.Equals(packType, MotelyBoosterPackType.Celestial);
                if (isCelestialPack.IsPartiallyTrue())
                {
                    // FIXED: Always consume maximum pack size (5) to avoid stream desync
                    var contents = ctx.GetNextCelestialPackContents(ref celestialStream, MotelyBoosterPackSize.Mega);
                    
                    // Only evaluate/score if this slot should be checked
                    if (!shouldEvaluateThisSlot) continue;
                    
                    // Check each card in the pack
                    for (int cardIndex = 0; cardIndex < contents.Length; cardIndex++)
                    {
                        var card = contents[cardIndex];
                        
                        // Check if this is a planet card that matches our clause
                        VectorMask isPlanetCard = VectorEnum256.Equals(card.TypeCategory, MotelyItemTypeCategory.PlanetCard);
                        
                        if (isPlanetCard.IsPartiallyTrue())
                        {
                            VectorMask typeMatches = VectorMask.AllBitsSet;
                            if (clause.PlanetTypes?.Count > 0)
                            {
                                VectorMask anyTypeMatch = VectorMask.NoBitsSet;
                                foreach (var planetType in clause.PlanetTypes)
                                {
                                    var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)planetType);
                                    anyTypeMatch |= VectorEnum256.Equals(card.Type, targetType);
                                }
                                typeMatches = anyTypeMatch;
                            }
                            else if (clause.PlanetType.HasValue)
                            {
                                var targetPlanetType = (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.PlanetType.Value);
                                typeMatches = VectorEnum256.Equals(card.Type, targetPlanetType);
                            }
                            
                            VectorMask editionMatches = VectorMask.AllBitsSet;
                            if (clause.EditionEnum.HasValue)
                            {
                                editionMatches = VectorEnum256.Equals(card.Edition, clause.EditionEnum.Value);
                            }
                            
                            VectorMask matches = (isCelestialPack & isPlanetCard & typeMatches & editionMatches);
                            foundInPacks |= matches;
                        }
                    }
                }
            }
            
            return foundInPacks;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateMaxShopSlotsNeeded(List<MotelyJsonPlanetFilterClause> clauses)
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
        private static bool CheckPlanetTypeMatch(MotelyItem item, MotelyJsonPlanetFilterClause clause)
        {
            if (clause.PlanetTypes?.Count > 0)
            {
                foreach (var planetType in clause.PlanetTypes)
                {
                    if (item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)planetType))
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (clause.PlanetType.HasValue)
            {
                return item.Type == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.PlanetType.Value);
            }
            else
            {
                return item.TypeCategory == MotelyItemTypeCategory.PlanetCard;
            }
        }
    }
}
