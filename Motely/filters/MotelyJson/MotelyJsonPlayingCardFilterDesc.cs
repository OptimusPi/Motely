using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on playing card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonPlayingCardFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> playingCardClauses)
    : IMotelySeedFilterDesc<MotelyJsonPlayingCardFilterDesc.MotelyJsonPlayingCardFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _playingCardClauses = playingCardClauses;

    public MotelyJsonPlayingCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _playingCardClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheBoosterPackStream(ante);
                }
            }
        }
        
        // PRE-CALCULATE ANTE RANGE
        int minAnte = int.MaxValue, maxAnte = int.MinValue;
        foreach (var clause in _playingCardClauses)
        {
            foreach (var ante in clause.EffectiveAntes)
            {
                if (ante < minAnte) minAnte = ante;
                if (ante > maxAnte) maxAnte = ante;
            }
        }
        
        return new MotelyJsonPlayingCardFilter(_playingCardClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonPlayingCardFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // Copy struct members to locals to avoid CS1673
            var clauses = _clauses;
            
            // Fall back to single-seed search since vectorized playing card implementation is not ready
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                // Check all clauses
                foreach (var clause in clauses)
                {
                    bool clauseMatched = false;
                    
                    foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                    {
                        // DYNAMIC: Set generatedFirstPack based on default pack slots  
                        var packStream = singleCtx.CreateBoosterPackStream(ante, ante != 1, false);
                        for (int i = 0; i < 2 + ante; i++)
                        {
                            var pack = singleCtx.GetNextBoosterPack(ref packStream);
                            
                            if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                            {
                                var standardStream = singleCtx.CreateStandardPackCardStream(ante);
                                var contents = singleCtx.GetNextStandardPackContents(ref standardStream, pack.GetPackSize());
                                
                                for (int k = 0; k < contents.Length; k++)
                                {
                                    var card = contents[k];
                                    if (card.TypeCategory != MotelyItemTypeCategory.PlayingCard)
                                        continue;
                                    
                                    var playingCard = (MotelyPlayingCard)card.Type;
                                    
                                    // Check suit if specified
                                    if (clause.SuitEnum.HasValue && playingCard.GetSuit() != clause.SuitEnum.Value)
                                        continue;
                                    
                                    // Check rank if specified  
                                    if (clause.RankEnum.HasValue && playingCard.GetRank() != clause.RankEnum.Value)
                                        continue;
                                    
                                    bool editionMatches = !clause.EditionEnum.HasValue || card.Edition == clause.EditionEnum.Value;
                                    bool sealMatches = !clause.SealEnum.HasValue || card.Seal == clause.SealEnum.Value;
                                    
                                    if (editionMatches && sealMatches)
                                    {
                                        clauseMatched = true;
                                        break;
                                    }
                                }
                            }
                            
                            if (clauseMatched) break;
                        }
                        
                        if (clauseMatched) break;
                    }
                    
                    // If any clause didn't match, the seed doesn't match
                    if (!clauseMatched) return false;
                }
                
                // All clauses matched
                return true;
            });
        }
    }
}