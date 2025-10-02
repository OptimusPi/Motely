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
                    
                    // Skip ante if not wanted
                    if (!clause.WantedAntes[ante])
                        continue;

                    VectorMask clauseResult = VectorMask.NoBitsSet;

                    // Check shops if any shop slots wanted
                    if (HasShopSlots(clause.WantedShopSlots))
                    {
                        // Use the self-contained shop planet stream - NO SYNCHRONIZATION ISSUES!
                        var shopPlanetStream = ctx.CreateShopPlanetStream(ante);
                        clauseResult |= CheckShopPlanetVectorized(clause, ctx, ref shopPlanetStream);
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
                        
                        var genericClause = ConvertToGeneric(clause);
                        if (MotelyJsonScoring.CountPlanetOccurrences(ref singleCtx, genericClause, ante, earlyExit: true) > 0)
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
        private static int CalculateMaxShopSlotsNeeded(List<MotelyJsonPlanetFilterClause> clauses)
        {
            int maxSlotNeeded = 0;
            foreach (var clause in clauses)
            {
                if (HasShopSlots(clause.WantedShopSlots))
                {
                    for (int i = clause.WantedShopSlots.Length - 1; i >= 0; i--)
                    {
                        if (clause.WantedShopSlots[i])
                        {
                            maxSlotNeeded = Math.Max(maxSlotNeeded, i + 1);
                            break;
                        }
                    }
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
        
        private static MotelyJsonConfig.MotleyJsonFilterClause ConvertToGeneric(MotelyJsonPlanetFilterClause clause)
        {
            var shopSlots = new List<int>();
            for (int i = 0; i < clause.WantedShopSlots.Length; i++)
                if (clause.WantedShopSlots[i]) shopSlots.Add(i);
                
            var packSlots = new List<int>();
            for (int i = 0; i < clause.WantedPackSlots.Length; i++)
                if (clause.WantedPackSlots[i]) packSlots.Add(i);
            
            return new MotelyJsonConfig.MotleyJsonFilterClause
            {
                Type = "PlanetCard",
                Value = clause.PlanetType?.ToString(),
                PlanetEnum = clause.PlanetType,
                Sources = new MotelyJsonConfig.SourcesConfig
                {
                    ShopSlots = shopSlots.ToArray(),
                    PackSlots = packSlots.ToArray()
                }
            };
        }
    }
}
