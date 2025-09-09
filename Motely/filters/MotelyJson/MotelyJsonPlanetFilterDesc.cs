using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on planet card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonPlanetFilterDesc(List<MotelyJsonPlanetFilterClause> planetClauses)
    : IMotelySeedFilterDesc<MotelyJsonPlanetFilterDesc.MotelyJsonPlanetFilter>
{
    private readonly List<MotelyJsonPlanetFilterClause> _planetClauses = planetClauses;

    public MotelyJsonPlanetFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Calculate ante range from bitmasks
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_planetClauses);
        
        // Cache streams for needed antes
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
            
            // Copy struct fields to local variables for lambda
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            int maxShopSlotsNeeded = GetMaxShopSlotsNeeded();
            
            // Search all seeds directly without vectorized potential check
             return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
             {

                 
                 VectorMask[] clauseMasks = new VectorMask[clauses.Count];
                 for (int i = 0; i < clauseMasks.Length; i++)
                     clauseMasks[i] = VectorMask.NoBitsSet;
                 
                 for (int ante = minAnte; ante <= maxAnte; ante++)
                 {
                     ulong anteBit = 1UL << (ante - 1);
                     

                     
                     // Create streams ONCE per ante for performance and PRNG correctness
                     var packStream = singleCtx.CreateBoosterPackStream(ante);
                     var celestialStream = singleCtx.CreateCelestialPackPlanetStream(ante);
                     var shopStream = singleCtx.CreateShopItemStream(ante);
                     
                     // Process packs - MUST iterate ALL packs to maintain PRNG state
                      int totalPacks = ante == 1 ? 4 : 6;
                     for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                     {
                         var pack = singleCtx.GetNextBoosterPack(ref packStream);
                         

                         
                         ulong packSlotBit = 1UL << packSlot;
                         
                         // Only process if it's a Celestial pack
                          if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                         {
                             var contents = singleCtx.GetNextCelestialPackContents(ref celestialStream, pack.GetPackSize());
                             

                             
                             // Check each clause to see if it wants this pack slot
                              for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                              {
                                  var clause = clauses[clauseIndex];
                                 

                                 
                                 // Check ante bitmask
                                 if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) 
                                     continue;
                                 
                                 // Check pack slot bitmask
                                 if ((clause.PackSlotBitmask & packSlotBit) == 0) 
                                     continue;
                                
                                // Check contents
                                for (int j = 0; j < contents.Length; j++)
                                {

                                    
                                    bool typeMatches = clause.PlanetType.HasValue
                                        ? contents[j].Type == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.PlanetType.Value)
                                        : contents[j].TypeCategory == MotelyItemTypeCategory.PlanetCard;
                                    
                                    bool editionMatches = !clause.EditionEnum.HasValue ||
                                                        contents[j].Edition == clause.EditionEnum.Value;
                                    
                                    if (typeMatches && editionMatches)
                                    {
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
                            
                            bool typeMatches = clause.PlanetType.HasValue
                                 ? shopItem.Type == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.PlanetType.Value)
                                 : shopItem.TypeCategory == MotelyItemTypeCategory.PlanetCard;
                            
                            bool editionMatches = !clause.EditionEnum.HasValue ||
                                                shopItem.Edition == clause.EditionEnum.Value;
                            
                            if (typeMatches && editionMatches)
                            {
                                clauseMasks[clauseIndex] = VectorMask.AllBitsSet;
                            }
                         }
                    }
                }
                
                // Combine all clause results - ALL clauses must match
                bool allClausesMatched = true;
                for (int i = 0; i < clauseMasks.Length; i++)
                {
                    if (clauseMasks[i].IsAllFalse())
                    {
                        allClausesMatched = false;
                        break;
                    }
                }
                
                return allClausesMatched;
            });
        }
        
        private int GetMaxShopSlotsNeeded()
        {
            if (_clauses == null || _clauses.Count == 0)
                return 6; // Default 4/6 concept
            
            int maxSlot = 0;
            foreach (var clause in _clauses)
            {
                if (clause.ShopSlotBitmask == 0)
                    return 6; // No specific shop slots specified, use 4/6 concept
                
                // Find highest bit set in bitmask
                ulong mask = clause.ShopSlotBitmask;
                int slot = 0;
                while (mask > 0)
                {
                    if ((mask & 1) != 0)
                        maxSlot = Math.Max(maxSlot, slot);
                    mask >>= 1;
                    slot++;
                }
            }
            
            return Math.Max(6, maxSlot + 1); // At least 6 slots for 4/6 concept
        }
    }
}