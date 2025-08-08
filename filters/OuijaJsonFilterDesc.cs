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
                        // Add occurrence count to details, not the score
                        scoreDetails.Add(occurrences);
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
            // PERFORMANCE: Try vectorized pre-filtering for jokers if looking for specific/rare items
            // BUT ONLY if we're searching shop slots (pack checking is too slow for pre-filter)
            bool hasPackSlots = clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Count() > 0;
            bool hasShopSlots = clause.Sources?.ShopSlots == null || clause.Sources.ShopSlots.Count() > 0;
            
            if (clause.ItemTypeEnum == MotelyFilterItemType.Joker && 
                !hasPackSlots && hasShopSlots && // Only pre-filter if ONLY checking shop
                (clause.JokerEnum.HasValue || 
                 clause.EditionEnum == MotelyItemEdition.Negative ||
                 clause.EditionEnum == MotelyItemEdition.Polychrome ||
                 IsRareJoker(clause.JokerEnum)))
            {
                var mask = TryVectorJokerPreFilter(ref ctx, clause);
                if (mask.IsAllFalse()) 
                {
                    DebugLogger.Log("[ProcessClause] Vector pre-filter eliminated all seeds!");
                    return mask; // Early exit - no seeds have this joker/edition
                }
                // Otherwise fall through to individual checking
            }
            
            // For non-vectorizable types or complex filtering, handle in individual search
            switch (clause.ItemTypeEnum)
            {
                case MotelyFilterItemType.Joker:
                case MotelyFilterItemType.SoulJoker:
                case MotelyFilterItemType.TarotCard:
                case MotelyFilterItemType.PlanetCard:
                case MotelyFilterItemType.SpectralCard:
                case MotelyFilterItemType.PlayingCard:
                    // These need individual checking for complex logic
                    return VectorMask.AllBitsSet;
                    
                case MotelyFilterItemType.SmallBlindTag:
                case MotelyFilterItemType.BigBlindTag:
                case MotelyFilterItemType.Voucher:
                    // These CAN be vectorized - process normally
                    break;
                    
                case MotelyFilterItemType.Boss:
                    // Boss blinds need individual checking
                    return VectorMask.AllBitsSet;
                    
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
                        
                    case MotelyFilterItemType.Boss:
                        // Boss blinds are not vectorized yet - return all true
                        anteMask = VectorMask.AllBitsSet;
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
                // Use isCached: false to ensure each clause gets a fresh stream
                var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
                
                // Debug shop stream for spectral cards
                if (clause.ItemTypeEnum == MotelyFilterItemType.SpectralCard && ctx.Deck == MotelyDeck.Ghost)
                {
                    DebugLogger.Log($"[CountOccurrences] Shop stream created: DoesProvideSpectrals = {shopStream.DoesProvideSpectrals}, SpectralRate = {shopStream.SpectralRate}");
                }
                
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
                        anteCount = CheckSpectral(ref ctx, clause, ante, ref shopStream, ref packStream) ? 1 : 0;
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
                        
                    case MotelyFilterItemType.Boss:
                        anteCount = CheckBoss(ref ctx, clause, ante) ? 1 : 0;
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
                                   clause.ItemTypeEnum == MotelyFilterItemType.PlanetCard ||
                                   clause.ItemTypeEnum == MotelyFilterItemType.SpectralCard;
                                   
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
                // Use isCached: false to ensure each clause gets a fresh stream
                var shopStream = needsShopStream ? ctx.CreateShopItemStream(ante, isCached: false) : default;
                var packStream = needsPackStream ? ctx.CreateBoosterPackStream(ante, isCached: false) : default;
                
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
                        found = CheckSpectral(ref ctx, clause, ante, ref shopStream, ref packStream);
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
                        
                    case MotelyFilterItemType.Boss:
                        found = CheckBoss(ref ctx, clause, ante);
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
                // Use the actual shopSlots array to allow unlimited rerolls
                var shopSlots = clause.Sources?.ShopSlots;
                int maxSlots = shopSlots != null && shopSlots.Length > 0 ? shopSlots.Max() + 1 : (ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots);
                
                // PERFORMANCE: Sort shop slots for early exit optimization
                if (shopSlots != null && shopSlots.Length > 0)
                {
                    // Convert to HashSet for O(1) lookups instead of O(n) array scan
                    var slotSet = new HashSet<int>(shopSlots);
                    int itemsGenerated = 0;
                    
                    // OPTIMIZATION 1: Check slots in order to allow early exit
                    // OPTIMIZATION 2: Only generate items up to highest requested slot
                    for (int i = 0; i < maxSlots; i++)
                    {
                        // Skip generation if this slot isn't requested
                        if (!slotSet.Contains(i))
                        {
                            // Still need to advance the stream to keep position correct
                            ctx.GetNextShopItem(ref shopStream);
                            continue;
                        }
                        
                        var item = ctx.GetNextShopItem(ref shopStream);
                        itemsGenerated++;
                        
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
                                DebugLogger.Log($"[CheckJoker] Found {jokerName} in shop slot {i}! Total count: {foundCount}");
                                
                                // TODO: Add early exit optimization when Min property is available
                            }
                        }
                    }
                }
                else
                {
                    // Check all slots with early exit optimization
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
                                DebugLogger.Log($"[CheckJoker] Found in shop slot {i}!");
                                
                                // TODO: Add early exit optimization when Min property is available
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
                // Use the actual shopSlots array to allow unlimited rerolls
                var shopSlots = clause.Sources?.ShopSlots;
                int maxSlots = shopSlots != null && shopSlots.Length > 0 ? 
                    shopSlots.Max() + 1 : 
                    (ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots);
                
                // PERFORMANCE: Use HashSet for O(1) slot lookups
                var slotSet = shopSlots != null ? new HashSet<int>(shopSlots) : null;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    // Skip slots not in our search set
                    if (slotSet != null && !slotSet.Contains(i))
                    {
                        ctx.GetNextShopItem(ref shopStream); // Still advance stream
                        continue;
                    }
                    
                    var item = ctx.GetNextShopItem(ref shopStream);
                    
                    if (item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                    {
                        var shopTarot = (MotelyTarotCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        if (shopTarot == targetTarot)
                        {
                            DebugLogger.Log($"[CheckTarot] Found {targetTarot} in shop slot {i}!");
                            return true; // Early exit on first match
                        }
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
                // Use the actual shopSlots array to allow unlimited rerolls
                var shopSlots = clause.Sources?.ShopSlots;
                int maxSlots = shopSlots != null && shopSlots.Length > 0 ? 
                    shopSlots.Max() + 1 : 
                    (ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots);
                
                // PERFORMANCE: Use HashSet for O(1) slot lookups
                var slotSet = shopSlots != null ? new HashSet<int>(shopSlots) : null;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    // Skip slots not in our search set
                    if (slotSet != null && !slotSet.Contains(i))
                    {
                        ctx.GetNextShopItem(ref shopStream); // Still advance stream
                        continue;
                    }
                    
                    var item = ctx.GetNextShopItem(ref shopStream);
                    
                    if (item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                    {
                        var shopPlanet = (MotelyPlanetCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        if (shopPlanet == targetPlanet)
                        {
                            DebugLogger.Log($"[CheckPlanet] Found {targetPlanet} in shop slot {i}!");
                            return true; // Early exit on first match
                        }
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
        
        private static bool CheckSpectral(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleShopItemStream shopStream, ref MotelySingleBoosterPackStream packStream)
        {
            DebugLogger.Log($"[CheckSpectral] Looking for spectral card: {clause.Value} in ante {ante}");
            
            // Check if searching for any spectral
            bool searchAnySpectral = clause.ItemTypeEnum == MotelyFilterItemType.SpectralCard && !clause.SpectralEnum.HasValue;
            
            var targetSpectral = searchAnySpectral ? (MotelySpectralCard?)null : clause.SpectralEnum!.Value;
            DebugLogger.Log($"[CheckSpectral] Search any spectral: {searchAnySpectral}, Target: {targetSpectral}");
            
            // Check shop (only for Ghost Deck)
            bool shouldCheckShop = clause.Sources?.ShopSlots != null && clause.Sources.ShopSlots.Length > 0;
            
            DebugLogger.Log($"[CheckSpectral] Sources: {(clause.Sources != null ? "exists" : "null")}");
            if (clause.Sources != null)
            {
                DebugLogger.Log($"[CheckSpectral] ShopSlots: {(clause.Sources.ShopSlots != null ? string.Join(",", clause.Sources.ShopSlots) : "null")}");
                DebugLogger.Log($"[CheckSpectral] PackSlots: {(clause.Sources.PackSlots != null ? string.Join(",", clause.Sources.PackSlots) : "null")}");
            }
            
            if (shouldCheckShop)
            {
                DebugLogger.Log($"[CheckSpectral] Checking shop slots in ante {ante}...");
                DebugLogger.Log($"[CheckSpectral] Context deck: {ctx.Deck}");
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                for (int i = 0; i < maxSlots; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    DebugLogger.Log($"[CheckSpectral] Shop slot {i}: {item.TypeCategory} - {item.Type}");
                    
                    // If using granular sources, check if this slot is included
                    if (clause.Sources != null && clause.Sources.ShopSlots != null)
                    {
                        if (!clause.Sources.ShopSlots.Contains(i))
                        {
                            DebugLogger.Log($"[CheckSpectral] Skipping slot {i} - not in allowed slots");
                            continue;
                        }
                    }
                    
                    if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                    {
                        var shopSpectral = (MotelySpectralCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                        DebugLogger.Log($"[CheckSpectral] Shop slot {i}: Found spectral {shopSpectral} (looking for {targetSpectral})");
                        
                        // If searching for any spectral, return true for any spectral card
                        if (searchAnySpectral)
                        {
                            DebugLogger.Log($"[CheckSpectral] FOUND any spectral in shop slot {i}: {shopSpectral}!");
                            return true;
                        }
                        
                        // Otherwise check for specific spectral
                        if (targetSpectral.HasValue && shopSpectral == targetSpectral.Value)
                        {
                            DebugLogger.Log($"[CheckSpectral] FOUND {targetSpectral} in shop slot {i}!");
                            return true;
                        }
                    }
                }
            }
                
            // Check packs
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
            
            // Check shop for spectral cards (Ghost Deck only)
            if (shouldCheckShop)
            {
                DebugLogger.Log($"[CheckSpectral] Checking shop for spectral cards (Ghost Deck)...");
                DebugLogger.Log($"[CheckSpectral] Deck = {ctx.Deck}, Should have spectral stream = {ctx.Deck == MotelyDeck.Ghost}");
                DebugLogger.Log($"[CheckSpectral] Note: Spectral cards have ~6.67% chance per shop slot on Ghost Deck (2/30 rate)");
                
                var shopSlots = clause.Sources?.ShopSlots;
                // ALWAYS iterate through all shop slots to keep stream in sync
                int maxSlots = ante == 1 ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
                
                DebugLogger.Log($"[CheckSpectral] Shop slots requested: {(shopSlots != null ? string.Join(",", shopSlots) : "all")}, iterating through {maxSlots} slots");
                DebugLogger.Log($"[CheckSpectral] Shop stream info: DoesProvideSpectrals={shopStream.DoesProvideSpectrals}, SpectralRate={shopStream.SpectralRate}, TotalRate={shopStream.TotalRate}");
                
                // Shop stream is already provided as parameter
                
                if (shopSlots != null && shopSlots.Length > 0)
                {
                    var slotSet = new HashSet<int>(shopSlots);
                    
                    for (int i = 0; i < maxSlots; i++)
                    {
                        // Skip if this slot isn't requested
                        if (!slotSet.Contains(i))
                        {
                            // Still need to advance the stream to keep position correct
                            ctx.GetNextShopItem(ref shopStream);
                            continue;
                        }
                        
                        var item = ctx.GetNextShopItem(ref shopStream);
                        
                        // Debug: Log what's actually in this slot
                        if (DebugLogger.IsEnabled)
                        {
                            string itemType = item.TypeCategory switch
                            {
                                MotelyItemTypeCategory.Joker => "Joker",
                                MotelyItemTypeCategory.TarotCard => "Tarot",
                                MotelyItemTypeCategory.PlanetCard => "Planet",
                                MotelyItemTypeCategory.SpectralCard => "SPECTRAL",
                                MotelyItemTypeCategory.PlayingCard => "Playing Card",
                                MotelyItemTypeCategory.Invalid => $"Invalid({item.Type})",
                                _ => $"Unknown({item.TypeCategory})"
                            };
                            DebugLogger.Log($"[CheckSpectral] Shop slot {i}: {itemType}");
                        }
                        
                        if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                        {
                            // Extract spectral card type
                            var shopSpectral = (MotelySpectralCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                            DebugLogger.Log($"[CheckSpectral] Found spectral card in shop slot {i}: {shopSpectral}");
                            
                            // Check if we're looking for any spectral or a specific one
                            if (searchAnySpectral || shopSpectral == targetSpectral)
                            {
                                DebugLogger.Log($"[CheckSpectral] MATCH! Found {(searchAnySpectral ? "any spectral" : targetSpectral.ToString())} in shop!");
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    // Check all default shop slots
                    for (int i = 0; i < maxSlots; i++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        
                        // Debug for all slots when no specific slots requested
                        if (DebugLogger.IsEnabled)
                        {
                            string itemType = item.TypeCategory switch
                            {
                                MotelyItemTypeCategory.Joker => "Joker",
                                MotelyItemTypeCategory.TarotCard => "Tarot",
                                MotelyItemTypeCategory.PlanetCard => "Planet",
                                MotelyItemTypeCategory.SpectralCard => "SPECTRAL",
                                MotelyItemTypeCategory.PlayingCard => "Playing Card",
                                MotelyItemTypeCategory.Invalid => $"Invalid({item.Type})",
                                _ => $"Unknown({item.TypeCategory})"
                            };
                            DebugLogger.Log($"[CheckSpectral] Shop slot {i}: {itemType}");
                        }
                        
                        if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                        {
                            var shopSpectral = (MotelySpectralCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                            DebugLogger.Log($"[CheckSpectral] Found spectral card in shop slot {i}: {shopSpectral}");
                            
                            if (searchAnySpectral || shopSpectral == targetSpectral)
                            {
                                DebugLogger.Log($"[CheckSpectral] MATCH! Found {(searchAnySpectral ? "any spectral" : targetSpectral.ToString())} in shop!");
                                return true;
                            }
                        }
                    }
                }
                
                DebugLogger.Log($"[CheckSpectral] No matching spectral cards found in shop");
            }
            else if (ctx.Deck == MotelyDeck.Ghost)
            {
                DebugLogger.Log($"[CheckSpectral] Ghost Deck detected but no shop slots configured - spectral cards CAN appear in shop!");
            }
            
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRareJoker(MotelyJoker? joker)
        {
            if (!joker.HasValue) return false;
            
            // Check if it's a legendary or rare joker
            var value = (int)joker.Value;
            var rarity = (MotelyJokerRarity)(value & 0xFF00);
            
            return rarity == MotelyJokerRarity.Legendary || 
                   rarity == MotelyJokerRarity.Rare;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask TryVectorJokerPreFilter(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            DebugLogger.Log($"[VectorPreFilter] Starting pre-filter for joker: {clause.JokerEnum}, edition: {clause.EditionEnum}");
            VectorMask resultMask = VectorMask.AllBitsClear;
            
            // Check each ante for the joker/edition
            foreach (var ante in clause.SearchAntes)
            {
                // Create joker stream for this ante
                var jokerStream = ctx.CreateShopJokerStream(ante, MotelyJokerStreamFlags.Default, isCached: true);
                
                // Sample a few jokers to see if ANY match our criteria
                // This is a quick pre-filter - if we don't find it in first 10-20 jokers, seed likely doesn't have it
                const int SAMPLE_COUNT = 15; // Check first 15 jokers (covers most of shop + some packs)
                
                VectorMask anteMask = VectorMask.AllBitsClear;
                
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    var joker = ctx.GetNextJoker(ref jokerStream);
                    
                    // Check if this joker matches our criteria
                    VectorMask matches = VectorMask.AllBitsSet;
                    
                    // Check joker type if specified
                    if (clause.JokerEnum.HasValue)
                    {
                        // CRITICAL FIX: Only compare the joker type, not edition/stickers!
                        // Mask out everything except the joker type bits
                        var targetJoker = new MotelyItemVector(new MotelyItem((int)MotelyItemTypeCategory.Joker | (int)clause.JokerEnum.Value));
                        var jokerTypeMask = Vector256.Create(Motely.ItemTypeMask);
                        
                        // Compare only the type bits
                        matches &= Vector256.Equals(
                            Vector256.BitwiseAnd(joker.Value, jokerTypeMask),
                            Vector256.BitwiseAnd(targetJoker.Value, jokerTypeMask)
                        );
                    }
                    
                    // Check edition if specified (especially good for Negative!)
                    if (clause.EditionEnum.HasValue)
                    {
                        matches &= VectorEnum256.Equals(joker.Edition, clause.EditionEnum.Value);
                    }
                    
                    // Check stickers
                    if (clause.StickerEnums != null && clause.StickerEnums.Count > 0)
                    {
                        foreach (var sticker in clause.StickerEnums)
                        {
                            switch (sticker)
                            {
                                case MotelyJokerSticker.Eternal:
                                    matches &= joker.IsEternal;
                                    break;
                                case MotelyJokerSticker.Perishable:
                                    matches &= joker.IsPerishable;
                                    break;
                                case MotelyJokerSticker.Rental:
                                    matches &= joker.IsRental;
                                    break;
                            }
                        }
                    }
                    
                    // If ANY seed has this joker, mark it
                    if (!matches.IsAllFalse())
                    {
                        DebugLogger.Log($"[VectorPreFilter] Found matching joker in ante {ante}, iteration {i}!");
                    }
                    anteMask |= matches;
                    
                    // OPTIMIZATION: If all seeds already found it, stop checking
                    if (anteMask.IsAllTrue())
                        break;
                }
                
                resultMask |= anteMask;
            }
            
            DebugLogger.Log($"[VectorJokerPreFilter] Result: {resultMask}");
            return resultMask;
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
            if (clause.EditionEnum.HasValue)
            {
                DebugLogger.Log($"[CheckEditionAndStickers] Checking edition: item.Edition={item.Edition}, clause.EditionEnum={clause.EditionEnum.Value}, match={item.Edition == clause.EditionEnum.Value}");
                if (item.Edition != clause.EditionEnum.Value)
                    return false;
            }
            
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckBoss(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // Boss blinds appear in EVERY ante (after small and big blinds)
            if (!clause.BossEnum.HasValue)
                return false;
            
            var targetBoss = clause.BossEnum.Value;
            
            // Get the boss for this ante
            var actualBoss = GetBossForAnte(ref ctx, ante);
            
            DebugLogger.Log($"[CheckBoss] Ante {ante}: Looking for {targetBoss}, found {actualBoss}");
            
            return actualBoss == targetBoss;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MotelyBossBlind GetBossForAnte(ref MotelySingleSearchContext ctx, int ante)
        {
            // Use the proper boss generation method that tracks locked bosses
            return ctx.GetBossForAnte(ante);
        }
        
    }
}