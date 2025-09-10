using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on joker criteria from JSON configuration.
/// REVERTED: Simple version that compiles with fixed slot range
/// </summary>
public struct MotelyJsonJokerFilterDesc(List<MotelyJsonJokerFilterClause> jokerClauses)
    : IMotelySeedFilterDesc<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>
{
    private readonly List<MotelyJsonJokerFilterClause> _jokerClauses = jokerClauses;

    public MotelyJsonJokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var (minAnte, maxAnte) = MotelyJsonFilterClause.CalculateAnteRange(_jokerClauses);
        
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
            
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            
            return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
                Span<bool> clauseMatches = stackalloc bool[clauses.Count];
                for (int i = 0; i < clauseMatches.Length; i++) clauseMatches[i] = false;
                
                for (int ante = minAnte; ante <= maxAnte; ante++)
                {
                    ulong anteBit = 1UL << (ante - 1);
                    
                    for (int clauseIndex = 0; clauseIndex < clauses.Count; clauseIndex++)
                    {
                        var clause = clauses[clauseIndex];
                        if (clause.AnteBitmask != 0 && (clause.AnteBitmask & anteBit) == 0) continue;
                        
                        // ✅ FIXED: Check shop slots with extended range for slot 7+
                        if (clause.ShopSlotBitmask != 0)
                        {
                            var shopStream = singleCtx.CreateShopItemStream(ante, isCached: false);
                            int maxSlot = GetMaxShopSlot(clause.ShopSlotBitmask, ante);
                            
                            for (int i = 0; i < maxSlot; i++)
                            {
                                var item = singleCtx.GetNextShopItem(ref shopStream);
                                if (((clause.ShopSlotBitmask >> i) & 1) != 0 && item.TypeCategory == MotelyItemTypeCategory.Joker)
                                {
                                    var joker = new MotelyItem(item.Value).GetJoker();
                                    bool typeMatches = CheckJokerTypeMatch(joker, clause);
                                    bool editionMatches = !clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value;
                                    
                                    if (typeMatches && editionMatches)
                                    {
                                        clauseMatches[clauseIndex] = true;
                                    }
                                }
                            }
                        }
                        
                        // Check pack slots
                        if (clause.PackSlotBitmask != 0)
                        {
                            var packStream = singleCtx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
                            var buffoonStream = singleCtx.CreateBuffoonPackJokerStream(ante);
                            
                            int maxPack = 64 - System.Numerics.BitOperations.LeadingZeroCount(clause.PackSlotBitmask);
                            for (int i = 0; i < maxPack; i++)
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
                                        bool typeMatches = CheckJokerTypeMatch(joker, clause);
                                        bool editionMatches = !clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value;
                                        
                                        if (typeMatches && editionMatches)
                                        {
                                            clauseMatches[clauseIndex] = true;
                                        }
                                    }
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
        private static int GetMaxShopSlot(ulong bitmask, int ante)
        {
            if (bitmask == 0) return ante == 1 ? 4 : 6; // Default 4/6 concept
            
            // Find highest bit + 1, but ensure minimum of 6 for ante 2+ to handle extended slots
            int maxSpecified = 64 - System.Numerics.BitOperations.LeadingZeroCount(bitmask);
            return ante == 1 ? Math.Max(4, maxSpecified) : Math.Max(6, maxSpecified);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckJokerTypeMatch(MotelyJoker joker, MotelyJsonJokerFilterClause clause)
        {
            if (!clause.IsWildcard)
            {
                if (clause.JokerTypes?.Count > 0)
                {
                    return clause.JokerTypes.Contains(joker);
                }
                else
                {
                    return joker == clause.JokerType;
                }
            }
            else
            {
                return CheckWildcardMatch(joker, clause.WildcardEnum);
            }
        }
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
