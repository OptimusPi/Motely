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

        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            
            return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
                Span<bool> clauseMatches = stackalloc bool[clauses.Count];
                for (int i = 0; i < clauseMatches.Length; i++)
                    clauseMatches[i] = false;
                
                for (int ante = minAnte; ante <= maxAnte; ante++)
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
                            var contents = singleCtx.GetNextCelestialPackContents(ref celestialStream, pack.GetPackSize());
                            
                            for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                            {
                                var clause = clauses[clauseIndex];
                                
                                if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) 
                                    continue;
                                
                                if ((clause.PackSlotBitmask & packSlotBit) == 0) 
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

                            for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                            {
                                var clause = clauses[clauseIndex];

                                if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0)
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
