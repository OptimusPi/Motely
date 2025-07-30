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
        DebugLogger.Log("[OuijaJsonFilterDesc] CreateFilter called!");
        DebugLogger.Log($"[OuijaJsonFilterDesc] Config has {Config.Must.Count} MUST clauses");
        
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
            DebugLogger.Log("[OuijaJsonFilter] Constructor called!");
            DebugLogger.Log($"[OuijaJsonFilter] Must clauses: {config.Must.Count}");
            _config = config;
            _cutoff = cutoff;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            DebugLogger.Log($"[Filter] ============= VECTOR FILTER CALLED =============");
            DebugLogger.Log($"[Filter] Config has {_config.Must.Count} MUST clauses");
            if (IsCancelled) 
            {
                DebugLogger.Log($"[Filter] Cancelled, returning AllBitsClear");
                return VectorMask.AllBitsClear;
            }
            
            VectorMask mask = VectorMask.AllBitsSet;
            
            
            // Process MUST clauses - all must match
            foreach (var must in _config.Must)
            {
                DebugLogger.Log($"[Filter] Processing MUST clause: {must.Type} = {must.Value}");
                mask &= ProcessClause(ref searchContext, must, true);
                if (mask.IsAllFalse()) return mask;
            }
            
            // Process MUST NOT clauses - none can match
            foreach (var mustNot in _config.MustNot)
            {
                DebugLogger.Log($"[Filter] Processing MUST NOT clause: {mustNot.Type} = {mustNot.Value}");
                mask &= ProcessClause(ref searchContext, mustNot, true) ^ VectorMask.AllBitsSet;
                if (mask.IsAllFalse()) return mask;
            }
            
            // Process SHOULD clauses for scoring
            var config = _config; // Capture for lambda
            var cutoff = _cutoff; // Capture for lambda
            
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                DebugLogger.Log($"[Filter] Processing individual seed: {singleCtx.GetSeed()}");
                if (IsCancelled) return false;
                
                var currentSeed = singleCtx.GetSeed();
                DebugLogger.Log($"[SEARCH] ========== CHECKING SEED: {currentSeed} ==========");
                
                // Check MUST clauses first - all must match
                foreach (var must in config.Must)
                {
                    DebugLogger.Log($"[SEARCH] Looking for: {must.Type} = {must.Value}, Edition = {must.Edition}");
                    if (!CheckSingleClause(ref singleCtx, must, config.MaxSearchAnte))
                    {
                        DebugLogger.Log($"[SEARCH] NOT FOUND!");
                        return false;
                    }
                    DebugLogger.Log($"[SEARCH] FOUND!");
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
                    DebugLogger.Log($"[Filter] Seed {currentSeed}: Checking SHOULD clause: {should.Type} = {should.Value}");
                    int occurrences = CountOccurrences(ref singleCtx, should, config.MaxSearchAnte);
                    if (occurrences > 0)
                    {
                        int clauseScore = should.Score * occurrences;
                        totalScore += clauseScore;
                        scoreDetails.Add(clauseScore);
                        DebugLogger.Log($"[Filter] Found {occurrences} occurrences, score = {should.Score} * {occurrences} = {clauseScore}");
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
            {
                var validTags = string.Join(", ", Enum.GetNames(typeof(MotelyTag)));
                throw new ArgumentException($"INVALID TAG NAME: '{clause.Value}' is not a valid tag!\nValid tags are: {validTags}");
            }
            
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
            {
                var validVouchers = string.Join(", ", Enum.GetNames(typeof(MotelyVoucher)));
                throw new ArgumentException($"INVALID VOUCHER NAME: '{clause.Value}' is not a valid voucher!\nValid vouchers are: {validVouchers}");
            }
                
            return VectorEnum256.Equals(voucher, targetVoucher);
        }
        
        private static int CountOccurrences(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int maxSearchAnte)
        {
            int totalCount = 0;
            DebugLogger.Log($"[CountOccurrences] Counting occurrences in antes: {string.Join(",", clause.SearchAntes)} (max: {maxSearchAnte})");
            
            foreach (var ante in clause.SearchAntes)
            {
                if (ante > maxSearchAnte) continue;
                
                DebugLogger.Log($"[CountOccurrences] Checking ante {ante}...");
                int anteCount = 0;
                
                switch (clause.Type.ToLower())
                {
                    case "joker":
                    case "souljoker":
                        anteCount = CheckJoker(ref ctx, clause, ante);
                        break;
                        
                    // For other types, just return 1 if found (for now)
                    case "tarot":
                    case "tarotcard":
                        anteCount = CheckTarot(ref ctx, clause, ante) ? 1 : 0;
                        break;
                        
                    case "planet":
                    case "planetcard":
                        anteCount = CheckPlanet(ref ctx, clause, ante) ? 1 : 0;
                        break;
                        
                    case "spectral":
                    case "spectralcard":
                        anteCount = CheckSpectral(ref ctx, clause, ante) ? 1 : 0;
                        break;
                        
                    case "tag":
                    case "smallblindtag":
                    case "bigblindtag":
                        anteCount = CheckTagSingle(ref ctx, clause, ante) ? 1 : 0;
                        break;
                        
                    case "voucher":
                        anteCount = CheckVoucherSingle(ref ctx, clause, ante) ? 1 : 0;
                        break;
                        
                    case "playingcard":
                        anteCount = CheckPlayingCard(ref ctx, clause, ante) ? 1 : 0;
                        break;
                }
                
                totalCount += anteCount;
                if (anteCount > 0)
                {
                    DebugLogger.Log($"[CountOccurrences] Found {anteCount} in ante {ante}");
                }
            }
            
            DebugLogger.Log($"[CountOccurrences] Total occurrences: {totalCount}");
            return totalCount;
        }
        
        private static bool CheckSingleClause(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int maxSearchAnte)
        {
            DebugLogger.Log($"[CheckSingleClause] Checking antes: {string.Join(",", clause.SearchAntes)} (max: {maxSearchAnte})");
            foreach (var ante in clause.SearchAntes)
            {
                if (ante > maxSearchAnte) 
                {
                    DebugLogger.Log($"[CheckSingleClause] Skipping ante {ante} (> max {maxSearchAnte})");
                    continue;
                }
                
                DebugLogger.Log($"[CheckSingleClause] Checking ante {ante}...");
                bool found = false;
                
                switch (clause.Type.ToLower())
                {
                    case "joker":
                    case "souljoker":
                        found = CheckJoker(ref ctx, clause, ante) > 0;
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
        
        private static int CheckJoker(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            DebugLogger.Log($"[CheckJoker] Searching for: Value={clause.Value}, Edition={clause.Edition}, Ante={ante}, Sources={string.Join(",", clause.Sources ?? new List<string>())}");
            
            // Check if searching for "any" joker with specific edition
            bool searchAnyJoker = string.IsNullOrEmpty(clause.Value) || 
                                  clause.Value.Equals("any", StringComparison.OrdinalIgnoreCase) ||
                                  clause.Value.Equals("*", StringComparison.OrdinalIgnoreCase);
            
            MotelyJoker targetJoker = MotelyJoker.Joker;
            if (!searchAnyJoker && !Enum.TryParse<MotelyJoker>(clause.Value, true, out targetJoker))
            {
                var validJokers = string.Join(", ", Enum.GetNames(typeof(MotelyJoker)));
                throw new ArgumentException($"INVALID JOKER NAME: '{clause.Value}' is not a valid joker!\nValid jokers are: {validJokers}\nUse 'any', '*', or leave empty to match any joker.");
            }
            
            int foundCount = 0;
            
            // Debug logging
            DebugLogger.Log($"[CheckJoker] Looking for {targetJoker} in ante {ante}");
                
            // Check if this is a legendary joker
            bool isLegendary = targetJoker == MotelyJoker.Perkeo || 
                               targetJoker == MotelyJoker.Canio || 
                               targetJoker == MotelyJoker.Triboulet || 
                               targetJoker == MotelyJoker.Yorick || 
                               targetJoker == MotelyJoker.Chicot;
                
            // Check shop (legendary jokers don't appear in shop)
            if (clause.IncludeShopStream && !isLegendary)
            {
                var shop = ctx.GenerateFullShop(ante);
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                // Debug: Print shop contents
                DebugLogger.Log($"[CheckJoker] === SHOP CONTENTS FOR ANTE {ante} ===  ");
                for (int i = 0; i < maxSlots; i++)
                {
                    ref var shopItem = ref shop.Items[i];
                    if (shopItem.Type == ShopState.ShopItem.ShopItemType.Joker)
                    {
                        DebugLogger.Log($"[CheckJoker] Shop slot {i}: Joker = {shopItem.Joker}, Edition = {shopItem.Edition}");
                        // Also log the raw item value for debugging
                        DebugLogger.Log($"[CheckJoker]   -> Raw Item: Type={shopItem.Item.Type}, Value={shopItem.Item.Value:X8}, TypeCategory={shopItem.Item.TypeCategory}");
                    }
                    else
                    {
                        DebugLogger.Log($"[CheckJoker] Shop slot {i}: {shopItem.Type}");
                    }
                }
                DebugLogger.Log($"[CheckJoker] === END SHOP CONTENTS ===");
                
                for (int i = 0; i < maxSlots; i++)
                {
                    ref var item = ref shop.Items[i];
                    if (item.Type == ShopState.ShopItem.ShopItemType.Joker)
                    {
                        // Extract joker without edition bits for comparison
                        var shopJoker = (MotelyJoker)(item.Item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        
                        // Check if we're looking for any joker or a specific one
                        bool jokerMatches = searchAnyJoker || shopJoker == targetJoker;
                        
                        if (jokerMatches && CheckEditionAndStickers(item, clause))
                        {
                            foundCount++;
                            string jokerName = searchAnyJoker ? $"any joker ({shopJoker})" : targetJoker.ToString();
                            DebugLogger.Log($"[CheckJoker] Found {jokerName} in shop! Shop reports: {item.Joker}, Extracted: {shopJoker}, Total count: {foundCount}");
                        }
                    }
                }
            }
            
            // Check booster packs
            if (clause.IncludeBoosterPacks)
            {
                DebugLogger.Log($"[CheckJoker] Checking booster packs in ante {ante}...");
                
                // First print what's in the shop
                DebugLogger.Log($"[CheckJoker] === SHOP CONTENTS FOR ANTE {ante} ===");
                var shop = ctx.GenerateFullShop(ante);
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                for (int i = 0; i < maxSlots; i++)
                {
                    ref var item = ref shop.Items[i];
                    if (item.Type == ShopState.ShopItem.ShopItemType.Joker)
                    {
                        DebugLogger.Log($"[CheckJoker] Shop slot {i}: Joker = {item.Joker}, Edition = {item.Edition}");
                        // Also log the raw item value for debugging
                        DebugLogger.Log($"[CheckJoker]   -> Raw Item: Type={item.Item.Type}, Value={item.Item.Value:X8}, TypeCategory={item.Item.TypeCategory}");
                    }
                    else
                    {
                        DebugLogger.Log($"[CheckJoker] Shop slot {i}: {item.Type}");
                    }
                }
                DebugLogger.Log($"[CheckJoker] === END SHOP CONTENTS ===");
                
                var packStream = ctx.CreateBoosterPackStream(ante);
                MotelySingleTarotStream tarotStream = default;
                MotelySingleSpectralStream spectralStream = default;
                MotelySingleJokerFixedRarityStream soulStream = default;
                bool tarotStreamInit = false;
                bool spectralStreamInit = false;
                bool soulStreamInit = false;
                
                // Check ALL available packs
                // The user can control which antes to search via the JSON
                int packCount = ante == 1 ? 4 : 6; // Ante 1 has 4 packs, others have 6
                
                DebugLogger.Log($"[CheckJoker] === BOOSTER PACK CONTENTS FOR ANTE {ante} ===");
                
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    DebugLogger.Log($"[CheckJoker] Pack #{i+1}: Type={pack.GetPackType()}, Size={pack.GetPackSize()}");
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        DebugLogger.Log($"[CheckJoker] Found Arcana pack #{i+1} at ante {ante}, checking for The Soul...");
                        if (!tarotStreamInit)
                        {
                            tarotStreamInit = true;
                            tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
                        }
                        
                        var hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                        DebugLogger.Log($"[CheckJoker] Arcana pack has The Soul: {hasSoul}");
                        if (hasSoul)
                        {
                            if (!soulStreamInit)
                            {
                                soulStreamInit = true;
                                soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
                            }
                            DebugLogger.Log($"[CheckJoker] Soul stream - DoesProvideEdition: {soulStream.DoesProvideEdition}");
                            var soulJoker = ctx.GetNextJoker(ref soulStream);
                            DebugLogger.Log($"[CheckJoker] The Soul gives: {soulJoker.Type}, Edition={soulJoker.Edition}");
                            
                            var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)targetJoker);
                            DebugLogger.Log($"[CheckJoker] Soul joker type={soulJoker.Type}, Target={targetType}, Edition={soulJoker.Edition}");
                            
                            // Extract joker type without edition bits (same fix as shop)
                            var soulJokerType = (MotelyItemType)(soulJoker.Value & Motely.ItemTypeMask);
                            
                            // Check if we're looking for any joker or a specific one
                            bool jokerMatches = searchAnyJoker || soulJokerType == targetType;
                            
                            if (jokerMatches)
                            {
                                string jokerName = searchAnyJoker ? $"any joker (The Soul)" : targetJoker.ToString();
                                DebugLogger.Log($"[CheckJoker] Match! {jokerName} Edition={soulJoker.Edition}, Required Edition={clause.Edition}");
                                if (CheckEditionAndStickers(soulJoker, clause))
                                {
                                    foundCount++;
                                    DebugLogger.Log($"[CheckJoker] Edition check PASSED! Found {jokerName} with edition {soulJoker.Edition}. Total count: {foundCount}");
                                }
                                else
                                {
                                    DebugLogger.Log($"[CheckJoker] Edition check FAILED! Found {jokerName} but edition {soulJoker.Edition} != {clause.Edition}");
                                }
                            }
                        }
                    }
                    else if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        DebugLogger.Log($"[CheckJoker] Found Buffoon pack #{i+1}, checking for jokers...");

                        // Use the proper method to get Buffoon pack contents
                        // IMPORTANT: Pack number should be i (0-based) not i+1
                        var contents = ctx.GetNextBuffoonPackContents(ante, i, pack.GetPackSize());
                        
                        DebugLogger.Log($"[CheckJoker] Buffoon pack size: {pack.GetPackSize()}, contains {contents.Length} jokers");
                        
                        // Check each joker in the pack
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var joker = contents.GetItem(j);
                            // Extract the actual joker enum value
                            var extractedJoker = (MotelyJoker)(joker.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                            DebugLogger.Log($"[CheckJoker]   Buffoon pack slot {j}: ExtractedJoker={extractedJoker}, Edition={joker.Edition}");
                            DebugLogger.Log($"[CheckJoker]     -> Raw: Value={joker.Value:X8}, TypeCategory={joker.TypeCategory}");
                            
                            // Extract joker type without edition bits (same fix as shop)
                            var jokerType = (MotelyItemType)(joker.Value & Motely.ItemTypeMask);
                            var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)targetJoker);
                            
                            // Check if we're looking for any joker or a specific one
                            bool jokerMatches = searchAnyJoker || jokerType == targetType;
                            
                            if (jokerMatches)
                            {
                                string jokerName = searchAnyJoker ? $"any joker (Buffoon)" : targetJoker.ToString();
                                DebugLogger.Log($"[CheckJoker] Buffoon Match! {jokerName} Edition={joker.Edition}, Required Edition={clause.Edition}");
                                DebugLogger.Log($"[CheckJoker]   -> Actual Joker: {extractedJoker}, Target: {targetJoker}, Match: {extractedJoker == targetJoker}");
                                if (CheckEditionAndStickers(joker, clause))
                                {
                                    foundCount++;
                                    string foundJokerName = searchAnyJoker ? $"any joker ({extractedJoker})" : extractedJoker.ToString();
                                    DebugLogger.Log($"[CheckJoker] Buffoon Edition check PASSED! Found {foundJokerName} with edition {joker.Edition}. Total count: {foundCount}");
                                    DebugLogger.Log($">>> FOUND IN PACK: Ante={ante}, Pack=#{i+1}, Slot={j}, Joker={extractedJoker}, Edition={joker.Edition}");
                                }
                                else
                                {
                                    DebugLogger.Log($"[CheckJoker] Buffoon Edition check FAILED! Found {jokerName} but edition {joker.Edition} != {clause.Edition}");
                                }
                            }
                        }
                    }
                    else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        DebugLogger.Log($"[CheckJoker] Found Spectral pack #{i+1}, checking for The Soul...");
                        if (!spectralStreamInit)
                        {
                            spectralStreamInit = true;
                            spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: true);
                        }
                        
                        if (ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                        {
                            DebugLogger.Log($"[CheckJoker] Spectral pack has The Soul!");
                            if (!soulStreamInit)
                            {
                                soulStreamInit = true;
                                soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
                            }
                            var soulJoker = ctx.GetNextJoker(ref soulStream);
                            DebugLogger.Log($"[CheckJoker] Found joker from The Soul (Spectral): {soulJoker.Type}, Edition={soulJoker.Edition}");
                            
                            var targetType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)targetJoker);
                            DebugLogger.Log($"[CheckJoker] Spectral Soul joker type={soulJoker.Type}, Target={targetType}, Edition={soulJoker.Edition}");
                            
                            // Extract joker type without edition bits (same fix as shop)
                            var soulJokerType = (MotelyItemType)(soulJoker.Value & Motely.ItemTypeMask);
                            
                            // Check if we're looking for any joker or a specific one
                            bool jokerMatches = searchAnyJoker || soulJokerType == targetType;
                            
                            if (jokerMatches)
                            {
                                string jokerName = searchAnyJoker ? $"any joker (Black Hole)" : targetJoker.ToString();
                                DebugLogger.Log($"[CheckJoker] Spectral Match! {jokerName} Edition={soulJoker.Edition}, Required Edition={clause.Edition}");
                                if (CheckEditionAndStickers(soulJoker, clause))
                                {
                                    foundCount++;
                                    DebugLogger.Log($"[CheckJoker] Spectral Edition check PASSED! Found {jokerName} with edition {soulJoker.Edition}. Total count: {foundCount}");
                                }
                                else
                                {
                                    DebugLogger.Log($"[CheckJoker] Spectral Edition check FAILED! Found {jokerName} but edition {soulJoker.Edition} != {clause.Edition}");
                                }
                            }
                        }
                    }
                    else if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                    {
                        DebugLogger.Log($"[CheckJoker]   Standard pack - contains playing cards");
                    }
                    else if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                    {
                        DebugLogger.Log($"[CheckJoker]   Celestial pack - contains planet cards");
                    }
                }
                
                DebugLogger.Log($"[CheckJoker] === END BOOSTER PACK CONTENTS ===");
            }
            
            string searchDescription = searchAnyJoker ? "any jokers" : targetJoker.ToString();
            DebugLogger.Log($"[CheckJoker] === FINAL COUNT for {searchDescription} in ante {ante}: {foundCount} ===");
            DebugLogger.Log($"[CheckJoker] Sources checked: Shop={clause.IncludeShopStream}, Packs={clause.IncludeBoosterPacks}, Tags={clause.IncludeSkipTags}");
            return foundCount;
        }
        
        private static bool CheckTarot(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!Enum.TryParse<MotelyTarotCard>(clause.Value, true, out var targetTarot))
            {
                var validTarots = string.Join(", ", Enum.GetNames(typeof(MotelyTarotCard)));
                throw new ArgumentException($"INVALID TAROT NAME: '{clause.Value}' is not a valid tarot!\nValid tarots are: {validTarots}");
            }
                
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
            {
                var validPlanets = string.Join(", ", Enum.GetNames(typeof(MotelyPlanetCard)));
                throw new ArgumentException($"INVALID PLANET NAME: '{clause.Value}' is not a valid planet!\nValid planets are: {validPlanets}");
            }
                
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
            {
                var validSpectrals = string.Join(", ", Enum.GetNames(typeof(MotelySpectralCard)));
                throw new ArgumentException($"INVALID SPECTRAL NAME: '{clause.Value}' is not a valid spectral!\nValid spectrals are: {validSpectrals}");
            }
                
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
            {
                var validTags = string.Join(", ", Enum.GetNames(typeof(MotelyTag)));
                throw new ArgumentException($"INVALID TAG NAME: '{clause.Value}' is not a valid tag!\nValid tags are: {validTags}");
            }
                
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
            {
                var validVouchers = string.Join(", ", Enum.GetNames(typeof(MotelyVoucher)));
                throw new ArgumentException($"INVALID VOUCHER NAME: '{clause.Value}' is not a valid voucher!\nValid vouchers are: {validVouchers}");
            }
                
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