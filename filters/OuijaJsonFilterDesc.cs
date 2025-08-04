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
        Cutoff = 0; // Set by command line --cutoff parameter
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
                    if (ante <= (config.Filter?.MaxAnte ?? 8))
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetFilterDescription(OuijaConfig.FilterItem item)
        {
            // PERFORMANCE: Only allocate when debug logging is enabled
            if (!DebugLogger.IsEnabled)
                return string.Empty;
                
            // Pre-allocate with reasonable capacity
            var parts = new List<string>(8)
            {
                $"{item.Type}"
            };
            
            if (item.ItemTypeEnum == MotelyFilterItemType.PlayingCard)
            {
                if (item.RankEnum.HasValue)
                    parts.Add($"rank={item.RankEnum.Value}");
                if (item.SuitEnum.HasValue)
                    parts.Add($"suit={item.SuitEnum.Value}");
            }
            else if (!string.IsNullOrEmpty(item.Value))
            {
                parts.Add($"name={item.Value}");
            }
            
            if (item.EditionEnum.HasValue)
                parts.Add($"edition={item.EditionEnum.Value}");
            if (item.EnhancementEnum.HasValue)
                parts.Add($"enhancement={item.EnhancementEnum.Value}");
            if (item.SealEnum.HasValue)
                parts.Add($"seal={item.SealEnum.Value}");
                
            return string.Join(", ", parts);
        }

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
            VectorMask mask = VectorMask.AllBitsSet;
            
            // CRITICAL PERFORMANCE: Early exit after EACH MUST clause if all seeds are eliminated
            // Process MUST clauses - all must match
            foreach (var must in _config.Must)
            {
                DebugLogger.Log($"[Filter] Processing MUST clause: {must.Type} = {must.Value}");
                mask &= ProcessClause(ref searchContext, must, true);
                
                // EARLY EXIT: If no seeds remain, stop processing immediately
                if (mask.IsAllFalse()) 
                {
                    DebugLogger.Log("[Filter] Early exit - all seeds eliminated after MUST clause");
                    return mask;
                }
            }
            
            // Process MUST NOT clauses - none can match
            foreach (var mustNot in _config.MustNot)
            {
                DebugLogger.Log($"[Filter] Processing MUST NOT clause: {mustNot.Type} = {mustNot.Value}");
                mask &= ProcessClause(ref searchContext, mustNot, true) ^ VectorMask.AllBitsSet;
                
                // EARLY EXIT: If no seeds remain, stop processing immediately
                if (mask.IsAllFalse()) 
                {
                    DebugLogger.Log("[Filter] Early exit - all seeds eliminated after MUST NOT clause");
                    return mask;
                }
            }
            
            // Process SHOULD clauses for scoring
            var config = _config; // Capture for lambda
            var cutoff = _cutoff; // Capture for lambda
            
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // EARLY EXIT: Check cancellation first
                if (IsCancelled) return false;
                
                var currentSeed = singleCtx.GetSeed();
                DebugLogger.Log($"[SEARCH] ========== CHECKING SEED: {currentSeed} ==========");
                
                // PERFORMANCE: Check MUST clauses first - exit immediately on failure
                // This avoids wasting time on expensive operations for seeds that don't match
                foreach (var must in config.Must)
                {
                    string searchDesc = GetFilterDescription(must);
                    DebugLogger.Log($"[SEARCH] Looking for: {searchDesc}");
                    if (!CheckSingleClause(ref singleCtx, must, config.Filter?.MaxAnte ?? 8))
                    {
                        DebugLogger.Log($"[SEARCH] NOT FOUND - exiting early!");
                        return false; // EARLY EXIT - no point checking other MUST clauses
                    }
                    DebugLogger.Log($"[SEARCH] FOUND!");
                }
                
                // Check MUST NOT clauses - none can match
                foreach (var mustNot in config.MustNot)
                {
                    if (CheckSingleClause(ref singleCtx, mustNot, config.Filter?.MaxAnte ?? 8))
                    {
                        DebugLogger.Log($"[SEARCH] Found MUST NOT item - exiting!");
                        return false; // EARLY EXIT
                    }
                }
                
                int totalScore = 0;
                // Pre-allocate based on SHOULD clause count
                var scoreDetails = new List<int>(config.Should.Count);

                // Calculate scores from SHOULD clauses
                foreach (var should in config.Should)
                {
                    DebugLogger.Log($"[Filter] Seed {currentSeed}: Checking SHOULD clause: {should.Type} = {should.Value}");
                    int occurrences = CountOccurrences(ref singleCtx, should, config.Filter?.MaxAnte ?? 8);
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
            // PERFORMANCE OPTIMIZATION: Many item types (jokers, spectral, etc) can't be vectorized
            // Return AllBitsSet immediately for those types to avoid wasted vector operations
            switch (clause.ItemTypeEnum)
            {
                case MotelyFilterItemType.Joker:
                case MotelyFilterItemType.SoulJoker:
                case MotelyFilterItemType.TarotCard:
                case MotelyFilterItemType.PlanetCard:
                case MotelyFilterItemType.SpectralCard:
                case MotelyFilterItemType.PlayingCard:
                    // These types can't be vectorized - handle in individual search
                    return VectorMask.AllBitsSet;
                    
                case MotelyFilterItemType.SmallBlindTag:
                case MotelyFilterItemType.BigBlindTag:
                case MotelyFilterItemType.Voucher:
                    // These CAN be vectorized - process normally
                    break;
                    
                default:
                    // Unknown types - assume can't be vectorized
                    return VectorMask.AllBitsSet;
            }
            
            VectorMask result = orAcrossAntes ? VectorMask.AllBitsClear : VectorMask.AllBitsSet;
            var maxSearchAnte = _config.Filter?.MaxAnte ?? 8; // Capture for comparison
            
            foreach (var ante in clause.SearchAntes)
            {
                if (ante > maxSearchAnte) continue;
                
                VectorMask anteMask = VectorMask.AllBitsClear;
                
                // Handle different types using pre-parsed enums
                switch (clause.ItemTypeEnum)
                {
                    case MotelyFilterItemType.SmallBlindTag:
                    case MotelyFilterItemType.BigBlindTag:
                        anteMask = CheckTag(ref ctx, clause, ante);
                        break;
                        
                    case MotelyFilterItemType.Voucher:
                        anteMask = CheckVoucher(ref ctx, clause, ante);
                        break;
                }
                
                if (orAcrossAntes)
                    result |= anteMask;
                else
                    result &= anteMask;
                    
                // EARLY EXIT: If processing AND clauses and result is already all false
                if (!orAcrossAntes && result.IsAllFalse())
                {
                    DebugLogger.Log($"[ProcessClause] Early exit - all seeds eliminated in ante {ante}");
                    return result;
                }
            }
            
            return result;
        }
        
        private VectorMask CheckTag(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);
            
            // Assume validation was done during config parsing
            if (!clause.TagEnum.HasValue)
                return VectorMask.AllBitsClear;
            
            var targetTag = clause.TagEnum.Value;
            
            // Use pre-parsed enum for type checking
            switch (clause.TagTypeEnum)
            {
                case MotelyTagType.SmallBlind:
                    return VectorEnum256.Equals(smallTag, targetTag);
                case MotelyTagType.BigBlind:
                    return VectorEnum256.Equals(bigTag, targetTag);
                default: // Any
                    return VectorEnum256.Equals(smallTag, targetTag) | VectorEnum256.Equals(bigTag, targetTag);
            }
        }
        
        private VectorMask CheckVoucher(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var voucher = ctx.GetAnteFirstVoucher(ante);
            
            // Assume validation was done during config parsing
            if (!clause.VoucherEnum.HasValue)
                return VectorMask.AllBitsClear;
                
            return VectorEnum256.Equals(voucher, clause.VoucherEnum.Value);
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
                
                // Create streams for this ante
                var shopStream = ctx.CreateShopItemStream(ante, isCached: true);
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: true);
                
                switch (clause.ItemTypeEnum)
                {
                    case MotelyFilterItemType.Joker:
                        anteCount = CheckJoker(ref ctx, clause, ante, ref shopStream, ref packStream);
                        break;
                        
                    case MotelyFilterItemType.SoulJoker:
                        anteCount = CheckSoulJoker(ref ctx, clause, ante, ref packStream);
                        break;
                        
                    // For other types, just return 1 if found (for now)
                    case MotelyFilterItemType.TarotCard:
                        anteCount = CheckTarot(ref ctx, clause, ante, ref shopStream, ref packStream) ? 1 : 0;
                        break;
                        
                    case MotelyFilterItemType.PlanetCard:
                        anteCount = CheckPlanet(ref ctx, clause, ante, ref shopStream, ref packStream) ? 1 : 0;
                        break;
                        
                    case MotelyFilterItemType.SpectralCard:
                        anteCount = CheckSpectral(ref ctx, clause, ante, ref packStream) ? 1 : 0;
                        break;
                        
                    case MotelyFilterItemType.SmallBlindTag:
                    case MotelyFilterItemType.BigBlindTag:
                        anteCount = CheckTagSingle(ref ctx, clause, ante) ? 1 : 0;
                        break;
                        
                    case MotelyFilterItemType.Voucher:
                        anteCount = CheckVoucherSingle(ref ctx, clause, ante) ? 1 : 0;
                        break;
                        
                    case MotelyFilterItemType.PlayingCard:
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
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckSingleClause(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int maxSearchAnte)
        {
            // PERFORMANCE: Early exit if no antes to check
            if (clause.SearchAntes == null || clause.SearchAntes.Length == 0)
                return false;
                
            DebugLogger.Log($"[CheckSingleClause] Checking antes: {string.Join(",", clause.SearchAntes)} (max: {maxSearchAnte})");
            
            // PERFORMANCE: Only create streams if we actually need them
            bool needsShopStream = clause.ItemTypeEnum == MotelyFilterItemType.Joker ||
                                   clause.ItemTypeEnum == MotelyFilterItemType.TarotCard ||
                                   clause.ItemTypeEnum == MotelyFilterItemType.PlanetCard;
                                   
            bool needsPackStream = clause.ItemTypeEnum == MotelyFilterItemType.Joker ||
                                   clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker ||
                                   clause.ItemTypeEnum == MotelyFilterItemType.TarotCard ||
                                   clause.ItemTypeEnum == MotelyFilterItemType.PlanetCard ||
                                   clause.ItemTypeEnum == MotelyFilterItemType.SpectralCard;
            
            foreach (var ante in clause.SearchAntes)
            {
                if (ante > maxSearchAnte) 
                {
                    DebugLogger.Log($"[CheckSingleClause] Skipping ante {ante} (> max {maxSearchAnte})");
                    continue;
                }
                
                DebugLogger.Log($"[CheckSingleClause] Checking ante {ante}...");
                bool found = false;
                
                // PERFORMANCE: Create streams only when needed
                var shopStream = needsShopStream ? ctx.CreateShopItemStream(ante, isCached: true) : default;
                var packStream = needsPackStream ? ctx.CreateBoosterPackStream(ante, isCached: true) : default;
                
                switch (clause.ItemTypeEnum)
                {
                    case MotelyFilterItemType.Joker:
                        found = CheckJoker(ref ctx, clause, ante, ref shopStream, ref packStream) > 0;
                        break;
                        
                    case MotelyFilterItemType.SoulJoker:
                        found = CheckSoulJoker(ref ctx, clause, ante, ref packStream) > 0;
                        break;
                        
                    case MotelyFilterItemType.TarotCard:
                        found = CheckTarot(ref ctx, clause, ante, ref shopStream, ref packStream);
                        break;
                        
                    case MotelyFilterItemType.PlanetCard:
                        found = CheckPlanet(ref ctx, clause, ante, ref shopStream, ref packStream);
                        break;
                        
                    case MotelyFilterItemType.SpectralCard:
                        found = CheckSpectral(ref ctx, clause, ante, ref packStream);
                        break;
                        
                    case MotelyFilterItemType.SmallBlindTag:
                    case MotelyFilterItemType.BigBlindTag:
                        found = CheckTagSingle(ref ctx, clause, ante);
                        break;
                        
                    case MotelyFilterItemType.Voucher:
                        found = CheckVoucherSingle(ref ctx, clause, ante);
                        break;
                        
                    case MotelyFilterItemType.PlayingCard:
                        found = CheckPlayingCard(ref ctx, clause, ante);
                        break;
                }
                
                // EARLY EXIT on first match
                if (found) return true;
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CheckJoker(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleShopItemStream shopStream, ref MotelySingleBoosterPackStream packStream)
        {
            // PERFORMANCE: Cache commonly used values
            bool searchAnyJoker = !clause.JokerEnum.HasValue;
            MotelyJoker targetJoker = searchAnyJoker ? MotelyJoker.Joker : clause.JokerEnum!.Value;
            int foundCount = 0;
            
            DebugLogger.Log($"[CheckJoker] Looking for {targetJoker} in ante {ante} SearchAnyJoker: {searchAnyJoker}");
                
            // Check shop
            bool shouldCheckShop = clause.Sources?.ShopSlots != null && clause.Sources.ShopSlots.Length > 0;
            
            if (shouldCheckShop)
            {
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                var shopSlots = clause.Sources?.ShopSlots;
                
                // PERFORMANCE: Use span for faster iteration if we have specific slots
                if (shopSlots != null && shopSlots.Length > 0)
                {
                    // Process items in slot order to minimize stream operations
                    for (int i = 0; i < maxSlots; i++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        
                        // Fast check using span
                        bool checkThisSlot = false;
                        for (int j = 0; j < shopSlots.Length; j++)
                        {
                            if (shopSlots[j] == i)
                            {
                                checkThisSlot = true;
                                break;
                            }
                        }
                        
                        if (!checkThisSlot) continue;
                        
                        if (item.TypeCategory == MotelyItemTypeCategory.Joker)
                        {
                            // Extract joker without edition bits for comparison
                            var shopJoker = (MotelyJoker)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);

                            // Check if we're looking for any joker or a specific one
                            bool jokerMatches = searchAnyJoker || shopJoker == targetJoker;

                            if (jokerMatches && CheckEditionAndStickers(item, clause))
                            {
                                foundCount++;
                                string jokerName = searchAnyJoker ? $"any joker ({shopJoker})" : targetJoker.ToString();
                                DebugLogger.Log($"[CheckJoker] Found {jokerName} in shop! Extracted: {shopJoker}, Total count: {foundCount}");
                            }
                        }
                    }
                }
                else
                {
                    // Check all slots
                    for (int i = 0; i < maxSlots; i++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        
                        if (item.TypeCategory == MotelyItemTypeCategory.Joker)
                        {
                            var shopJoker = (MotelyJoker)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                            bool jokerMatches = searchAnyJoker || shopJoker == targetJoker;
                            
                            if (jokerMatches && CheckEditionAndStickers(item, clause))
                            {
                                foundCount++;
                                DebugLogger.Log($"[CheckJoker] Found in shop!");
                            }
                        }
                    }
                }
            }
            
            // Check booster packs
            bool shouldCheckPacks = clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0;
            
            if (shouldCheckPacks)
            {
                DebugLogger.Log($"[CheckJoker] Checking booster packs in ante {ante}...");
                
                var packSlots = clause.Sources?.PackSlots;
                int packCount = ante == 1 ? 4 : 6;
                int buffoonPackIndex = 0;
                bool requireMega = clause.Sources?.RequireMega ?? false;
                
                DebugLogger.Log($"[CheckJoker] === BOOSTER PACK CONTENTS FOR ANTE {ante} ===");
                
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    DebugLogger.Log($"[CheckJoker] Pack #{i+1}: Type={pack.GetPackType()}, Size={pack.GetPackSize()}");
                    
                    // PERFORMANCE: Fast slot check
                    if (packSlots != null && packSlots.Length > 0)
                    {
                        bool checkThisPack = false;
                        for (int j = 0; j < packSlots.Length; j++)
                        {
                            if (packSlots[j] == i)
                            {
                                checkThisPack = true;
                                break;
                            }
                        }
                        if (!checkThisPack) continue;
                    }
                    
                    // Arcana packs don't contain regular jokers, only The Soul card can give soul jokers
                    if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        // If requireMega is set, skip non-mega buffoon packs
                        if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                        {
                            DebugLogger.Log($"[CheckJoker] Skipping non-mega Buffoon pack (requireMega=true)");
                            buffoonPackIndex++; // Still increment for proper stream tracking
                            continue;
                        }
                        
                        DebugLogger.Log($"[CheckJoker] Found Buffoon pack #{i+1}, checking for jokers...");

                        // Use the proper method to get Buffoon pack contents
                        // IMPORTANT: Pack number should be buffoonPackIndex (0-based) not i
                        var contents = ctx.GetNextBuffoonPackContents(ante, buffoonPackIndex, pack.GetPackSize());
                        buffoonPackIndex++; // Increment for next buffoon pack
                        
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
                    // Spectral packs don't contain regular jokers, only The Soul card can give soul jokers
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
            DebugLogger.Log($"[CheckJoker] Sources checked: Shop={(clause.Sources?.ShopSlots?.Length > 0)}, Packs={(clause.Sources?.PackSlots?.Length > 0)}, Tags={(clause.Sources?.Tags ?? false)}");
            return foundCount;
        }
        
        private static int CheckSoulJoker(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleBoosterPackStream packStream)
        {
            // Soul jokers only come from The Soul card in Arcana or Spectral packs
            // They are always legendary: Perkeo, Canio, Triboulet, Yorick, or Chicot
            
            bool searchAnySoulJoker = !clause.JokerEnum.HasValue;
            MotelyJoker? targetJoker = searchAnySoulJoker ? null : clause.JokerEnum!.Value;
            int foundCount = 0;
            
            string searchTarget = searchAnySoulJoker ? "any soul joker" : targetJoker.ToString()!;
            DebugLogger.Log($"[CheckSoulJoker] Looking for {searchTarget} in ante {ante}");
            
            // Soul jokers can only come from booster packs, not shop
            bool shouldCheckPacks = clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0;
            
            if (!shouldCheckPacks)
            {
                DebugLogger.Log($"[CheckSoulJoker] No pack slots to check, returning 0");
                return 0;
            }
            
            DebugLogger.Log($"[CheckSoulJoker] Checking booster packs in ante {ante}...");
            
            MotelySingleTarotStream tarotStream = default;
            MotelySingleSpectralStream spectralStream = default;
            MotelySingleJokerFixedRarityStream soulStream = default;
            bool tarotStreamInit = false;
            bool spectralStreamInit = false;
            bool soulStreamInit = false;
            
            int packCount = ante == 1 ? 4 : 6;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                DebugLogger.Log($"[CheckSoulJoker] Pack #{i+1}: Type={pack.GetPackType()}, Size={pack.GetPackSize()}");
                
                // If using granular sources, check if this pack slot is included
                if (clause.Sources != null && clause.Sources.PackSlots != null)
                {
                    if (!clause.Sources.PackSlots.Contains(i))
                    {
                        DebugLogger.Log($"[CheckSoulJoker] Skipping pack #{i+1} - not in packSlots filter");
                        continue;
                    }
                }
                
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    // If requireMega is set, skip non-mega arcana packs
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                    {
                        DebugLogger.Log($"[CheckSoulJoker] Skipping non-mega Arcana pack (requireMega=true)");
                        continue;
                    }
                    
                    DebugLogger.Log($"[CheckSoulJoker] Found Arcana pack #{i+1}, checking for The Soul...");
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
                    }
                    
                    var hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize());
                    DebugLogger.Log($"[CheckSoulJoker] Arcana pack has The Soul: {hasSoul}");
                    
                    if (hasSoul)
                    {
                        if (!soulStreamInit)
                        {
                            soulStreamInit = true;
                            soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
                        }
                        var soulJoker = ctx.GetNextJoker(ref soulStream);
                        DebugLogger.Log($"[CheckSoulJoker] The Soul gives: {soulJoker.Type}, Edition={soulJoker.Edition}");
                        
                        var soulJokerType = (MotelyJoker)(soulJoker.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        
                        if (searchAnySoulJoker || soulJokerType == targetJoker)
                        {
                            if (CheckEditionAndStickers(soulJoker, clause))
                            {
                                foundCount++;
                                DebugLogger.Log($"[CheckSoulJoker] Found {soulJokerType} from Arcana Soul! Total count: {foundCount}");
                            }
                        }
                    }
                }
                else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    // If requireMega is set, skip non-mega spectral packs
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                    {
                        DebugLogger.Log($"[CheckSoulJoker] Skipping non-mega Spectral pack (requireMega=true)");
                        continue;
                    }
                    
                    DebugLogger.Log($"[CheckSoulJoker] Found Spectral pack #{i+1}, checking for The Soul...");
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
                    }
                    
                    var hasSoul = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize());
                    DebugLogger.Log($"[CheckSoulJoker] Spectral pack has The Soul: {hasSoul}");
                    
                    if (hasSoul)
                    {
                        if (!soulStreamInit)
                        {
                            soulStreamInit = true;
                            soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
                        }
                        var soulJoker = ctx.GetNextJoker(ref soulStream);
                        DebugLogger.Log($"[CheckSoulJoker] The Soul gives: {soulJoker.Type}, Edition={soulJoker.Edition}");
                        
                        var soulJokerType = (MotelyJoker)(soulJoker.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        
                        if (searchAnySoulJoker || soulJokerType == targetJoker)
                        {
                            if (CheckEditionAndStickers(soulJoker, clause))
                            {
                                foundCount++;
                                DebugLogger.Log($"[CheckSoulJoker] Found {soulJokerType} from Spectral Soul! Total count: {foundCount}");
                            }
                        }
                    }
                }
            }
            
            DebugLogger.Log($"[CheckSoulJoker] === FINAL COUNT for {targetJoker} in ante {ante}: {foundCount} ===");
            return foundCount;
        }
        
        private static bool CheckTarot(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleShopItemStream shopStream, ref MotelySingleBoosterPackStream packStream)
        {
            // Assume validation was done during config parsing
            if (!clause.TarotEnum.HasValue)
                return false;
            
            var targetTarot = clause.TarotEnum.Value;
                
            // Check shop
            bool shouldCheckShop = clause.Sources?.ShopSlots != null && clause.Sources.ShopSlots.Length > 0;
            
            if (shouldCheckShop)
            {
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    
                    // If using granular sources, check if this slot is included
                    if (clause.Sources != null && clause.Sources.ShopSlots != null)
                    {
                        if (!clause.Sources.ShopSlots.Contains(i))
                            continue;
                    }
                    
                    if (item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                    {
                        var shopTarot = (MotelyTarotCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        if (shopTarot == targetTarot)
                            return true;
                    }
                }
            }
            
            // Check booster packs
            bool shouldCheckPacks = clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0;
            
            if (shouldCheckPacks)
            {
                int packCount = ante == 1 ? 4 : 6;
                
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    // If using granular sources, check if this pack slot is included
                    if (clause.Sources != null && clause.Sources.PackSlots != null)
                    {
                        if (!clause.Sources.PackSlots.Contains(i))
                            continue;
                    }
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        // If requireMega is set, skip non-mega arcana packs
                        if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                        {
                            DebugLogger.Log($"[CheckTarot] Skipping non-mega Arcana pack (requireMega=true)");
                            continue;
                        }
                        
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
        
        private static bool CheckPlanet(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleShopItemStream shopStream, ref MotelySingleBoosterPackStream packStream)
        {
            // Assume validation was done during config parsing
            if (!clause.PlanetEnum.HasValue)
                return false;
            
            var targetPlanet = clause.PlanetEnum.Value;
                
            // Check shop
            bool shouldCheckShop = clause.Sources?.ShopSlots != null && clause.Sources.ShopSlots.Length > 0;
            
            if (shouldCheckShop)
            {
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    
                    // If using granular sources, check if this slot is included
                    if (clause.Sources != null && clause.Sources.ShopSlots != null)
                    {
                        if (!clause.Sources.ShopSlots.Contains(i))
                            continue;
                    }
                    
                    if (item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                    {
                        var shopPlanet = (MotelyPlanetCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        if (shopPlanet == targetPlanet)
                            return true;
                    }
                }
            }
            
            // Check booster packs
            bool shouldCheckPacks = clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0;
            
            if (shouldCheckPacks)
            {
                int packCount = ante == 1 ? 4 : 6;
                
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    // If using granular sources, check if this pack slot is included
                    if (clause.Sources != null && clause.Sources.PackSlots != null)
                    {
                        if (!clause.Sources.PackSlots.Contains(i))
                            continue;
                    }
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                    {
                        // If requireMega is set, skip non-mega celestial packs
                        if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                        {
                            DebugLogger.Log($"[CheckPlanet] Skipping non-mega Celestial pack (requireMega=true)");
                            continue;
                        }
                        
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
        
        private static bool CheckSpectral(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleBoosterPackStream packStream)
        {
            DebugLogger.Log($"[CheckSpectral] Looking for spectral card: {clause.Value} in ante {ante}");
            
            // Check if searching for any spectral
            bool searchAnySpectral = clause.ItemTypeEnum == MotelyFilterItemType.SpectralCard && !clause.SpectralEnum.HasValue;
            
            var targetSpectral = searchAnySpectral ? (MotelySpectralCard?)null : clause.SpectralEnum!.Value;
            DebugLogger.Log($"[CheckSpectral] Search any spectral: {searchAnySpectral}, Target: {targetSpectral}");
                
            // Spectral cards appear only in packs, not in shop
            bool shouldCheckPacks = clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0;
            
            if (shouldCheckPacks)
            {
                DebugLogger.Log($"[CheckSpectral] Checking booster packs in ante {ante}...");
                
                // Create spectral stream ONCE outside the loop
                MotelySingleSpectralStream spectralStream = default;
                bool spectralStreamInit = false;
                
                // Check up to 6 packs available in the ante (ante 1 has 4, others have 6)
                int packCount = ante == 1 ? 4 : 6;
                DebugLogger.Log($"[CheckSpectral] Checking {packCount} packs...");
                
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    DebugLogger.Log($"[CheckSpectral] Pack #{i+1}: Type={pack.GetPackType()}, Size={pack.GetPackSize()}");
                    
                    // If using granular sources, check if this pack slot is included
                    if (clause.Sources != null && clause.Sources.PackSlots != null)
                    {
                        if (!clause.Sources.PackSlots.Contains(i))
                            continue;
                    }
                    
                    if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        // If requireMega is set, skip non-mega spectral packs
                        if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                        {
                            DebugLogger.Log($"[CheckSpectral] Skipping non-mega Spectral pack (requireMega=true)");
                            continue;
                        }
                        
                        DebugLogger.Log($"[CheckSpectral] Found Spectral pack! Getting contents...");
                        
                        // Only create stream if not already created
                        if (!spectralStreamInit)
                        {
                            spectralStreamInit = true;
                            spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                        }
                        
                        var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                        
                        DebugLogger.Log($"[CheckSpectral] Spectral pack contains {contents.Length} cards:");
                        
                        // Check each card in the pack
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents.GetItem(j);
                            var spectralType = (MotelySpectralCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                            DebugLogger.Log($"[CheckSpectral]   Card {j}: {spectralType} (raw: {item.Type}, value: {item.Value:X8}, enum: {(int)spectralType})");
                            
                            // If searching for any spectral, return true for any spectral card
                            if (searchAnySpectral)
                            {
                                DebugLogger.Log($"[CheckSpectral] FOUND any spectral: {spectralType}!");
                                return true;
                            }
                            
                            // Otherwise check for specific spectral
                            if (targetSpectral.HasValue && spectralType == targetSpectral.Value)
                            {
                                DebugLogger.Log($"[CheckSpectral] FOUND {targetSpectral}!");
                                return true;
                            }
                        }
                    }
                }
                // Don't print "no spectral pack found" if we already returned true
            }
            else
            {
                DebugLogger.Log($"[CheckSpectral] Not checking booster packs (no pack slots configured)");
            }
            
            return false;
        }
        
        private static bool CheckTagSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // Assume validation was done during config parsing
            if (!clause.TagEnum.HasValue)
                return false;
            
            var targetTag = clause.TagEnum.Value;
            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);
            
            // Use pre-parsed enum for type checking
            switch (clause.TagTypeEnum)
            {
                case MotelyTagType.SmallBlind:
                    return smallTag == targetTag;
                case MotelyTagType.BigBlind:
                    return bigTag == targetTag;
                default: // Any
                    return smallTag == targetTag || bigTag == targetTag;
            }
        }
        
        private static bool CheckVoucherSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // Assume validation was done during config parsing
            if (!clause.VoucherEnum.HasValue)
                return false;
                
            var voucher = ctx.GetAnteFirstVoucher(ante);
            return voucher == clause.VoucherEnum.Value;
        }
        
        private static bool CheckPlayingCard(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: true);
            int packCount = ante == 1 ? 4 : 6;
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                {
                    // If requireMega is set, skip non-mega standard packs
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                    {
                        DebugLogger.Log($"[CheckPlayingCard] Skipping non-mega Standard pack (requireMega=true)");
                        continue;
                    }
                    
                    var cardStream = ctx.CreateStandardPackCardStream(ante);
                    var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                    
                    // Check each card in the pack
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents.GetItem(j);
                        if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard)
                        {
                            DebugLogger.Log($"[CheckPlayingCard] Pack card: rank={item.PlayingCardRank}, suit={item.PlayingCardSuit}, seal={item.Seal}, enhancement={item.Enhancement}, edition={item.Edition}");
                            // For playing cards, we need to check the specific properties
                            
                            // Check suit if specified
                            if (clause.SuitEnum.HasValue && item.PlayingCardSuit != clause.SuitEnum.Value)
                                continue;
                            
                            // Check rank if specified
                            if (clause.RankEnum.HasValue && item.PlayingCardRank != clause.RankEnum.Value)
                                continue;
                            
                            // Check enhancement if specified
                            if (clause.EnhancementEnum.HasValue && item.Enhancement != clause.EnhancementEnum.Value)
                                continue;
                            
                            // Check seal if specified
                            if (clause.SealEnum.HasValue && item.Seal != clause.SealEnum.Value)
                                continue;
                            
                            // Check edition if specified
                            if (clause.EditionEnum.HasValue && item.Edition != clause.EditionEnum.Value)
                                continue;
                            
                            // If we get here, all specified criteria match
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckEditionAndStickers(in MotelyItem item, OuijaConfig.FilterItem clause)
        {
            // PERFORMANCE: Quick exit if no filters specified
            if (!clause.EditionEnum.HasValue && 
                (clause.StickerEnums == null || clause.StickerEnums.Count == 0))
                return true;
            
            // Check edition using pre-parsed enum
            if (clause.EditionEnum.HasValue && item.Edition != clause.EditionEnum.Value)
                return false;
            
            // Check stickers if specified
            if (clause.StickerEnums != null && clause.StickerEnums.Count > 0)
            {
                // PERFORMANCE: Unroll common cases
                int stickerCount = clause.StickerEnums.Count;
                if (stickerCount == 1)
                {
                    // Most common case - single sticker requirement
                    var sticker = clause.StickerEnums[0];
                    return sticker switch
                    {
                        MotelyJokerSticker.Eternal => item.IsEternal,
                        MotelyJokerSticker.Perishable => item.IsPerishable,
                        MotelyJokerSticker.Rental => item.IsRental,
                        _ => true
                    };
                }
                else
                {
                    // Multiple stickers - all must match
                    foreach (var requiredSticker in clause.StickerEnums)
                    {
                        switch (requiredSticker)
                        {
                            case MotelyJokerSticker.Eternal:
                                if (!item.IsEternal) return false;
                                break;
                            case MotelyJokerSticker.Perishable:
                                if (!item.IsPerishable) return false;
                                break;
                            case MotelyJokerSticker.Rental:
                                if (!item.IsRental) return false;
                                break;
                        }
                    }
                }
            }
            
            return true;
        }
        
    }
}