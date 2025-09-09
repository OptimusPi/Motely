using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on joker criteria from JSON configuration.
/// </summary>
public struct MotelyJsonJokerFilterDesc(List<MotelyJsonJokerFilterClause> jokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>
{
    private readonly List<MotelyJsonJokerFilterClause> _jokerClauses = jokerClauses;

    public MotelyJsonJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Calculate ante range from bitmasks
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_jokerClauses);
        
        // Cache streams for needed antes
        for (int ante = minAnte; ante <= maxAnte; ante++)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }
        
        return new MotelyJsonJokerFilter(_jokerClauses, minAnte, maxAnte);
    }

    public struct MotelyJsonJokerFilter(List<MotelyJsonJokerFilterClause> clauses, int minAnte, int maxAnte) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonJokerFilterClause> _clauses = clauses;
        private readonly int _minAnte = minAnte;
        private readonly int _maxAnte = maxAnte;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            // Copy struct fields to local variables for lambda
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            
            // Process all seeds directly with efficient bitmask filtering
            return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
                VectorMask[] clauseMasks = new VectorMask[clauses.Count];
                for (int i = 0; i < clauseMasks.Length; i++) clauseMasks[i] = VectorMask.NoBitsSet;
                
                // ANTE LOOP FIRST - using pre-calculated range
                for (int ante = minAnte; ante <= maxAnte; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    
                    // CLAUSE LOOP SECOND
                    for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                    {
                        var clause = clauses[clauseIndex];
                        // Check ante bitmask
                        if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                        
                        // Check shop slots using bitmask
                        if (clause.ShopSlotBitmask != 0)
                        {
                            var shopStream = singleCtx.CreateShopItemStream(ante, isCached: false);
                            for (int i = 0; i < 64 && clause.ShopSlotBitmask != 0; i++)
                            {
                                var item = singleCtx.GetNextShopItem(ref shopStream);
                                if (((clause.ShopSlotBitmask >> i) & 1) != 0 && item.TypeCategory == MotelyItemTypeCategory.Joker)
                                {
                                    var joker = new MotelyItem(item.Value).GetJoker();
                                    bool typeMatches = false;
                                    
                                    if (!clause.IsWildcard)
                                    {
                                        if (clause.JokerTypes?.Count > 0)
                                        {
                                            // Multi-value: OR logic - match any joker in the list
                                            typeMatches = clause.JokerTypes.Contains(joker);
                                        }
                                        else
                                        {
                                            // Single value
                                            typeMatches = joker == clause.JokerType;
                                        }
                                    }
                                    else
                                    {
                                        typeMatches = CheckWildcardMatch(joker, clause.WildcardEnum);
                                    }
                                    
                                    bool editionMatches = !clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value;
                                    
                                    if (typeMatches && editionMatches)
                                    {
                                        clauseMasks[clauseIndex] = VectorMask.AllBitsSet;
                                    }
                                }
                            }
                        }
                        
                        // Check pack slots using bitmask
                        if (clause.PackSlotBitmask != 0)
                        {
                            var packStream = singleCtx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
                            var buffoonStream = singleCtx.CreateBuffoonPackJokerStream(ante);
                            for (int i = 0; i < 64 && clause.PackSlotBitmask != 0; i++)
                            {
                                var pack = singleCtx.GetNextBoosterPack(ref packStream);
                                if (((clause.PackSlotBitmask >> i) & 1) != 0 && pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                                {
                                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;
                                    
                                    var contents = singleCtx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                                    for (int j = 0; j < contents.Length; j++)
                                    {
                                        var item = contents[j];
                                        var joker = item.GetJoker();
                                        bool typeMatches = false;
                                        
                                        if (!clause.IsWildcard)
                                        {
                                            if (clause.JokerTypes?.Count > 0)
                                            {
                                                // Multi-value: OR logic - match any joker in the list
                                                typeMatches = clause.JokerTypes.Contains(joker);
                                            }
                                            else
                                            {
                                                // Single value
                                                typeMatches = joker == clause.JokerType;
                                            }
                                        }
                                        else
                                        {
                                            typeMatches = CheckWildcardMatch(joker, clause.WildcardEnum);
                                        }
                                        bool editionMatches = !clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value;
                                        
                                        if (typeMatches && editionMatches)
                                        {
                                            clauseMasks[clauseIndex] = VectorMask.AllBitsSet;
                                        }
                                    }
                                }
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
            });}
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckWildcardMatch(MotelyJoker joker, MotelyJsonConfigWildcards? wildcard)
    {
        if (!wildcard.HasValue) return false;
        if (wildcard == MotelyJsonConfigWildcards.AnyJoker) return true;

        var rarity = (MotelyJokerRarity)((int)joker & Motely.JokerRarityMask);
        return wildcard switch
        {
            MotelyJsonConfigWildcards.AnyCommon => rarity == MotelyJokerRarity.Common,
            MotelyJsonConfigWildcards.AnyUncommon => rarity == MotelyJokerRarity.Uncommon,
            MotelyJsonConfigWildcards.AnyRare => rarity == MotelyJokerRarity.Rare,
            _ => false
        };
    }
}