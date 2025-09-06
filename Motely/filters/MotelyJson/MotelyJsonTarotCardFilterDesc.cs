using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on tarot card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonTarotCardFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> tarotClauses)
    : IMotelySeedFilterDesc<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _tarotClauses = tarotClauses;

    public readonly string Name => "JSON Tarot Card Filter";
    public readonly string Description => "Vectorized tarot card filtering from Arcana packs";

    public MotelyJsonTarotCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _tarotClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheBoosterPackStream(ante);
                }
            }
        }
        
        return new MotelyJsonTarotCardFilter(_tarotClauses);
    }

    public struct MotelyJsonTarotCardFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            var resultMask = VectorMask.AllBitsSet;
            
            foreach (var clause in _clauses)
            {
                var clauseMask = VectorMask.NoBitsSet;
                
                foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                {
                    // Use individual seed checking for tarot cards since pack mechanics are complex
                    return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
                    {
                        // DYNAMIC: Set generatedFirstPack based on default pack slots
                        var packStream = singleCtx.CreateBoosterPackStream(ante, ante != 1, false);
                        for (int i = 0; i < 2 + ante; i++)
                        {
                            var pack = singleCtx.GetNextBoosterPack(ref packStream);
                            
                            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                            {
                                var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante, true);
                                var contents = singleCtx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                                
                                for (int k = 0; k < contents.Length; k++)
                                {
                                    var card = contents[k];
                                    bool typeMatches = clause.TarotEnum.HasValue
                                        ? card.Type == (MotelyItemType)clause.TarotEnum.Value
                                        : card.TypeCategory == MotelyItemTypeCategory.TarotCard;
                                    
                                    bool editionMatches = !clause.EditionEnum.HasValue || card.Edition == clause.EditionEnum.Value;
                                    
                                    if (typeMatches && editionMatches)
                                        return true;
                                }
                            }
                        }
                        return false;
                    });
                }
                
                resultMask &= clauseMask;
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            return resultMask;
        }
    }
}