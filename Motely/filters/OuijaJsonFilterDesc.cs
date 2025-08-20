using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    public bool AutoCutoff { get; set; }
    
    // Static callback for when results are found
    public static Action<string, int, int[]>? OnResultFound { get; set; }
    // Global prefilter toggle (enabled via --prefilter CLI flag). Default: false
    public static bool PrefilterEnabled = false;

    public OuijaJsonFilterDesc(OuijaConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Cutoff = 0; // Set by command line --cutoff parameter
        AutoCutoff = false;
    }

    // CENTRAL CONFIG VALIDATION
    // Throws (YEET) on structurally invalid clauses so runtime paths can assume invariants.
    private static void ValidateConfig(OuijaConfig cfg)
    {
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));

        void ValidateList(List<OuijaConfig.FilterItem>? list, string kind)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var fi = list[i];
                // EffectiveAntes must be parsed & non-empty
                if (fi.EffectiveAntes == null || fi.EffectiveAntes.Length == 0)
                    throw new ArgumentException($"[{kind}][{i}] '{fi.Type}:{fi.Value}' missing 'antes' – provide at least one ante.");

                // Joker / SoulJoker must specify at least one source domain (shop or packs)
                if ((fi.ItemTypeEnum == MotelyFilterItemType.Joker || fi.ItemTypeEnum == MotelyFilterItemType.SoulJoker) &&
                    (fi.Sources == null || ((fi.Sources.ShopSlots == null || fi.Sources.ShopSlots.Length == 0) && (fi.Sources.PackSlots == null || fi.Sources.PackSlots.Length == 0))))
                    throw new ArgumentException($"[{kind}][{i}] Joker/SoulJoker '{fi.Type}:{fi.Value}' requires 'shopSlots' or 'packSlots'.");

                // WARN (not error): out-of-baseline pack slot usage (ante1 >4, others >6) – user may intentionally extend
                if (fi.Sources?.PackSlots is { Length: > 0 })
                {
                    int maxPack = fi.Sources.PackSlots.Max();
                    int declaredCount = maxPack + 1;
                    foreach (var ante in fi.EffectiveAntes)
                    {
                        if (ante == 1 && declaredCount > 4)
                            DebugLogger.Log($"[WARNING] [{kind}][{i}] '{fi.Type}:{fi.Value}' requests {declaredCount} pack slots in ante 1 (>4 baseline). Proceeding.");
                        else if (ante != 1 && declaredCount > 6)
                            DebugLogger.Log($"[WARNING] [{kind}][{i}] '{fi.Type}:{fi.Value}' requests {declaredCount} pack slots in ante {ante} (>6 baseline). Proceeding.");
                    }
                }
            }
        }

        ValidateList(cfg.Must, "must");
        ValidateList(cfg.Should, "should");
        ValidateList(cfg.MustNot, "mustNot");
    }

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        try
        {
            DebugLogger.Log("[OuijaJsonFilterDesc] CreateFilter called!");
            DebugLogger.Log($"[OuijaJsonFilterDesc] Config has {Config.Must?.Count ?? 0} MUST clauses");
            
            // Cache streams for all antes we need
            var allAntes = new HashSet<int>();
            
            var config = Config; // Capture for lambda
            Action<List<OuijaConfig.FilterItem>> collectAntes = (items) =>
            {
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        try
                        {
                            foreach (var ante in item.EffectiveAntes)
                                allAntes.Add(ante);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[OuijaJsonFilterDesc] Error getting EffectiveAntes: {ex.Message}");
                        }
                    }
                }
            };
            
            if (Config.Must != null)
                collectAntes(Config.Must);
            if (Config.Should != null)
                collectAntes(Config.Should);
            if (Config.MustNot != null)
                collectAntes(Config.MustNot);
        
            foreach (var ante in allAntes)
            {
                ctx.CacheShopStream(ante);
                ctx.CacheBoosterPackStream(ante);
                ctx.CacheTagStream(ante);
                ctx.CacheVoucherStream(ante);
                ctx.CacheSoulJokerStream(ante);
            }
            
            return new OuijaJsonFilter(Config, Cutoff, AutoCutoff);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[OuijaJsonFilterDesc] CreateFilter EXCEPTION: {ex}");
            throw;
        }
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        private readonly OuijaConfig _config;
        private readonly int _cutoff;
        private readonly bool _autoCutoff;
            // Derived per-ante slot loop bounds (computed from config so we don't guess).
            // Index = ante number. Value = (max referenced slot index + 1) across ALL clauses for that ante.
            // If zero => no clauses referenced any specific slot for that ante; we fall back to a conservative global max.
            private readonly int[] _maxShopSlotsPerAnte; // for shop jokers
            private readonly int[] _maxPackSlotsPerAnte; // for booster packs
        
        public static bool IsCancelled = false;
        
        // Auto-cutoff tracking - simple highest score seen
        private static int _currentCutoff = 0;
        private static int _highestScoreSeen = 0;
        private static int _resultsFound = 0;
        private static long _autoCutoffStartTicks = 0;
        private static bool _autoCutoffExpired = false;
        
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

        public OuijaJsonFilter(OuijaConfig config, int cutoff, bool autoCutoff = false)
        {
            DebugLogger.Log("[OuijaJsonFilter] Constructor called!");
            DebugLogger.Log($"[OuijaJsonFilter] Must clauses: {config.Must?.Count ?? 0}");
            DebugLogger.Log($"[OuijaJsonFilter] Should clauses: {config.Should?.Count ?? 0}");
            DebugLogger.Log($"[OuijaJsonFilter] MustNot clauses: {config.MustNot?.Count ?? 0}");
            DebugLogger.Log($"[OuijaJsonFilter] Cutoff: {cutoff}, AutoCutoff: {autoCutoff}");
            
            // Log the first few clauses for debugging
            if (config.Must != null && config.Must.Count > 0)
            {
                DebugLogger.Log($"[OuijaJsonFilter] First MUST clause: Type={config.Must[0].Type}, Value={config.Must[0].Value}");
            }
            if (config.Should != null && config.Should.Count > 0)
            {
                DebugLogger.Log($"[OuijaJsonFilter] First SHOULD clause: Type={config.Should[0].Type}, Value={config.Should[0].Value}, Score={config.Should[0].Score}");
            }
            
            // PATCH: If SHOULD is empty, copy MUST clauses to SHOULD for scoring
            // Otherwise seeds match MUST but have no score and get filtered out!
            if ((config.Should == null || config.Should.Count == 0) && config.Must != null && config.Must.Count > 0)
            {
                DebugLogger.Log($"[OuijaJsonFilter] PATCH: SHOULD is empty, copying {config.Must.Count} MUST clauses to SHOULD for scoring!");
                config.Should = new List<OuijaConfig.FilterItem>(config.Must);
            }
            
            _config = config;
            _cutoff = cutoff;
            _autoCutoff = autoCutoff;

            // Build per-ante SLOT MAXIMA.
            // SHOP: derived from highest referenced shop slot index per ante (user can exceed defaults intentionally).
            // PACK: fixed Balatro rule per user directive: ante 1 => 4 Buffoon pack "slots", all other antes => 6.
            // We retain arrays for structural consistency; pack maxima are pre-filled constants.
            const int GlobalFallbackShopSlots = 6; // used only when NO explicit shop slots referenced for an ante
            const int PackSlotsAnte1 = 4;
            const int PackSlotsOtherAntes = 6;
            _maxShopSlotsPerAnte = new int[16];
            _maxPackSlotsPerAnte = new int[16];
            for (int i = 0; i < _maxPackSlotsPerAnte.Length; i++)
                _maxPackSlotsPerAnte[i] = (i == 1) ? PackSlotsAnte1 : PackSlotsOtherAntes;

            // Copy refs to avoid struct 'this' capture issue inside local functions
            var shopMaxRef = _maxShopSlotsPerAnte;
            void Accumulate(OuijaConfig.FilterItem fi)
            {
                if (fi.Sources?.ShopSlots is { Length: > 0 })
                {
                    int maxIdx = -1;
                    foreach (var s in fi.Sources.ShopSlots)
                        if (s >= 0 && s < 32 && s > maxIdx) maxIdx = s;
                    if (maxIdx >= 0)
                    {
                        int needed = maxIdx + 1;
                        foreach (var ante in fi.EffectiveAntes)
                        {
                            if ((uint)ante < (uint)shopMaxRef.Length && needed > shopMaxRef[ante])
                                shopMaxRef[ante] = needed;
                        }
                    }
                }
            }
            void AccumulateList(List<OuijaConfig.FilterItem>? list)
            {
                if (list == null) return;
                foreach (var fi in list) Accumulate(fi);
            }
            AccumulateList(config.Must);
            AccumulateList(config.Should);
            AccumulateList(config.MustNot);

            // Ensure any ante that had zero explicit references still has fallback bounds
            for (int i = 0; i < _maxShopSlotsPerAnte.Length; i++)
            {
                if (_maxShopSlotsPerAnte[i] == 0) _maxShopSlotsPerAnte[i] = GlobalFallbackShopSlots;
                // Pack array already pre-filled with constants; no fallback needed.
            }
            if (autoCutoff)
            {
                _currentCutoff = 0;
                _highestScoreSeen = 0;
                _resultsFound = 0;
                _autoCutoffStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                _autoCutoffExpired = false;
                DebugLogger.Log("[OuijaJsonFilter] Auto-cutoff initialized, will expire in 10 seconds");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            DebugLogger.Log("[OuijaJsonFilter] Filter method called!");
            VectorMask mask = VectorMask.AllBitsSet;

            // Create shared voucher state for tracking activations across all voucher clauses
            MotelyVectorRunStateVoucher sharedVoucherState = new();

            // Optional ultra-early pre-filters (enabled only with --prefilter) for very restrictive MUST clauses.
            // Currently targets SoulJoker clauses specifying a concrete Joker (and optional Edition).
            // Safe: only eliminates seeds whose FIRST soul legendary cannot satisfy the clause.
            if (PrefilterEnabled)
                mask &= HandlePreFilters(ref searchContext);
            if (mask.IsAllFalse())
            {
                DebugLogger.Log("[PreFilters] All seeds eliminated by pre-filters – aborting early.");
                return mask;
            }
            
            // UNIFIED VOUCHER PROCESSING: Process all voucher clauses together sequentially from ante 1-8
            mask &= ProcessAllVoucherClausesUnified(ref searchContext, mask, ref sharedVoucherState);
            if (mask.IsAllFalse())
            {
                DebugLogger.Log("[Filter] Early exit - all seeds eliminated after unified voucher processing");
                return mask;
            }
            
            // Order non-voucher MUST clauses by heuristic cost (cheap & selective first)
            var sortedMust = _config.Must.Where(c => c.ItemTypeEnum != MotelyFilterItemType.Voucher).OrderBy(GetClauseCost);
            
            // Process non-voucher MUST clauses - all must match (vouchers already processed above)
            foreach (var must in sortedMust)
            {
                DebugLogger.Log($"[Filter] Processing MUST clause: {must.Type} = {must.Value}");
                
                // Use vectorization for EVERYTHING that Motely supports!
                bool canVectorize = must.ItemTypeEnum switch
                {
                    MotelyFilterItemType.SmallBlindTag => true,
                    MotelyFilterItemType.BigBlindTag => true,
                    MotelyFilterItemType.Voucher => false, // Already processed in unified voucher processing
                    MotelyFilterItemType.Joker => true,      // Vectorized via joker stream
                    MotelyFilterItemType.SoulJoker => true,  // We have CheckSoulJokerVector()
                    MotelyFilterItemType.TarotCard => true,  // Can be vectorized for simple checks
                    MotelyFilterItemType.PlanetCard => true,  // Can be vectorized for simple checks
                    MotelyFilterItemType.SpectralCard => true,  // Can be vectorized for simple checks
                    MotelyFilterItemType.PlayingCard => false,  // Complex multi-attribute matching
                    MotelyFilterItemType.Boss => false,  // Not vectorized in Motely
                    _ => false
                };
                
                if (canVectorize)
                {
                    // Pass current mask so clause evaluation can skip already dead lanes
                    var clauseMask = ProcessClause(ref searchContext, must, true, mask, ref sharedVoucherState);
                    mask &= clauseMask;
                    
                    // EARLY EXIT: If no seeds remain, stop processing immediately
                    if (mask.IsAllFalse()) 
                    {
                        DebugLogger.Log("[Filter] Early exit - all seeds eliminated after MUST clause");
                        return mask;
                    }
                }
                // Non-vectorizable MUST clauses will be checked in individual seed processing
            }
            
            // Process non-voucher MUST NOT clauses - none can match (vouchers already processed above)
            foreach (var mustNot in _config.MustNot.Where(c => c.ItemTypeEnum != MotelyFilterItemType.Voucher))
            {
                DebugLogger.Log($"[Filter] Processing MUST NOT clause: {mustNot.Type} = {mustNot.Value}");
                
                // Use vectorization for EVERYTHING that Motely supports!
                bool canVectorize = mustNot.ItemTypeEnum switch
                {
                    MotelyFilterItemType.SmallBlindTag => true,
                    MotelyFilterItemType.BigBlindTag => true,
                    MotelyFilterItemType.Voucher => false, // Already processed in unified voucher processing
                    MotelyFilterItemType.Joker => true,
                    MotelyFilterItemType.SoulJoker => true,
                    MotelyFilterItemType.TarotCard => true,
                    MotelyFilterItemType.PlanetCard => true,
                    MotelyFilterItemType.SpectralCard => true,
                    MotelyFilterItemType.PlayingCard => false,
                    MotelyFilterItemType.Boss => false,
                    _ => false
                };
                
                if (canVectorize)
                {
                    var clauseMask = ProcessClause(ref searchContext, mustNot, true, mask, ref sharedVoucherState);
                    mask &= clauseMask ^ VectorMask.AllBitsSet; // invert only surviving lanes of this clause
                    
                    // EARLY EXIT: If no seeds remain, stop processing immediately
                    if (mask.IsAllFalse()) 
                    {
                        DebugLogger.Log("[Filter] Early exit - all seeds eliminated after MUST NOT clause");
                        return mask;
                    }
                }
                // Non-vectorizable MUST NOT clauses will be checked in individual seed processing
            }
            
            // Process SHOULD clauses for scoring
            var config = _config; // Capture for lambda
            var cutoff = _cutoff; // Capture for lambda
            var autoCutoff = _autoCutoff; // Capture for lambda
            
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                // EARLY EXIT: Check cancellation first
                if (IsCancelled) return false;
                
                var currentSeed = singleCtx.GetSeed();
                DebugLogger.Log($"[SEARCH] ========== CHECKING SEED: {currentSeed} ==========");
                
                // Create voucher state for this seed that persists across all checks
                MotelyRunState voucherState = new();

                // EARLY STATE ACQUISITION: Activate prerequisite vouchers and Showman (if explicitly required)
                // This runs once per seed before clause-by-clause processing so later checks can rely on runState.
                // Voucher clauses are now processed first in MUST clause ordering
                // Showman activation happens during individual joker clause processing

                // PERFORMANCE: Check MUST clauses first - exit immediately on failure
                // This avoids wasting time on expensive operations for seeds that don't match
                foreach (var must in config.Must)
                {
                    string searchDesc = GetFilterDescription(must);
                    DebugLogger.Log($"[SEARCH] Looking for: {searchDesc}");
                    if (!CheckSingleClause(ref singleCtx, must, ref voucherState))
                    {
                        DebugLogger.Log($"[SEARCH] NOT FOUND - exiting early!");
                        return false; // EARLY EXIT - no point checking other MUST clauses
                    }
                    DebugLogger.Log($"[SEARCH] FOUND!");
                }
                
                // Check MUST NOT clauses - none can match
                foreach (var mustNot in config.MustNot)
                {
                    if (CheckSingleClause(ref singleCtx, mustNot, ref voucherState))
                    {
                        DebugLogger.Log($"[SEARCH] Found MUST NOT item - exiting!");
                        return false; // EARLY EXIT
                    }
                }
                
                // Start with 1 point for passing all MUST clauses
                int totalScore = 1;
                // Pre-allocate based on SHOULD clause count
                var scoreDetails = new List<int>(config.Should.Count);

                // Calculate scores from SHOULD clauses
                foreach (var should in config.Should)
                {
                    DebugLogger.Log($"[Filter] Seed {currentSeed}: Checking SHOULD clause: {should.Type} = {should.Value}");
                    int occurrences = CountOccurrences(ref singleCtx, should, ref voucherState);
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
                
                // Simple auto-cutoff: if enabled, track highest score and use it as cutoff
                if (autoCutoff && totalScore > 0 && !_autoCutoffExpired)
                {
                    // Check if 10 seconds have passed (10 seconds = 10 * Stopwatch.Frequency ticks)
                    if (!_autoCutoffExpired && (System.Diagnostics.Stopwatch.GetTimestamp() - _autoCutoffStartTicks) > (10 * System.Diagnostics.Stopwatch.Frequency))
                    {
                        _autoCutoffExpired = true;
                        DebugLogger.Log($"[OuijaJsonFilter] Auto-cutoff expired after 10 seconds. Final cutoff: {_highestScoreSeen}");
                    }
                    
                    if (!_autoCutoffExpired)
                    {
                        // First few results, let them through to establish baseline
                        if (_resultsFound < 10)
                        {
                            _resultsFound++;
                            if (totalScore > _highestScoreSeen)
                            {
                                _highestScoreSeen = totalScore;
                            }
                            // Let first 10 results through to establish range
                            var seed = singleCtx.GetSeed();
                            var scores = scoreDetails.ToArray();
                            OnResultFound?.Invoke(seed, totalScore, scores);
                            return true;
                        }
                        
                        // After 10 results, only accept if equal to or better than highest
                        if (totalScore >= _highestScoreSeen)
                        {
                            _highestScoreSeen = totalScore;
                            _currentCutoff = totalScore;
                            
                            var seed = singleCtx.GetSeed();
                            var scores = scoreDetails.ToArray();
                            OnResultFound?.Invoke(seed, totalScore, scores);
                            return true;
                        }
                    }
                }
                else if ((!autoCutoff && totalScore >= cutoff) || 
                         (autoCutoff && _autoCutoffExpired && totalScore >= _highestScoreSeen))
                {
                    // Fixed cutoff mode
                    var seed = singleCtx.GetSeed();
                    var scores = scoreDetails.ToArray();
                    DebugLogger.Log($"[RESULT] FOUND MATCHING SEED: {seed}, Score: {totalScore}, Cutoff: {cutoff}");
                    OnResultFound?.Invoke(seed, totalScore, scores);
                    
                    return true;
                }
                
                return false;
            });
        }

        // Early activation pass: process (cheap) voucher prerequisites and Showman joker before full MUST evaluation
        // Order: vouchers -> (blind) tags (no state) -> Showman. Only activates state; does NOT eliminate seeds.
        // Deleted EarlyActivateVouchersAndShowman - vouchers now processed in proper order
        // Showman activation happens during individual joker clause processing

        // PRE-FILTER (soul joker): cheap identity (and optional edition) check on FIRST soul legendary per seed.
        // Rationale: For a specific soul legendary (e.g. Perkeo) you can reject all seeds whose first soul legendary
        // is different without simulating any pack contents. This is deterministic and safe (no false negatives) because
        // if the first soul legendary is not the target, that seed can never yield the target from The Soul this run.
        // We IGNORE wildcard legendary soul clauses here (would require multi-soul lookahead) and let them pass.
        // NOTE: Removed previous environment variable toggles – always apply this safe identity prefilter.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask HandlePreFilters(ref MotelyVectorSearchContext ctx)
        {
            // Double-guard: only run if the global flag is set.
            if (!OuijaJsonFilterDesc.PrefilterEnabled)
                return VectorMask.AllBitsSet;
            if (_config.Must == null || _config.Must.Count == 0)
                return VectorMask.AllBitsSet;

            // DISABLED: The original soul joker prefilter compared ONLY the *first* soul legendary.
            // This causes FALSE NEGATIVES when the target legendary could appear from a later Soul card
            // (e.g., earlier Soul rolls a different legendary in an unrequested pack slot / later ante).
            // Until we implement multi-soul lookahead (or user explicitly asks for first-soul-only semantics),
            // we fail OPEN here.
            DebugLogger.Log("[PreFilters] Soul joker identity prefilter disabled (avoiding first-soul false negatives)");

            // NEW: Cheap negative blind tag prefilter (antes 2-4). Eliminates seeds lacking NegativeTag on both blinds.
            bool requiresNegTag = _config.Must?.Any(it =>
                (it.ItemTypeEnum == MotelyFilterItemType.SmallBlindTag || it.ItemTypeEnum == MotelyFilterItemType.BigBlindTag) &&
                it.TagEnum.HasValue && it.TagEnum.Value == MotelyTag.NegativeTag) ?? false;

            if (!requiresNegTag)
                return VectorMask.AllBitsSet;

            return PrefilterNegativeTag(ref ctx);
        }

        // Heuristic cost for ordering MUST clauses: lower runs earlier (cheaper &/or more selective)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetClauseCost(OuijaConfig.FilterItem item)
        {
            // Base on type + specificity + rarity. Lower = sooner.
            switch (item.ItemTypeEnum)
            {
                case MotelyFilterItemType.Voucher:
                    return 3; // BALANCED PRIORITY - vouchers need early processing for state but are expensive
                case MotelyFilterItemType.SmallBlindTag:
                case MotelyFilterItemType.BigBlindTag:
                    return 6; // two tag fetches
                case MotelyFilterItemType.SoulJoker:
                    // Specific legendary soul joker: super selective & cheap vector look
                    return item.JokerEnum.HasValue ? 1 : 25;
                case MotelyFilterItemType.Joker:
                    // Specific legendary earlier than rare; wildcards later
                    if (item.JokerEnum.HasValue)
                    {
                        // Extract rarity bits (same logic as IsRareJoker but need legendary priority)
                        var rarity = (MotelyJokerRarity)((int)item.JokerEnum.Value & 0xFF00);
                        return rarity == MotelyJokerRarity.Legendary ? 2 :
                               rarity == MotelyJokerRarity.Rare ? 8 : 12;
                    }
                    if (item.WildcardEnum.HasValue)
                    {
                        return item.WildcardEnum.Value switch
                        {
            
                            JokerWildcard.AnyRare => 9,
                            JokerWildcard.AnyUncommon => 13,
                            JokerWildcard.AnyCommon => 14,
                            JokerWildcard.AnyJoker => 18,
                            _ => 19
                        };
                    }
                    return 20;
                case MotelyFilterItemType.SpectralCard:
                case MotelyFilterItemType.TarotCard:
                case MotelyFilterItemType.PlanetCard:
                    return item.SpectralEnum.HasValue || item.TarotEnum.HasValue || item.PlanetEnum.HasValue ? 11 : 15;
                case MotelyFilterItemType.PlayingCard:
                    return 30; // expensive multi-attribute
                case MotelyFilterItemType.Boss:
                    return 16; // trivial but low selectivity
                default:
                    return 40;
            }
        }
        
        // Enhanced ProcessClause with voucher state support for vectorized filtering
        private VectorMask ProcessClause(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, bool orAcrossAntes, VectorMask activeMask, ref MotelyVectorRunStateVoucher sharedVoucherState)
        {
            // If upstream mask already killed all lanes, bail early
            if (activeMask.IsAllFalse())
                return activeMask;

            VectorMask result = orAcrossAntes ? VectorMask.NoBitsSet : VectorMask.AllBitsSet;
            
            // For voucher clauses, we need special handling to support activation and upgrades
            if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
            {
                DebugLogger.Log($"[ProcessClause] Processing voucher clause for {clause.VoucherEnum?.ToString() ?? "unknown"} across antes: {string.Join(",", clause.EffectiveAntes)}");
                return ProcessVoucherClause(ref ctx, clause, orAcrossAntes, activeMask, ref sharedVoucherState);
            }
            
            foreach (var ante in clause.EffectiveAntes)
            {
                VectorMask anteMask = VectorMask.NoBitsSet;

                // Handle different types using VECTORIZED methods!
                anteMask = (global::System.Object)clause.ItemTypeEnum switch
                {
                    MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => CheckTag(ref ctx, clause, ante),
                    MotelyFilterItemType.Voucher => CheckVoucher(ref ctx, clause, ante), // This should not be reached due to early return above
                    MotelyFilterItemType.Joker => CheckJoker(ref ctx, clause, ante),
                    MotelyFilterItemType.SoulJoker => CheckSoulJoker(ref ctx, clause, ante),// VECTORIZED soul joker checking
                    MotelyFilterItemType.TarotCard or MotelyFilterItemType.PlanetCard or MotelyFilterItemType.SpectralCard => CheckConsumable(ref ctx, clause, ante),// These can do basic vectorized checks for existence
                    MotelyFilterItemType.PlayingCard or MotelyFilterItemType.Boss => VectorMask.AllBitsSet,// These need individual checking - return all true to defer to individual processing
                    _ => VectorMask.AllBitsSet,// Unknown types - assume can't be vectorized
                };
                if (orAcrossAntes)
                    result |= anteMask & activeMask; // only care about still-alive lanes
                else
                    result &= anteMask; // AND semantics already naturally shrink
                    
                // EARLY EXIT: If processing AND clauses and result is already all false
                if (!orAcrossAntes && result.IsAllFalse())
                {
                    DebugLogger.Log($"[ProcessClause] Early exit - all seeds eliminated in ante {ante}");
                    return result;
                }
            }
            
            // Constrain by upstream active mask (so we never re-enable dead lanes)
            return result & activeMask;
        }
        
        // NEW: Vectorized joker checking - does a quick vectorized pass
        private VectorMask CheckJoker(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // IMPORTANT: Shop slot vectorization DISABLED.
            // Previous implementation incorrectly used a pure Joker stream (skipping Tarot/Planet/Spectral draws),
            // breaking slot index alignment and causing false negatives (e.g. shopSlots:[2]).
            // Until a FULL vector shop item stream (with item type PRNG) is implemented, we must NOT prefilter by shop joker slots.
            // Return AllBitsSet so scalar path performs authoritative evaluation.
            if (clause.Sources?.ShopSlots is { Length: > 0 })
                return VectorMask.AllBitsSet;
            // PACK-ONLY prefilter: if only packSlots specified, we can at least eliminate seeds whose referenced pack slots are NOT Buffoon (or not Mega when required).
            if (OuijaJsonFilterDesc.PrefilterEnabled && clause.Sources?.PackSlots is { Length: > 0 })
            {
                var packSlots = clause.Sources.PackSlots;
                int maxPackIdx = packSlots.Max();
                var packSlotSet = new HashSet<int>(packSlots);
                var packStream = ctx.CreateBoosterPackStream(ante);
                VectorMask anyBuffoon = VectorMask.NoBitsSet;
                for (int i = 0; i <= maxPackIdx; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (!packSlotSet.Contains(i)) continue; // skip unrequested pack slots (still advanced for RNG alignment)
                    // Only Buffoon packs can contain non-legendary jokers like Blueprint. (Soul legendary path handled elsewhere.)
                    var isBuffoon = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Buffoon);
                    if (clause.Sources.RequireMega == true)
                    {
                        var isMega = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega);
                        isBuffoon &= isMega;
                    }
                    anyBuffoon |= isBuffoon;
                }
                // anyBuffoon lanes remain candidates; others eliminated now.
                return anyBuffoon;
            }
            // No explicit shop slots: defer to scalar path (packs or invalid) without pre-filtering.
            return VectorMask.AllBitsSet;
        }
        
        // SMART Vectorized soul joker checking - pre-filters by FIRST soul joker
        private VectorMask CheckSoulJoker(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // KEY INSIGHT: Check what the FIRST soul joker would be for each seed
            // This eliminates seeds that would generate the wrong legendary even if they had Soul
            // Seeds that pass this check still need individual processing to verify pack contents
            // IMPORTANT: This is ONLY sound for queries that care about the FIRST soul legendary irrespective of
            // which pack slot produced The Soul. If the user restricts pack slots (sources.packSlots) we can get
            // FALSE NEGATIVES (first soul legendary might come from an unrequested pack slot while the requested
            // slot later yields the target). In that case we must FAIL OPEN and defer to scalar simulation.
            
            // OPTIMIZATION: For negative edition searches, we can pre-filter by checking edition
            // even with packSlots, since we can safely consume RNG state with cached streams
            if (clause.Sources?.PackSlots is { Length: > 0 })
            {
                // No special pre-filtering for packSlots - requires full simulation
                
                return VectorMask.AllBitsSet; // keep all seeds - packSlots require full simulation
            }
            if (clause.Min.HasValue && clause.Min.Value > 1)
                return VectorMask.AllBitsSet; // multi-count queries require full simulation
            
            // Get the vectorized soul joker stream for this ante
            var soulJokerStream = ctx.CreateSoulJokerStream(ante);
            var firstSoulJoker = ctx.GetNextJoker(ref soulJokerStream);
            
            VectorMask result = VectorMask.NoBitsSet;
            
            if (clause.JokerEnum.HasValue)
            {
                // Check for specific joker
                var targetJoker = new MotelyItem(clause.JokerEnum.Value);
                result = MotelyItemVector.Equals(firstSoulJoker, targetJoker);
                
                // Also check edition if specified
                if (clause.EditionEnum.HasValue)
                {
                    result &= VectorEnum256.Equals(firstSoulJoker.Edition, clause.EditionEnum.Value);
                }
                
                DebugLogger.Log($"[CheckSoulJokerVector] Ante {ante}: Pre-filtered for FIRST soul joker = {clause.Value} {clause.Edition}");
            }
            else
            {
                // Looking for any legendary joker
                result = VectorEnum256.Equals(firstSoulJoker.Type, MotelyItemType.Triboulet) |
                        VectorEnum256.Equals(firstSoulJoker.Type, MotelyItemType.Perkeo) |
                        VectorEnum256.Equals(firstSoulJoker.Type, MotelyItemType.Yorick) |
                        VectorEnum256.Equals(firstSoulJoker.Type, MotelyItemType.Chicot) |
                        VectorEnum256.Equals(firstSoulJoker.Type, MotelyItemType.Canio);
                
                if (clause.EditionEnum.HasValue)
                {
                    result &= VectorEnum256.Equals(firstSoulJoker.Edition, clause.EditionEnum.Value);
                }
            }
            
            // Seeds passing this check MIGHT have the right soul joker
            // Individual processing will verify if they actually have Soul cards in packs
            return result;
        }
        
        // NEW: Basic vectorized consumable checking
        private VectorMask CheckConsumable(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // Use Motely's built-in vectorized consumable filtering
            switch (clause.ItemTypeEnum)
            {
                case MotelyFilterItemType.TarotCard:
                    if (clause.TarotEnum.HasValue)
                        return ctx.FilterTarotCard(ante, clause.TarotEnum.Value);
                    break;

                case MotelyFilterItemType.PlanetCard:
                    if (clause.PlanetEnum.HasValue)
                        return ctx.FilterPlanetCard(ante, clause.PlanetEnum.Value);
                    break;

                case MotelyFilterItemType.SpectralCard:
                    if (clause.SpectralEnum.HasValue)
                        return ctx.FilterSpectralCard(ante, clause.SpectralEnum.Value);
                    break;
                
            }
            
            // If no specific consumable type or enum, defer to individual processing
            return VectorMask.AllBitsSet;
        }
        
        private VectorMask CheckTag(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);
            
            // Assume validation was done during config parsing
            if (!clause.TagEnum.HasValue)
                return VectorMask.NoBitsSet;
            
            var targetTag = clause.TagEnum.Value;

            // Use pre-parsed enum for type checking
            return (global::System.Object?)clause.TagTypeEnum switch
            {
                MotelyTagType.SmallBlind => (VectorMask)VectorEnum256.Equals(smallTag, targetTag),
                MotelyTagType.BigBlind => (VectorMask)VectorEnum256.Equals(bigTag, targetTag),
                // Any
                _ => (VectorMask)(VectorEnum256.Equals(smallTag, targetTag) | VectorEnum256.Equals(bigTag, targetTag)),
            };
        }
        
        // NEW: Prefilter seeds that must have NegativeTag on both blinds in antes 2-4
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask PrefilterNegativeTag(ref MotelyVectorSearchContext ctx)
        {
            VectorMask mask = VectorMask.AllBitsSet;
            for (int ante = 2; ante <= 4; ante++)
            {
                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                mask &= VectorEnum256.Equals(smallTag, MotelyTag.NegativeTag);
                mask &= VectorEnum256.Equals(bigTag, MotelyTag.NegativeTag);

                if (mask.IsAllFalse())
                    break;
            }
            return mask;
        }

        // Single-pass voucher processing: iterate through antes 1-N, activate vouchers and check requirements immediately
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask ProcessAllVoucherClausesUnified(ref MotelyVectorSearchContext searchContext, VectorMask currentMask, ref MotelyVectorRunStateVoucher sharedVoucherState)
        {
            // Collect all voucher clauses (both MUST and MUST NOT)
            var allVoucherClauses = new List<(OuijaConfig.FilterItem clause, bool isMust)>();
            
            foreach (var must in _config.Must.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher))
                allVoucherClauses.Add((must, true));
            
            foreach (var mustNot in _config.MustNot.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher))
                allVoucherClauses.Add((mustNot, false));
            
            if (allVoucherClauses.Count == 0)
            {
                DebugLogger.Log("[ProcessAllVoucherClausesUnified] No voucher clauses found, returning current mask");
                return currentMask;
            }
            
            // Determine the maximum ante from all voucher clauses
            int maxAnte = allVoucherClauses.SelectMany(vc => vc.clause.EffectiveAntes).Max();
            DebugLogger.Log($"[ProcessAllVoucherClausesUnified] Starting single-pass voucher processing from ante 1-{maxAnte}");
            
            VectorMask resultMask = currentMask;
            
            // Single pass: iterate through antes 1-N, activate vouchers and check requirements
            for (int ante = 1; ante <= maxAnte; ante++)
            {
                // Get the voucher that appears in this ante
                var voucher = searchContext.GetAnteFirstVoucher(ante, sharedVoucherState);
                
                // Activate EVERY voucher found in this ante (1 voucher per ante)
                foreach (MotelyVoucher possibleVoucher in Enum.GetValues<MotelyVoucher>())
                {
                    var voucherMask = VectorEnum256.Equals(voucher, possibleVoucher);
                    VectorMask voucherMaskConverted = voucherMask;
                    
                    if (!voucherMaskConverted.IsAllFalse())
                    {
                        DebugLogger.Log($"[ProcessAllVoucherClausesUnified] Ante {ante}: Activating voucher {possibleVoucher}");
                        sharedVoucherState.ActivateVoucher(possibleVoucher);
                        
                        // Handle prerequisite activation for odd vouchers
                        int voucherInt = (int)possibleVoucher;
                        if ((voucherInt & 1) == 1) // Odd voucher
                        {
                            var prerequisiteVoucher = (MotelyVoucher)(voucherInt - 1);
                            DebugLogger.Log($"[ProcessAllVoucherClausesUnified] Ante {ante}: {possibleVoucher} is odd, also activating prerequisite {prerequisiteVoucher}");
                            sharedVoucherState.ActivateVoucher(prerequisiteVoucher);
                        }
                        break; // Only one voucher per ante
                    }
                }
                
                // After activating vouchers in this ante, check all requirements that include this ante
                foreach (var (clause, isMust) in allVoucherClauses)
                {
                    if (!clause.VoucherEnum.HasValue || !clause.EffectiveAntes.Contains(ante)) continue;
                    
                    var targetVoucher = clause.VoucherEnum.Value;
                    
                    // Check if the target voucher is active in the shared state (handles prerequisites)
                    var isActive = sharedVoucherState.IsVoucherActive(targetVoucher);
                    VectorMask activeMask = isActive;
                    
                    DebugLogger.Log($"[ProcessAllVoucherClausesUnified] Ante {ante}: Checking {(isMust ? "MUST" : "MUST NOT")} voucher {targetVoucher}, active: {!activeMask.IsAllFalse()}");
                    
                    if (isMust)
                    {
                        // MUST: keep only seeds where the voucher is active
                        resultMask &= activeMask;
                    }
                    else
                    {
                        // MUST NOT: exclude seeds where the voucher is active
                        resultMask &= activeMask ^ VectorMask.AllBitsSet;
                    }
                    
                    if (resultMask.IsAllFalse())
                    {
                        DebugLogger.Log($"[ProcessAllVoucherClausesUnified] All seeds eliminated after {(isMust ? "MUST" : "MUST NOT")} voucher {targetVoucher} in ante {ante}");
                        return resultMask;
                    }
                }
            }
            
            DebugLogger.Log($"[ProcessAllVoucherClausesUnified] Single-pass voucher processing complete, seeds remaining");
            return resultMask;
        }
        
        // Process a single voucher clause for a specific ante
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask ProcessVoucherClauseForAnte(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, bool isMust, VectorMask activeMask, ref MotelyVectorRunStateVoucher voucherState, int ante)
        {
            if (!clause.VoucherEnum.HasValue)
            {
                DebugLogger.Log($"[ProcessVoucherClauseForAnte] No voucher enum specified for ante {ante}");
                return VectorMask.NoBitsSet;
            }

            var targetVoucher = clause.VoucherEnum.Value;
            DebugLogger.Log($"[ProcessVoucherClauseForAnte] Voucher {targetVoucher} EffectiveAntes: [{string.Join(", ", clause.EffectiveAntes)}]");
            
            // Check if this ante is in the clause's effective antes
            if (!clause.EffectiveAntes.Contains(ante))
            {
                DebugLogger.Log($"[ProcessVoucherClauseForAnte] Voucher {targetVoucher} not specified for ante {ante}, skipping");
                return VectorMask.NoBitsSet; // No match for MUST, no restriction for MUST NOT
            }
            
            DebugLogger.Log($"[ProcessVoucherClauseForAnte] Checking voucher {targetVoucher} in ante {ante} with shared state");
            
            // CRITICAL FIX: Handle prerequisite vouchers for odd-numbered vouchers
            // Observatory (11) requires Telescope (10), etc.
            int targetVoucherInt = (int)targetVoucher;
            if ((targetVoucherInt & 1) == 1) // Odd voucher requires prerequisite
            {
                var prerequisiteVoucher = (MotelyVoucher)(targetVoucherInt - 1);
                DebugLogger.Log($"[ProcessVoucherClauseForAnte] {targetVoucher} is odd ({targetVoucherInt}), checking prerequisite {prerequisiteVoucher} ({targetVoucherInt - 1})");
                
                // Check if we need to activate the prerequisite first
                // This handles cases like Observatory requiring Telescope
                if (!voucherState.IsVoucherActive(prerequisiteVoucher).Equals(Vector256<int>.AllBitsSet))
                {
                    DebugLogger.Log($"[ProcessVoucherClauseForAnte] Prerequisite {prerequisiteVoucher} not fully active, checking if it appears in this ante");
                    
                    // Check if the prerequisite appears in this ante
                    var prerequisiteCheck = ctx.GetAnteFirstVoucher(ante, voucherState);
                    var prerequisiteMask = VectorEnum256.Equals(prerequisiteCheck, prerequisiteVoucher);
                    
                    // Activate prerequisite for seeds that found it
                    VectorMask prerequisiteMaskConverted = prerequisiteMask;
                    if (!prerequisiteMaskConverted.IsAllFalse())
                    {
                        DebugLogger.Log($"[ProcessVoucherClauseForAnte] Activating prerequisite {prerequisiteVoucher} found in ante {ante}");
                        voucherState.ActivateVoucher(prerequisiteVoucher);
                    }
                }
            }
            
            // Get voucher with current state (handles prerequisites and upgrades)
            var voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
            var anteMask = VectorEnum256.Equals(voucher, targetVoucher);
            
            // Activate the voucher for seeds that found it (enables upgrades in later antes)
            VectorMask anteMaskConverted = anteMask;
            if (!anteMaskConverted.IsAllFalse())
            {
                DebugLogger.Log($"[ProcessVoucherClauseForAnte] Activating voucher {targetVoucher} for matched seeds in ante {ante}");
                // Note: We activate for ALL lanes since we can't selectively activate per lane in vectorized mode
                // Individual seed processing will handle the precise activation logic
                voucherState.ActivateVoucher(targetVoucher);
            }
            
            DebugLogger.Log($"[ProcessVoucherClauseForAnte] Result for voucher {targetVoucher} in ante {ante}: processing complete");
            return anteMask;
        }
        
        // Enhanced voucher clause processing with activation and upgrade support
        private VectorMask ProcessVoucherClause(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, bool orAcrossAntes, VectorMask activeMask, ref MotelyVectorRunStateVoucher voucherState)
        {
            if (!clause.VoucherEnum.HasValue)
            {
                DebugLogger.Log($"[ProcessVoucherClause] No voucher enum specified");
                return VectorMask.NoBitsSet;
            }

            var targetVoucher = clause.VoucherEnum.Value;
            DebugLogger.Log($"[ProcessVoucherClause] Processing voucher {targetVoucher} with shared state support");
            
            VectorMask result = orAcrossAntes ? VectorMask.NoBitsSet : VectorMask.AllBitsSet;
            
            foreach (var ante in clause.EffectiveAntes)
            {
                DebugLogger.Log($"[ProcessVoucherClause] Checking ante {ante} for voucher {targetVoucher}");
                
                // Get voucher with current state (handles prerequisites and upgrades)
                var voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
                var anteMask = VectorEnum256.Equals(voucher, targetVoucher);
                
                
                // Activate the voucher for seeds that found it (enables upgrades in later antes)
                VectorMask anteMaskConverted = anteMask;
                if (!anteMaskConverted.IsAllFalse())
                {
                    DebugLogger.Log($"[ProcessVoucherClause] Activating voucher {targetVoucher} for matched seeds in ante {ante}");
                    // Note: We activate for ALL lanes since we can't selectively activate per lane in vectorized mode
                    // Individual seed processing will handle the precise activation logic
                    voucherState.ActivateVoucher(targetVoucher);
                }
                
                // Combine results based on OR/AND logic
                if (orAcrossAntes)
                {
                    result |= anteMask;
                }
                else
                {
                    result &= anteMask;
                }
                
                // Early exit optimization for AND logic
                if (!orAcrossAntes && result.IsAllFalse())
                {
                    DebugLogger.Log($"[ProcessVoucherClause] Early exit - no seeds remain after ante {ante}");
                    break;
                }
            }
            
            DebugLogger.Log($"[ProcessVoucherClause] Final result for voucher {targetVoucher}: processing complete");
            return result;
        }
        
        // Enhanced voucher checking with state support for vectorized filtering
        private VectorMask CheckVoucher(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            // Assume validation was done during config parsing
            if (!clause.VoucherEnum.HasValue)
            {
                DebugLogger.Log($"[CheckVoucher] No voucher enum specified for ante {ante}");
                return VectorMask.NoBitsSet;
            }

            var targetVoucher = clause.VoucherEnum.Value;
            DebugLogger.Log($"[CheckVoucher] Looking for voucher {targetVoucher} in ante {ante}");
            
            // Get the first voucher without state (basic check)
            var voucher = ctx.GetAnteFirstVoucher(ante);
            var basicMatch = VectorEnum256.Equals(voucher, targetVoucher);
            
            DebugLogger.Log($"[CheckVoucher] Basic match result for {targetVoucher}: processing complete");
            
            // If we have basic matches, we're done for the vectorized path
            // Individual seed processing will handle voucher activation and upgrades
            return basicMatch;
        }
        
        private static int CountOccurrences(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyRunState voucherState)
        {
            int totalCount = 0;
            DebugLogger.Log($"[CountOccurrences] Counting occurrences in antes: {string.Join(",", clause.EffectiveAntes)}");
            
            foreach (var ante in clause.EffectiveAntes)
            {
                
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
                        anteCount = CheckSoulJoker(ref ctx, clause, ante, ref packStream, in voucherState);
                        break;
                        
                    // For other types, just return 1 if found (for now)
                    case MotelyFilterItemType.TarotCard:
                        anteCount = CheckTarot(ref ctx, clause, ante, ref shopStream, ref packStream, in voucherState) ? 1 : 0;
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
                        anteCount = CheckVoucherSingle(ref ctx, clause, ante, ref voucherState) ? 1 : 0;
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
        private static bool CheckSingleClause(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyRunState voucherState)
        {
            // PERFORMANCE: Early exit if no antes to check
            if (clause.EffectiveAntes == null || clause.EffectiveAntes.Length == 0)
                return false;
                
            DebugLogger.Log($"[CheckSingleClause] Checking antes: {string.Join(",", clause.EffectiveAntes)}");
            
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
            
            foreach (var ante in clause.EffectiveAntes)
            {
                
                DebugLogger.Log($"[CheckSingleClause] Checking ante {ante}...");
                bool found = false;
                
                // PERFORMANCE: Create streams only when needed
                // Use isCached: false to ensure each clause gets a fresh stream
                var shopStream = needsShopStream ? ctx.CreateShopItemStream(ante, isCached: false) : default;
                var packStream = needsPackStream ? ctx.CreateBoosterPackStream(ante, isCached: false) : default;
                
                switch (clause.ItemTypeEnum)
                {
                    case MotelyFilterItemType.Joker:
                        int jokerCount = CheckJoker(ref ctx, clause, ante, ref shopStream, ref packStream);
                        found = jokerCount > 0;
                        
                        // SHOWMAN ACTIVATION: If we found a Showman joker, activate it in the run state
                        if (found && clause.JokerEnum.HasValue && clause.JokerEnum.Value == MotelyJoker.Showman)
                        {
                            voucherState.ActivateShowman();
                            if (DebugLogger.IsEnabled)
                                DebugLogger.Log($"[CheckSingleClause] Activated Showman joker for ante {ante}");
                        }
                        break;
                        
                    case MotelyFilterItemType.SoulJoker:
                        found = CheckSoulJoker(ref ctx, clause, ante, ref packStream, in voucherState) > 0;
                        break;
                        
                    case MotelyFilterItemType.TarotCard:
                        found = CheckTarot(ref ctx, clause, ante, ref shopStream, ref packStream, in voucherState);
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
                        found = CheckVoucherSingle(ref ctx, clause, ante, ref voucherState);
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
            bool hasWildcard = clause.WildcardEnum.HasValue;
            bool hasSpecificJoker = clause.JokerEnum.HasValue;
            
            // DEBUG: Log what we're searching for
            Debug.Assert(hasWildcard || hasSpecificJoker, 
                $"CheckJoker called but no joker specified! Value='{clause.Value}', Type='{clause.Type}', WildcardEnum={clause.WildcardEnum}, JokerEnum={clause.JokerEnum}");
            
            if (!hasWildcard && !hasSpecificJoker)
            {
                // This should NEVER happen - something is wrong with enum parsing
                DebugLogger.Log($"[CheckJoker] ERROR: No joker enum parsed! Value='{clause.Value}', Type='{clause.Type}'");
                throw new InvalidOperationException("No joker enum parsed");
            }
            
            // Determine target rarity for wildcard searches
            MotelyJokerRarity? targetRarity = null;
            if (hasWildcard)
            {
                targetRarity = clause.WildcardEnum switch
                {
                    JokerWildcard.AnyCommon => MotelyJokerRarity.Common,
                    JokerWildcard.AnyUncommon => MotelyJokerRarity.Uncommon,
                    JokerWildcard.AnyRare => MotelyJokerRarity.Rare,
    
                    JokerWildcard.AnyJoker => null, // Any rarity
                    _ => null
                };
            }
            
            MotelyJoker? targetJoker = hasSpecificJoker ? clause.JokerEnum : null;
            int foundCount = 0;
            
            if (DebugLogger.IsEnabled)
            {
                string searchDescription = hasWildcard ? clause.WildcardEnum.ToString()! : 
                                          hasSpecificJoker ? targetJoker!.Value.ToString() :
                                          "unknown";
                DebugLogger.Log($"[CheckJoker] Looking for {searchDescription} in ante {ante} HasWildcard: {hasWildcard} HasSpecificJoker: {hasSpecificJoker}");
            }
                
            // SHOP: only inspect if explicit shopSlots specified.
            if (clause.Sources?.ShopSlots is { Length: > 0 })
            {
                var slots = clause.Sources.ShopSlots;
                int maxIdx = slots.Max();
                var slotSet = new HashSet<int>(slots);
                for (int i = 0; i <= maxIdx; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream); // advance for RNG alignment
                    if (!slotSet.Contains(i)) continue;
                    if (item.TypeCategory != MotelyItemTypeCategory.Joker) continue;
                    var shopJoker = new MotelyItem(item.Value).GetJoker();
                    bool jokerMatches = false;
                    if (hasWildcard)
                    {
                        if (clause.WildcardEnum == JokerWildcard.AnyJoker)
                            jokerMatches = true;
                        else if (targetRarity.HasValue)
                        {
                            var jokerRarity = (MotelyJokerRarity)((int)shopJoker & Motely.JokerRarityMask);
                            jokerMatches = jokerRarity == targetRarity.Value;
                        }
                    }
                    else if (hasSpecificJoker)
                    {
                        jokerMatches = shopJoker == targetJoker;
                        if (DebugLogger.IsEnabled && clause.EditionEnum.HasValue)
                            DebugLogger.Log($"[CheckJoker] Specific joker check: shopJoker={shopJoker}, targetJoker={targetJoker}, matches={jokerMatches}, edition={item.Edition}, targetEdition={clause.EditionEnum}");
                    }
                    if (jokerMatches && CheckEditionAndStickers(item, clause))
                    {
                        foundCount++;
                        if (DebugLogger.IsEnabled)
                        {
                            string jokerName = hasWildcard ? $"{clause.WildcardEnum} ({shopJoker})" : shopJoker.ToString();
                            DebugLogger.Log($"[CheckJoker] Found {jokerName} in shop slot {i}! Total count: {foundCount}");
                        }
                        if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                            return foundCount;
                    }
                }
            }

            // PACKS: only inspect if explicit packSlots specified.
            if (clause.Sources?.PackSlots is { Length: > 0 })
            {
                var packSlots = clause.Sources.PackSlots;
                int packLoop = clause.MaxPackSlot >= 0 ? clause.MaxPackSlot + 1 : packSlots.Max() + 1;
                bool requireMega = clause.Sources?.RequireMega ?? false;
                bool simpleFastPath = clause.IsSimpleNegativeAnyPackOnly; // any joker + edition + only packs + Min<=1
                MotelySingleJokerStream buffoonStream = default;
                bool buffoonStreamInit = false;
                for (int i = 0; i < packLoop; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    // membership test (small array): linear; packSlots length small; optimize later via mask if needed
                    bool evaluate = false;
                    foreach (var ps in packSlots) { if (ps == i) { evaluate = true; break; } }
                    if (!evaluate) continue;
                    if (pack.GetPackType() != MotelyBoosterPackType.Buffoon) continue;
                    if (requireMega && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;
                    if (!buffoonStreamInit)
                    {
                        buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                        buffoonStreamInit = true;
                    }
                    int cardCount = MotelyBoosterPackType.Buffoon.GetCardCount(pack.GetPackSize());
                    if (simpleFastPath)
                    {
                        // Stream cards one by one; early exit on first Negative edition joker.
                        for (int c = 0; c < cardCount; c++)
                        {
                            var jokerItem = ctx.GetNextJoker(ref buffoonStream);
                            if (CheckEditionAndStickers(jokerItem, clause))
                            {
                                foundCount = 1;
                                return foundCount; // Min <=1 ensured
                            }
                        }
                    }
                    else
                    {
                        var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var joker = contents.GetItem(j);
                            var extractedJoker = new MotelyItem(joker.Value).GetJoker();
                            bool jokerMatches = false;
                            if (hasWildcard)
                            {
                                if (clause.WildcardEnum == JokerWildcard.AnyJoker) jokerMatches = true; else if (targetRarity.HasValue)
                                {
                                    var jr = (MotelyJokerRarity)((int)extractedJoker & Motely.JokerRarityMask);
                                    jokerMatches = jr == targetRarity.Value;
                                }
                            }
                            else if (hasSpecificJoker)
                                jokerMatches = extractedJoker == targetJoker;
                            if (jokerMatches && CheckEditionAndStickers(joker, clause))
                            {
                                foundCount++;
                                if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                                    return foundCount;
                            }
                        }
                    }
                }
            }
            
            if (DebugLogger.IsEnabled)
            {
                string searchDescription = hasWildcard ? clause.WildcardEnum.ToString()! : 
                                          hasSpecificJoker ? targetJoker!.Value.ToString() :
                                          "unknown";
                DebugLogger.Log($"[CheckJoker] === FINAL COUNT for {searchDescription} in ante {ante}: {foundCount} ===");
                DebugLogger.Log($"[CheckJoker] Sources checked: Shop={(clause.Sources?.ShopSlots?.Length > 0)}, Packs={(clause.Sources?.PackSlots?.Length > 0)}");
            }
            return foundCount;
        }
        
    private static int CheckSoulJoker(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleBoosterPackStream packStream, in MotelyRunState runState)
        {
            // Soul jokers only come from The Soul card in Arcana or Spectral packs
            // They are always legendary: Perkeo, Canio, Triboulet, Yorick, or Chicot
            
            bool searchAnySoulJoker = !clause.JokerEnum.HasValue;
            MotelyJoker? targetJoker = searchAnySoulJoker ? null : clause.JokerEnum!.Value;
            int foundCount = 0;
            
            // DEBUG: Log what we're actually searching for
            DebugLogger.Log($"[CheckSoulJoker] clause.JokerEnum.HasValue={clause.JokerEnum.HasValue}, clause.Value='{clause.Value}', clause.Type='{clause.Type}'");
            DebugLogger.Log($"[CheckSoulJoker] searchAnySoulJoker={searchAnySoulJoker}, targetJoker={targetJoker}");
            
            string searchTarget = searchAnySoulJoker ? "any soul joker" : targetJoker.ToString()!;
            DebugLogger.Log($"[CheckSoulJoker] Looking for {searchTarget} in ante {ante}, Edition={clause.EditionEnum}");
            
            // Soul jokers can only come from booster packs, not shop
            // If PackSlots is specified but empty, we should still check packs (all slots)
            // Only skip if explicitly null or the sources object is null
            bool shouldCheckPacks = clause.Sources == null || clause.Sources.PackSlots != null;
            
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
            
            // Use packSlots from config if specified
            var packSlots = clause.Sources?.PackSlots;
            int packCount = (packSlots != null && packSlots.Length > 0) ? 
                packSlots.Max() + 1 : // Need to iterate up to highest slot + 1
                (ante == 1 ? 4 : 6); // Default pack count if not specified
            
            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                DebugLogger.Log($"[CheckSoulJoker] Pack slot {i}: Type={pack.GetPackType()}, Size={pack.GetPackSize()}");
                
                // Skip if this pack slot isn't in our filter
                if (packSlots != null && packSlots.Length > 0 && !packSlots.Contains(i))
                {
                    DebugLogger.Log($"[CheckSoulJoker] Skipping pack #{i} - not in packSlots filter");
                    continue;
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
                    
                    // Generate full pack contents to keep RNG in sync
                    var packContents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                    bool hasSoul = false;
                    for (int j = 0; j < packContents.Length; j++)
                    {
                        if (packContents.GetItem(j) == MotelyItemType.Soul)
                        {
                            hasSoul = true;
                            // DON'T BREAK - need to check all cards to keep RNG in sync!
                        }
                    }
                    DebugLogger.Log($"[CheckSoulJoker] Arcana pack has The Soul: {hasSoul}");
                    
                    if (hasSoul)
                    {
                        if (!soulStreamInit) 
                        {
                            soulStreamInit = true;
                            soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
                            DebugLogger.Log($"[CheckSoulJoker] Created new soul stream for ante {ante}");
                        }
                        var soulJoker = ctx.GetNextJoker(ref soulStream);
                        DebugLogger.Log($"[CheckSoulJoker] The Soul gives: {soulJoker.Type}, Edition={soulJoker.Edition}");
                        DebugLogger.Log($"[CheckSoulJoker] Raw joker value: 0x{soulJoker.Value:X8}");
                        DebugLogger.Log($"[CheckSoulJoker] MotelyItemType enum value: {(int)soulJoker.Type}");
                        
                        // Simple comparison like PerkeoObservatory does - just use .Type!
                        var soulJokerType = soulJoker.Type;
                        
                        DebugLogger.Log($"[CheckSoulJoker] Soul gave: {soulJokerType}, Target: {(targetJoker.HasValue ? $"MotelyItemType.{targetJoker.Value}" : "any")}, searchAnySoulJoker={searchAnySoulJoker}");
                        
                        if (searchAnySoulJoker || (targetJoker.HasValue && soulJokerType == new MotelyItem(targetJoker.Value).Type))
                        {
                            DebugLogger.Log($"[CheckSoulJoker] Joker matches! Now checking edition: {soulJoker.Edition} vs required: {clause.EditionEnum}");
                            if (CheckEditionAndStickers(soulJoker, clause))
                            {
                                foundCount++;
                                DebugLogger.Log($"[CheckSoulJoker] ✅ FOUND MATCH! {soulJokerType} with {soulJoker.Edition} edition from Arcana Soul! Total count: {foundCount}");
                            }
                            else
                            {
                                DebugLogger.Log($"[CheckSoulJoker] ❌ Edition mismatch: {soulJoker.Edition} != {clause.EditionEnum}");
                            }
                        }
                        else
                        {
                            DebugLogger.Log($"[CheckSoulJoker] ❌ Wrong joker: {soulJokerType} != {targetJoker}");
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
                    
                    // Generate full pack contents to keep RNG in sync
                    var packContents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                    bool hasSoul = false;
                    for (int j = 0; j < packContents.Length; j++)
                    {
                        if (packContents.GetItem(j) == MotelyItemType.Soul)
                        {
                            hasSoul = true;
                            // DON'T BREAK - need to check all cards to keep RNG in sync!
                        }
                    }
                    DebugLogger.Log($"[CheckSoulJoker] Spectral pack has The Soul: {hasSoul}");
                    
                    if (hasSoul)
                    {
                        if (!soulStreamInit) 
                        {
                            soulStreamInit = true;
                            soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
                            DebugLogger.Log($"[CheckSoulJoker] Created new soul stream for ante {ante}");
                        }
                        var soulJoker = ctx.GetNextJoker(ref soulStream);
                        DebugLogger.Log($"[CheckSoulJoker] The Soul gives: {soulJoker.Type}, Edition={soulJoker.Edition}");
                        DebugLogger.Log($"[CheckSoulJoker] Raw joker value: 0x{soulJoker.Value:X8}");
                        DebugLogger.Log($"[CheckSoulJoker] MotelyItemType enum value: {(int)soulJoker.Type}");
                        
                        // Simple comparison like PerkeoObservatory does - just use .Type!
                        var soulJokerType = soulJoker.Type;
                        
                        DebugLogger.Log($"[CheckSoulJoker] SPECTRAL Soul gave: {soulJokerType}, Target: {(targetJoker.HasValue ? $"MotelyItemType.{targetJoker.Value}" : "any")}");
                        
                        if (searchAnySoulJoker || (targetJoker.HasValue && soulJokerType == new MotelyItem(targetJoker.Value).Type))
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
            
            DebugLogger.Log($"[CheckSoulJoker] === FINAL COUNT for {(targetJoker.HasValue ? targetJoker.Value.ToString() : "any soul joker")} in ante {ante}: {foundCount} ===");
            return foundCount;
        }
        
    private static bool CheckTarot(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelySingleShopItemStream shopStream, ref MotelySingleBoosterPackStream packStream, in MotelyRunState runState)
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
                    (ante == 1 ? 5 : 6);
                
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
                        var shopTarot = new MotelyItem(item.Value).GetTarot();
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
                // Use packSlots from config if specified, otherwise use default
                var packSlots = clause.Sources?.PackSlots;
                int packCount = (packSlots != null && packSlots.Length > 0) ? 
                    packSlots.Length : // Only iterate through specified pack slots
                    (ante == 1 ? 4 : 6); // Default if not specified
                
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
                    (ante == 1 ? 5 : 6);
                
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
                        var shopPlanet = new MotelyItem(item.Value).GetPlanet();
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
                // Use packSlots from config if specified, otherwise use default
                var packSlots = clause.Sources?.PackSlots;
                int packCount = (packSlots != null && packSlots.Length > 0) ? 
                    packSlots.Length : // Only iterate through specified pack slots
                    (ante == 1 ? 4 : 6); // Default if not specified
                
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
            
            // NOTE: Previous implementation performed TWO separate shop loops, advancing the shop stream twice
            // per ante when shopSlots were specified. This caused stream/index misalignment and could report
            // finds in unexpected higher slot numbers (e.g. "slot 6 or 7") even when only shopSlots:[0,1]
            // were configured. We now defer all shop spectral checking to the unified block later in this
            // method ("Check shop for spectral cards" section) to ensure a single pass with correct slot mapping.
            bool shouldCheckShop = clause.Sources?.ShopSlots != null && clause.Sources.ShopSlots.Length > 0;
            DebugLogger.Log($"[CheckSpectral] Sources: {(clause.Sources != null ? "exists" : "null")}");
            if (clause.Sources != null)
            {
                DebugLogger.Log($"[CheckSpectral] ShopSlots: {(clause.Sources.ShopSlots != null ? string.Join(",", clause.Sources.ShopSlots) : "null")}");
                DebugLogger.Log($"[CheckSpectral] PackSlots: {(clause.Sources.PackSlots != null ? string.Join(",", clause.Sources.PackSlots) : "null")}");
            }
                
            // Check packs
            bool shouldCheckPacks = clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0;
            
            if (shouldCheckPacks)
            {
                DebugLogger.Log($"[CheckSpectral] Checking booster packs in ante {ante}...");
                
                // Create spectral stream ONCE outside the loop
                MotelySingleSpectralStream spectralStream = default;
                bool spectralStreamInit = false;
                
                // IMPORTANT: We must iterate through ALL packs to keep stream in sync
                // but only check the ones specified in PackSlots
                // Use packSlots from config if specified
                var packSlots = clause.Sources?.PackSlots;
                int packCount = (packSlots != null && packSlots.Length > 0) ? 
                    packSlots.Max() + 1 : // Need to iterate up to highest slot + 1
                    (ante == 1 ? 4 : 6); // Default pack count if not specified
                
                DebugLogger.Log($"[CheckSpectral] Checking pack slots: {string.Join(",", packSlots ?? Array.Empty<int>())}");
                
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    
                    // Skip if this pack slot isn't in our filter
                    if (packSlots != null && packSlots.Length > 0 && !packSlots.Contains(i))
                    {
                        DebugLogger.Log($"[CheckSpectral] Pack slot {i}: Type={pack.GetPackType()}, Size={pack.GetPackSize()} - SKIPPING (not in filter)");
                        continue;
                    }
                    
                    DebugLogger.Log($"[CheckSpectral] Pack slot {i}: Type={pack.GetPackType()}, Size={pack.GetPackSize()} - CHECKING");
                    
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
                            
                            // Debug what we actually got
                            DebugLogger.Log($"[CheckSpectral]   Card {j}: Type={item.Type}, Value={item.Value:X8}, TypeCategory={item.TypeCategory}");
                            
                            // Check for Soul and BlackHole
                            if (item.Type == MotelyItemType.Soul)
                            {
                                DebugLogger.Log($"[CheckSpectral]   Card {j}: Soul (special)");
                                if (searchAnySpectral || (targetSpectral.HasValue && targetSpectral.Value == MotelySpectralCard.Soul))
                                {
                                    DebugLogger.Log($"[CheckSpectral] FOUND Soul!");
                                    return true;
                                }
                            }
                            else if (item.Type == MotelyItemType.BlackHole)
                            {
                                DebugLogger.Log($"[CheckSpectral]   Card {j}: BlackHole (special)");
                                if (searchAnySpectral || (targetSpectral.HasValue && targetSpectral.Value == MotelySpectralCard.BlackHole))
                                {
                                    DebugLogger.Log($"[CheckSpectral] FOUND BlackHole!");
                                    return true;
                                }
                            }
                            else if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                            {
                                // Regular spectral cards
                                var spectralType = new MotelyItem(item.Value).GetSpectral();
                                DebugLogger.Log($"[CheckSpectral]   Card {j}: {spectralType} (regular spectral)");
                                
                                // Check if it might be BlackHole with wrong enum value
                                if (spectralType == MotelySpectralCard.BlackHole)
                                {
                                    DebugLogger.Log($"[CheckSpectral] WARNING: Found BlackHole as regular spectral enum!");
                                    if (searchAnySpectral || (targetSpectral.HasValue && targetSpectral.Value == MotelySpectralCard.BlackHole))
                                    {
                                        DebugLogger.Log($"[CheckSpectral] FOUND BlackHole!");
                                        return true;
                                    }
                                }
                                
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
                int maxSlots = ante == 1 ? 5 : 6;
                
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
                            // NOTE: BlackHole and Soul NEVER appear in shops, only regular spectral cards
                            var shopSpectral = new MotelyItem(item.Value).GetSpectral();
                            DebugLogger.Log($"[CheckSpectral] Found spectral card in shop slot {i}: {shopSpectral}");
                            
                            // BlackHole and Soul cannot appear in shops
                            if (targetSpectral.HasValue && (targetSpectral.Value == MotelySpectralCard.BlackHole || targetSpectral.Value == MotelySpectralCard.Soul))
                            {
                                DebugLogger.Log($"[CheckSpectral] Note: {targetSpectral} NEVER appears in shops, only in packs!");
                                continue;
                            }
                            
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
                            // NOTE: BlackHole and Soul NEVER appear in shops, only regular spectral cards
                            var shopSpectral = new MotelyItem(item.Value).GetSpectral();
                            DebugLogger.Log($"[CheckSpectral] Found spectral card in shop slot {i}: {shopSpectral}");
                            
                            // BlackHole and Soul cannot appear in shops
                            if (targetSpectral.HasValue && (targetSpectral.Value == MotelySpectralCard.BlackHole || targetSpectral.Value == MotelySpectralCard.Soul))
                            {
                                DebugLogger.Log($"[CheckSpectral] Note: {targetSpectral} NEVER appears in shops, only in packs!");
                                continue;
                            }
                            
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
        
        // REMOVED: TryVectorJokerPreFilter - was unused dead code that created confusion.
        // The actual CheckJoker function already properly handles shopSlots by disabling
        // vectorization and deferring to scalar path when shopSlots are specified.
        
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
            return (global::System.Object?)clause.TagTypeEnum switch
            {
                MotelyTagType.SmallBlind => smallTag == targetTag,
                MotelyTagType.BigBlind => bigTag == targetTag,
                // Any
                _ => smallTag == targetTag || bigTag == targetTag,
            };
        }
        
        // Enhanced single voucher checking with comprehensive debugging
        private static bool CheckVoucherSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState voucherState)
        {
            // Assume validation was done during config parsing
            if (!clause.VoucherEnum.HasValue)
            {
                DebugLogger.Log($"[CheckVoucherSingle] No voucher enum specified for ante {ante}");
                return false;
            }
                
            var targetVoucher = clause.VoucherEnum.Value;
            var currentSeed = ctx.GetSeed();
            
            DebugLogger.Log($"[CheckVoucherSingle] Seed {currentSeed}: Checking ante {ante} for voucher {targetVoucher}");
            DebugLogger.Log($"[CheckVoucherSingle] Seed {currentSeed}: Current voucher state: {voucherState.VoucherBitfield:X8}");
            
            // Check if target voucher is already active
            if (voucherState.IsVoucherActive(targetVoucher))
            {
                DebugLogger.Log($"[CheckVoucherSingle] Seed {currentSeed}: Voucher {targetVoucher} already active - returning true");
                return true;
            }
            
            var voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
            DebugLogger.Log($"[CheckVoucherSingle] Seed {currentSeed}: Found voucher {voucher} in ante {ante}");
            
            if (voucher == targetVoucher)
            {
                DebugLogger.Log($"[CheckVoucherSingle] Seed {currentSeed}: MATCH! Found {targetVoucher} in ante {ante}");
                // Activate the voucher for upgrade vouchers in later antes!
                voucherState.ActivateVoucher(voucher);
                DebugLogger.Log($"[CheckVoucherSingle] Seed {currentSeed}: Activated voucher {voucher}, new state: {voucherState.VoucherBitfield:X8}");
                return true;
            }
            else
            {
                DebugLogger.Log($"[CheckVoucherSingle] Seed {currentSeed}: No match - wanted {targetVoucher}, got {voucher}");
            }
            
            return false;
        }
        
        private static bool CheckPlayingCard(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: true);
            // Use packSlots from config if specified, otherwise use default
            var packSlots = clause.Sources?.PackSlots;
            int packCount = (packSlots != null && packSlots.Length > 0) ? 
                packSlots.Length : // Only iterate through specified pack slots
                (ante == 1 ? 4 : 6); // Default if not specified
            
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