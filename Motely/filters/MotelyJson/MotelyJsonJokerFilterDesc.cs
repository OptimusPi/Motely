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
            
            // Quick vectorized check for potential jokers
            VectorMask hasPotential = VectorMask.NoBitsSet;
            
            for (int ante = _minAnte; ante <= _maxAnte; ante++)
            {
                // Check shop slots for any jokers
                var shopStream = ctx.CreateShopItemStream(ante);
                int maxShopSlots = 10; // Default max shop slots for potential check
                for (int shopSlot = 0; shopSlot < maxShopSlots; shopSlot++)
                {
                    var shopItem = ctx.GetNextShopItem(ref shopStream);
                    var shopPotential = VectorEnum256.Equals(shopItem.TypeCategory, MotelyItemTypeCategory.Joker);
                    hasPotential |= shopPotential;
                }
                
                // Check buffoon packs for any jokers
                var packStream = ctx.CreateBoosterPackStream(ante, generatedFirstPack: ante != 1);
                var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                int totalPacks = ante == 1 ? 4 : 6;
                for (int packSlot = 0; packSlot < totalPacks; packSlot++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    VectorMask isBuffoonPack = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Buffoon);
                    
                    if (isBuffoonPack.IsPartiallyTrue())
                    {
                        var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize()[0]);
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var packPotential = VectorEnum256.Equals(contents[j].TypeCategory, MotelyItemTypeCategory.Joker);
                            hasPotential |= (packPotential & isBuffoonPack);
                        }
                    }
                }
            }
            
            // Early exit if no potential matches
            if (hasPotential.IsAllFalse())
                return VectorMask.NoBitsSet;
            
            // Copy struct fields to local variables for lambda
            var clauses = _clauses;
            int minAnte = _minAnte;
            int maxAnte = _maxAnte;
            
            // Now do full individual processing for seeds with potential
            return ctx.SearchIndividualSeeds(hasPotential, (ref MotelySingleSearchContext singleCtx) =>
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
                                    bool typeMatches = !clause.IsWildcard ? joker == clause.JokerType : true; // TODO: Add wildcard support
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
                                        bool typeMatches = !clause.IsWildcard ? joker == clause.JokerType : true; // TODO: Add wildcard support
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
            });
        }
    }
}