using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;

namespace Motely.Filters;

/// <summary>
/// Clean filter descriptor for MongoDB-style queries
/// </summary>
public struct OuijaJsonFilterDesc : IMotelySeedFilterDesc<OuijaJsonFilterDesc.OuijaJsonFilter>
{
    public OuijaConfig Config { get; }
    public int Cutoff { get; set; }

    public OuijaJsonFilterDesc(OuijaConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Cutoff = config.MinimumScore;
    }

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache streams for all antes we need
        var allAntes = new HashSet<int>();
        
        var config = Config; // Capture for lambda
        Action<List<OuijaConfig.FilterItem>> collectAntes = (items) =>
        {
            foreach (var item in items)
                foreach (var ante in item.SearchAntes)
                    if (ante <= config.MaxSearchAnte)
                        allAntes.Add(ante);
        };
        
        collectAntes(Config.Must);
        collectAntes(Config.Should);
        collectAntes(Config.MustNot);
        
        foreach (var ante in allAntes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheTagStream(ante);
            ctx.CacheVoucherStream(ante);
            ctx.CacheSoulJokerStream(ante);
        }
        
        return new OuijaJsonFilter(Config, Cutoff);
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        private readonly OuijaConfig _config;
        private readonly int _cutoff;
        
        public static ConcurrentQueue<OuijaResult> ResultsQueue = new();
        public static bool IsCancelled = false;

        public OuijaJsonFilter(OuijaConfig config, int cutoff)
        {
            _config = config;
            _cutoff = cutoff;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            if (IsCancelled) return VectorMask.AllBitsClear;
            
            VectorMask mask = VectorMask.AllBitsSet;
            
            
            // Process MUST clauses - all must match
            foreach (var must in _config.Must)
            {
                mask &= ProcessClause(ref searchContext, must, true);
                if (mask.IsAllFalse()) return mask;
            }
            
            // Process MUST NOT clauses - none can match
            foreach (var mustNot in _config.MustNot)
            {
                mask &= ProcessClause(ref searchContext, mustNot, true) ^ VectorMask.AllBitsSet;
                if (mask.IsAllFalse()) return mask;
            }
            
            // Process SHOULD clauses for scoring
            var config = _config; // Capture for lambda
            var cutoff = _cutoff; // Capture for lambda
            
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                if (IsCancelled) return false;
                
                // Check MUST clauses first - all must match
                foreach (var must in config.Must)
                {
                    if (!CheckSingleClause(ref singleCtx, must, config.MaxSearchAnte))
                        return false;
                }
                
                // Check MUST NOT clauses - none can match
                foreach (var mustNot in config.MustNot)
                {
                    if (CheckSingleClause(ref singleCtx, mustNot, config.MaxSearchAnte))
                        return false;
                }
                
                int totalScore = 0;
                var scoreDetails = new List<int>();

                // Calculate scores from SHOULD clauses
                foreach (var should in config.Should)
                {
                    if (CheckSingleClause(ref singleCtx, should, config.MaxSearchAnte))
                    {
                        totalScore += should.Score;
                        scoreDetails.Add(should.Score);
                    }
                    else
                    {
                        scoreDetails.Add(0);
                    }
                }
                
                // Check if meets minimum score
                if (totalScore >= cutoff)
                {
                    var result = new OuijaResult
                    {
                        Seed = singleCtx.GetSeed(),
                        TotalScore = totalScore,
                        ScoreWants = scoreDetails.ToArray(),
                        Success = true
                    };
                    
                    ResultsQueue.Enqueue(result);
                    return true;
                }
                
                return false;
            });
        }
        
        private VectorMask ProcessClause(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, bool orAcrossAntes)
        {
            VectorMask result = orAcrossAntes ? VectorMask.AllBitsClear : VectorMask.AllBitsSet;
            var maxSearchAnte = _config.MaxSearchAnte; // Capture for comparison
            
            foreach (var ante in clause.SearchAntes)
            {
                if (ante > maxSearchAnte) continue;
                
                VectorMask anteMask = VectorMask.AllBitsClear;
                
                // Handle different types
                switch (clause.Type.ToLower())
                {
                    case "tag":
                    case "smallblindtag":
                    case "bigblindtag":
                        anteMask = CheckTag(ref ctx, clause, ante);
                        break;
                        
                    case "voucher":
                        anteMask = CheckVoucher(ref ctx, clause, ante);
                        break;
                        
                    default:
                        // For non-vectorizable types, we'll handle in individual search
                        anteMask = VectorMask.AllBitsSet;
                        break;
                }
                
                if (orAcrossAntes)
                    result |= anteMask;
                else
                    result &= anteMask;
            }
            
            return result;
        }
        
        private VectorMask CheckTag(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);
            
            if (!Enum.TryParse<MotelyTag>(clause.Value, true, out var targetTag))
                return VectorMask.AllBitsClear;
            
            var isSmall = clause.Type.Contains("Small", StringComparison.OrdinalIgnoreCase);
            var isBig = clause.Type.Contains("Big", StringComparison.OrdinalIgnoreCase);
            
            if (isSmall && !isBig)
                return VectorEnum256.Equals(smallTag, targetTag);
            else if (isBig && !isSmall)
                return VectorEnum256.Equals(bigTag, targetTag);
            else
                return VectorEnum256.Equals(smallTag, targetTag) | VectorEnum256.Equals(bigTag, targetTag);
        }
        
        private VectorMask CheckVoucher(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var voucher = ctx.GetAnteFirstVoucher(ante);
            
            if (!Enum.TryParse<MotelyVoucher>(clause.Value, true, out var targetVoucher))
                return VectorMask.AllBitsClear;
                
            return VectorEnum256.Equals(voucher, targetVoucher);
        }
        
        private static bool CheckSingleClause(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int maxSearchAnte)
        {
            foreach (var ante in clause.SearchAntes)
            {
                if (ante > maxSearchAnte) continue;
                
                bool found = false;
                
                switch (clause.Type.ToLower())
                {
                    case "joker":
                    case "souljoker":
                        found = CheckJoker(ref ctx, clause, ante);
                        break;
                        
                    case "tarot":
                    case "tarotcard":
                        found = CheckTarot(ref ctx, clause, ante);
                        break;
                        
                    case "planet":
                    case "planetcard":
                        found = CheckPlanet(ref ctx, clause, ante);
                        break;
                        
                    case "spectral":
                    case "spectralcard":
                        found = CheckSpectral(ref ctx, clause, ante);
                        break;
                        
                    case "tag":
                    case "smallblindtag":
                    case "bigblindtag":
                        found = CheckTagSingle(ref ctx, clause, ante);
                        break;
                        
                    case "voucher":
                        found = CheckVoucherSingle(ref ctx, clause, ante);
                        break;
                        
                    case "playingcard":
                        found = CheckPlayingCard(ref ctx, clause, ante);
                        break;
                }
                
                if (found) return true;
            }
            
            return false;
        }
        
        private static bool CheckJoker(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!Enum.TryParse<MotelyJoker>(clause.Value, true, out var targetJoker))
                return false;
                
            // Check shop
            if (clause.IncludeShopStream)
            {
                var shop = ctx.GenerateFullShop(ante);
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    ref var item = ref shop.Items[i];
                    if (item.Type == ShopState.ShopItem.ShopItemType.Joker && item.Joker == targetJoker)
                    {
                        if (CheckEditionAndStickers(item, clause))
                            return true;
                    }
                }
            }
            
            // Check booster packs
            if (clause.IncludeBoosterPacks)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                
                // Check up to 3 packs available in the ante
                for (int i = 0; i < 3; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    // Check Buffoon packs for jokers
                    if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        // Buffoon packs appear to be generated via tags, not direct joker streams
                        // The Buffoon Tag creates jokers when skipped
                        // For now, we'll skip this as it requires tag simulation
                        continue;
                    }
                    
                    // Check if Arcana/Spectral pack has The Soul
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
                        if (ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                        {
                            var soulStream = ctx.CreateSoulJokerStream(ante);
                            var soulJoker = ctx.GetNextJoker(ref soulStream);
                            if (soulJoker.Type == (MotelyItemType)(int)targetJoker)
                            {
                                if (CheckEditionAndStickers(soulJoker, clause))
                                    return true;
                            }
                        }
                    }
                    else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: true);
                        if (ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                        {
                            var soulStream = ctx.CreateSoulJokerStream(ante);
                            var soulJoker = ctx.GetNextJoker(ref soulStream);
                            if (soulJoker.Type == (MotelyItemType)(int)targetJoker)
                            {
                                if (CheckEditionAndStickers(soulJoker, clause))
                                    return true;
                            }
                        }
                    }
                }
            }
            
            // Check soul jokers (from tags)
            if (clause.IncludeSkipTags && clause.Type.Equals("SoulJoker", StringComparison.OrdinalIgnoreCase))
            {
                var soulStream = ctx.CreateSoulJokerStream(ante);
                var soul1 = ctx.GetNextJoker(ref soulStream);
                var soul2 = ctx.GetNextJoker(ref soulStream);
                
                // MotelyItem encodes joker in Type property
                if (soul1.Type == (MotelyItemType)(int)targetJoker)
                    return true;
                if (soul2.Type == (MotelyItemType)(int)targetJoker)  
                    return true;
            }
            
            return false;
        }
        
        private static bool CheckTarot(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!Enum.TryParse<MotelyTarotCard>(clause.Value, true, out var targetTarot))
                return false;
                
            // Check shop
            if (clause.IncludeShopStream)
            {
                var shop = ctx.GenerateFullShop(ante);
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    ref var item = ref shop.Items[i];
                    if (item.Type == ShopState.ShopItem.ShopItemType.Tarot && item.Tarot == targetTarot)
                        return true;
                }
            }
            
            // Check booster packs
            if (clause.IncludeBoosterPacks)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                
                // Check up to 3 packs available in the ante
                for (int i = 0; i < 3; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                        var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                        
                        // Check each card in the pack
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents.GetItem(j);
                            if (item.Type == (MotelyItemType)targetTarot)
                                return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        private static bool CheckPlanet(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!Enum.TryParse<MotelyPlanetCard>(clause.Value, true, out var targetPlanet))
                return false;
                
            // Check shop
            if (clause.IncludeShopStream)
            {
                var shop = ctx.GenerateFullShop(ante);
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    ref var item = ref shop.Items[i];
                    if (item.Type == ShopState.ShopItem.ShopItemType.Planet && item.Planet == targetPlanet)
                        return true;
                }
            }
            
            // Check booster packs
            if (clause.IncludeBoosterPacks)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                
                // Check up to 3 packs available in the ante
                for (int i = 0; i < 3; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                    {
                        var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                        var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize());
                        
                        // Check each card in the pack
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents.GetItem(j);
                            if (item.Type == (MotelyItemType)targetPlanet)
                                return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        private static bool CheckSpectral(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!Enum.TryParse<MotelySpectralCard>(clause.Value, true, out var targetSpectral))
                return false;
                
            // Spectral cards appear only in packs, not in shop
            if (clause.IncludeBoosterPacks)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                
                // Check up to 3 packs available in the ante
                for (int i = 0; i < 3; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                        var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                        
                        // Check each card in the pack
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents.GetItem(j);
                            if (item.Type == (MotelyItemType)targetSpectral)
                                return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        private static bool CheckTagSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!Enum.TryParse<MotelyTag>(clause.Value, true, out var targetTag))
                return false;
                
            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);
            
            var isSmall = clause.Type.Contains("Small", StringComparison.OrdinalIgnoreCase);
            var isBig = clause.Type.Contains("Big", StringComparison.OrdinalIgnoreCase);
            
            if (isSmall && !isBig)
                return smallTag == targetTag;
            else if (isBig && !isSmall)
                return bigTag == targetTag;
            else
                return smallTag == targetTag || bigTag == targetTag;
        }
        
        private static bool CheckVoucherSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!Enum.TryParse<MotelyVoucher>(clause.Value, true, out var targetVoucher))
                return false;
                
            var voucher = ctx.GetAnteFirstVoucher(ante);
            return voucher == targetVoucher;
        }
        
        private static bool CheckPlayingCard(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // Playing cards can appear in Standard packs
            if (clause.IncludeBoosterPacks)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                
                // Check up to 3 packs available in the ante
                for (int i = 0; i < 3; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                    {
                        var cardStream = ctx.CreateStandardPackCardStream(ante);
                        var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                        
                        // Check each card in the pack
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents.GetItem(j);
                            if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard)
                            {
                                // For playing cards, we need to check the specific properties
                                
                                // Check suit if specified
                                if (!string.IsNullOrEmpty(clause.Suit))
                                {
                                    if (!Enum.TryParse<MotelyPlayingCardSuit>(clause.Suit, true, out var targetSuit))
                                        continue;
                                    if (item.PlayingCardSuit != targetSuit)
                                        continue;
                                }
                                
                                // Check rank if specified
                                if (!string.IsNullOrEmpty(clause.Rank))
                                {
                                    if (!Enum.TryParse<MotelyPlayingCardRank>(clause.Rank, true, out var targetRank))
                                        continue;
                                    if (item.PlayingCardRank != targetRank)
                                        continue;
                                }
                                
                                // Check enhancement if specified
                                if (!string.IsNullOrEmpty(clause.Enhancement))
                                {
                                    if (!Enum.TryParse<MotelyItemEnhancement>(clause.Enhancement, true, out var targetEnhancement))
                                        continue;
                                    if (item.Enhancement != targetEnhancement)
                                        continue;
                                }
                                
                                // Check seal if specified
                                if (!string.IsNullOrEmpty(clause.Seal))
                                {
                                    if (!Enum.TryParse<MotelyItemSeal>(clause.Seal, true, out var targetSeal))
                                        continue;
                                    if (item.Seal != targetSeal)
                                        continue;
                                }
                                
                                // Check edition if specified
                                if (!string.IsNullOrEmpty(clause.Edition))
                                {
                                    if (!Enum.TryParse<MotelyItemEdition>(clause.Edition, true, out var targetEdition))
                                        continue;
                                    if (item.Edition != targetEdition)
                                        continue;
                                }
                                
                                // If we get here, all specified criteria match
                                return true;
                            }
                        }
                    }
                }
            }
            
            // TODO: Check playing cards in deck/hand?
            return false;
        }
        
        private static bool CheckEditionAndStickers(in ShopState.ShopItem item, OuijaConfig.FilterItem clause)
        {
            // Check edition
            if (!string.IsNullOrEmpty(clause.Edition))
            {
                if (!Enum.TryParse<MotelyItemEdition>(clause.Edition, true, out var targetEdition))
                    return false;
                    
                if (item.Edition != targetEdition)
                    return false;
            }
            
            // TODO: Check stickers
            
            return true;
        }
        
        private static bool CheckEditionAndStickers(in MotelyItem item, OuijaConfig.FilterItem clause)
        {
            // Check edition
            if (!string.IsNullOrEmpty(clause.Edition))
            {
                if (!Enum.TryParse<MotelyItemEdition>(clause.Edition, true, out var targetEdition))
                    return false;
                    
                if (item.Edition != targetEdition)
                    return false;
            }
            
            // TODO: Check stickers
            
            return true;
        }
    }
}