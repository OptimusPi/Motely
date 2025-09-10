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
public struct MotelyJsonTarotCardFilterDesc(List<MotelyJsonTarotFilterClause> tarotClauses)
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
                    var arcanaStream = singleCtx.CreateArcanaPackTarotStream(ante);
                    var shopStream = singleCtx.CreateShopItemStream(ante, isCached: false);
                    
                    int totalPacks = ante == 1 ? 4 : 6;
                    for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                    {
                        var pack = singleCtx.GetNextBoosterPack(ref packStream);
                        ulong packSlotBit = 1UL << packSlot;
                        
                        if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                        {
                            var contents = singleCtx.GetNextArcanaPackContents(ref arcanaStream, pack.GetPackSize());
                            
                            for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                            {
                                var clause = clauses[clauseIndex];
                                
                                if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) 
                                    continue;
                                
                                if ((clause.PackSlotBitmask & packSlotBit) == 0) 
                                    continue;
                               
                               for (int j = 0; j < contents.Length; j++)
                               {
                                   bool typeMatches = CheckTarotTypeMatch(contents[j], clause);
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
                   
                   // ✅ NEW: Add shop consumable support
                   for (int shopSlot = 0; shopSlot < 6; shopSlot++)
                   {
                       var shopItem = singleCtx.GetNextShopItem(ref shopStream);
                       
                       if (shopItem.TypeCategory == MotelyItemTypeCategory.TarotCard)
                       {
                           ulong shopSlotBit = 1UL << shopSlot;
                           
                           for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                           {
                               var clause = clauses[clauseIndex];
                               
                               if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) 
                                   continue;
                               
                               if (clause.ShopSlotBitmask != 0 && (clause.ShopSlotBitmask & shopSlotBit) == 0) 
                                   continue;
                               
                               bool typeMatches = CheckTarotTypeMatch(shopItem, clause);
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
    }
}
