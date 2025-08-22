using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using Motely.Filters.Ouija;
namespace Motely.Filters;

/// <summary>
/// Clean filter descriptor for MongoDB-style queries
/// </summary>
public struct OuijaJsonFilterDesc(bool PrefilterEnabled, OuijaConfig config, Action<string, int, int[]> OnResultFound) : IMotelySeedFilterDesc<OuijaJsonFilterDesc.OuijaJsonFilter>
{
    private readonly OuijaConfig _config = config;
    public int Cutoff { get; set; } = 1;
    public bool AutoCutoff { get; set; } = false;
    
    // Auto cutoff state
    private static int _learnedCutoff = 1;
    
    // Results tracking for rarity calculation
    private static long _resultsFound = 0;
    public static long ResultsFound => _resultsFound;

    public readonly string Name => _config.Name ?? "OuijaJsonFilter";
    public readonly string Description => _config.Description ?? "JSON-configured filter";

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Reset for new search
        _learnedCutoff = Cutoff;
        _resultsFound = 0;
        
        return new OuijaJsonFilter(_config, Cutoff, AutoCutoff);
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
        {
            public static bool IsCancelled;
            private readonly OuijaConfig _config;
            private readonly int _cutoff;
            private readonly bool _autoCutoff;

        public OuijaJsonFilter(OuijaConfig config, int cutoff, bool autoCutoff)
        {
            _config = config;
            _cutoff = cutoff;
            _autoCutoff = autoCutoff;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Copy fields to local variables to avoid struct closure issues
            var config = _config;
            var cutoff = _cutoff;
            var autoCutoff = _autoCutoff;
            
            // PreFilter: Aggressively filter out seeds using vectorized operations
            var voucherState = new MotelyVectorRunStateVoucher();
            var mask = PreFilter(config, ref searchContext, ref voucherState);
            if (mask.IsAllFalse()) 
            {
                DebugLogger.Log("[PreFilter] All seeds filtered out by PreFilter");
                return mask;
            }
            
            DebugLogger.Log("[PreFilter] Seeds passed PreFilter, proceeding to individual processing");
            
            // SearchIndividualSeeds: Handle complex state and scoring for remaining seeds
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                
                // Activate all vouchers for scoring
                var maxVoucherAnte = GetMaxVoucherAnte(config);
                if (maxVoucherAnte > 0)
                {
                    ActivateAllVouchers(ref singleCtx, ref runState, maxVoucherAnte);
                }
                
                // Check remaining MUST clauses (not handled by PreFilter)
                var remainingMustClauses = config.Must?.Where(c => 
                    c.ItemTypeEnum == MotelyFilterItemType.SoulJoker ||
                    c.ItemTypeEnum == MotelyFilterItemType.Joker ||
                    c.ItemTypeEnum == MotelyFilterItemType.PlayingCard) ?? Enumerable.Empty<OuijaConfig.FilterItem>();
                
                foreach (var clause in remainingMustClauses.OrderBy(c => c.EffectiveAntes?.FirstOrDefault() ?? 0))
                {
                    DebugLogger.Log($"[Must] Checking {clause.ItemTypeEnum} {clause.Value} in antes [{string.Join(",", clause.EffectiveAntes ?? new int[0])}]");
                    DebugLogger.Log($"[Must] Showman active: {runState.ShowmanActive}, Owned jokers: {runState.OwnedJokers.Length}");
                    
                    bool clauseResult = CheckSingleClause(ref singleCtx, clause, ref runState);
                    
                    DebugLogger.Log($"[Must] Result: {clauseResult} for {clause.ItemTypeEnum} {clause.Value}");
                    
                    if (!clauseResult)
                    {
                        DebugLogger.Log($"[Must] FAILED! Seed filtered out because {clause.ItemTypeEnum} {clause.Value} not found");
                        return false; // Seed doesn't meet requirements
                    }
                }
                
                // Check all MUST NOT clauses
                if (config.MustNot?.Count > 0)
                {
                    foreach (var clause in config.MustNot)
                    {
                        if (CheckSingleClause(ref singleCtx, clause, ref runState))
                            return false;
                    }
                }
                
                // Calculate scores for SHOULD clauses
                int totalScore = 1; // Base score for passing MUST
                var scores = new List<int>();
                
                if (config.Should?.Count > 0)
                {
                    foreach (var should in config.Should)
                    {
                        int count = CountOccurrences(ref singleCtx, should, ref runState);
                        int score = count * should.Score;
                        scores.Add(count);
                        totalScore += score;
                    }
                }
                
                // Auto cutoff logic
                var currentCutoff = autoCutoff ? GetCurrentCutoff(totalScore) : cutoff;
                
                // Only output if score meets threshold
                if (totalScore >= currentCutoff)
                {
                    // Get the seed string for output  
                    unsafe
                    {
                        char* seedPtr = stackalloc char[9];
                        int len = singleCtx.GetSeed(seedPtr);
                        string seedStr = new string(seedPtr, 0, len);
                        
                        // Output CSV row with scores
                        var row = $"{seedStr},{totalScore}";
                        foreach (var score in scores)
                        {
                            row += $",{score}";
                        }
                        Console.WriteLine(row);
                        
                        // Track results for rarity calculation
                        Interlocked.Increment(ref _resultsFound);
                    }
                }
                
                // Return false to suppress plain seed spam (we handle our own CSV output)
                return false;
            });
        }
        
        private static int GetCurrentCutoff(int currentScore)
        {
            // Thread-safe auto cutoff: Start at 1, raise to highest score found
            if (currentScore > _learnedCutoff)
            {
                var oldCutoff = Interlocked.Exchange(ref _learnedCutoff, currentScore);
                DebugLogger.Log($"[AutoCutoff] Raised cutoff from {oldCutoff} to {currentScore}");
            }
            
            return _learnedCutoff;
        }
        
        /// <summary>
        /// Find the maximum ante needed for voucher checking.
        /// </summary>
        private static int GetMaxVoucherAnte(OuijaConfig config)
        {
            int maxAnte = 0;
            
            // Check Must clauses
            if (config.Must != null)
            {
                foreach (var clause in config.Must.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher))
                {
                    if (clause.EffectiveAntes != null)
                        maxAnte = Math.Max(maxAnte, clause.EffectiveAntes.Max());
                }
            }
            
            // Check Should clauses  
            if (config.Should != null)
            {
                foreach (var clause in config.Should.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher))
                {
                    if (clause.EffectiveAntes != null)
                        maxAnte = Math.Max(maxAnte, clause.EffectiveAntes.Max());
                }
            }
            
            return maxAnte;
        }
        
        /// <summary>
        /// Activate all vouchers from antes 1 through maxAnte to simulate actual game progression.
        /// This is much faster than checking vouchers individually per clause.
        /// </summary>
        private static void ActivateAllVouchers(ref MotelySingleSearchContext ctx, ref MotelyRunState runState, int maxAnte)
        {
            for (int ante = 1; ante <= maxAnte; ante++)
            {
                var voucher = ctx.GetAnteFirstVoucher(ante, runState);
                runState.ActivateVoucher(voucher);
                DebugLogger.Log($"[VoucherActivation] Ante {ante}: Activated {voucher}");
                
                // Special case: Hieroglyph gives a bonus voucher in the SAME ante
                if (voucher == MotelyVoucher.Hieroglyph)
                {
                    // Use a voucher stream to get the NEXT voucher (not the first one again)
                    var voucherStream = ctx.CreateVoucherStream(ante);
                    var bonusVoucher = ctx.GetNextVoucher(ref voucherStream, runState);
                    runState.ActivateVoucher(bonusVoucher);
                    DebugLogger.Log($"[VoucherActivation] Ante {ante}: Hieroglyph bonus activated {bonusVoucher}");
                }
                
            }
        }
        
        /// <summary>
        /// PreFilter: Aggressively filter out seeds using vectorized operations.
        /// Handles ALL Must[] clauses to eliminate 99.9999% of seeds before expensive individual processing.
        /// </summary>
        private static VectorMask PreFilter(OuijaConfig config, ref MotelyVectorSearchContext searchContext, ref MotelyVectorRunStateVoucher voucherState)
        {
            var mask = VectorMask.AllBitsSet;
            
            if (config.Must?.Count == 0) return mask;
            
            // Step 1: Tags (fastest - no state)
            foreach (var clause in config.Must!.Where(c => 
                c.ItemTypeEnum == MotelyFilterItemType.SmallBlindTag ||
                c.ItemTypeEnum == MotelyFilterItemType.BigBlindTag))
            {
                mask &= CheckTag(ref searchContext, clause);
                if (mask.IsAllFalse()) return mask; // Early exit!
            }
            
            // Step 2: Vouchers (build state for later scoring)
            var voucherClauses = config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher) ?? Enumerable.Empty<OuijaConfig.FilterItem>();
            mask = FilterVouchers(voucherClauses, ref searchContext, mask, ref voucherState);
            if (mask.IsAllFalse()) 
            {
                DebugLogger.Log("[PreFilter] Filtered out by voucher requirements");
                return mask;
            }
            
            // Step 3: Tarots (vectorized)
            foreach (var clause in config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.TarotCard) ?? [])
            {
                mask &= CheckTarot(ref searchContext, clause);
                if (mask.IsAllFalse()) return mask;
            }
            
            // Step 4: Planets (vectorized)
            foreach (var clause in config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.PlanetCard) ?? [])
            {
                mask &= CheckPlanet(ref searchContext, clause);
                if (mask.IsAllFalse()) return mask;
            }
            
            // Step 5: Spectrals (vectorized)
            foreach (var clause in config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.SpectralCard) ?? [])
            {
                mask &= CheckSpectral(ref searchContext, clause);
                if (mask.IsAllFalse()) return mask;
            }
            
            // Step 6: Bosses (vectorized)
            foreach (var clause in config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Boss) ?? [])
            {
                mask &= CheckBoss(ref searchContext, clause);
                if (mask.IsAllFalse()) return mask;
            }

            // TODO: Jokers ARE vectorized...
            
            
            return mask;
        }
        
        private static VectorMask FilterVouchers(IEnumerable<OuijaConfig.FilterItem> clausesList, ref MotelyVectorSearchContext searchContext, VectorMask inputMask, ref MotelyVectorRunStateVoucher voucherState)
        {
            var mask = inputMask;
            
            // Find max ante needed
            int maxAnte = 0;
            foreach (var clause in clausesList)
            {
                if (clause.EffectiveAntes != null)
                    maxAnte = Math.Max(maxAnte, clause.EffectiveAntes.Max());
            }
            
            // Create clause result tracking
            var clauseResults = new Dictionary<OuijaConfig.FilterItem, VectorMask>();
            foreach (var clause in clausesList)
            {
                clauseResults[clause] = VectorMask.NoBitsSet; // Start with no matches
            }
            
            // Loop ante 1-N: Build state and score simultaneously
            for (int ante = 1; ante <= maxAnte; ante++)
            {
                var vouchers = searchContext.GetAnteFirstVoucher(ante, voucherState);
                
                DebugLogger.Log($"[FilterVouchers] Ante {ante}: Found vouchers {vouchers}");
                
                // Score: Check each clause to see if this ante satisfies it
                foreach (var clause in clausesList)
                {
                    if (clause.EffectiveAntes?.Contains(ante) == true)
                    {
                        VectorMask matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                        clauseResults[clause] |= matches; // Accumulate OR result
                        
                        DebugLogger.Log($"[FilterVouchers] Ante {ante}: Checking {clause.VoucherEnum.Value}, match: {(matches.IsPartiallyTrue() ? "YES" : "NO")}");
                        
                        // Only activate vouchers that we found and are looking for
                        if (matches.IsPartiallyTrue())
                        {
                            voucherState.ActivateVoucher(clause.VoucherEnum.Value);
                            DebugLogger.Log($"[FilterVouchers] Activated {clause.VoucherEnum.Value} in state");
                            
                            // Special case: Hieroglyph changes the NEXT ante's voucher
                            if (clause.VoucherEnum.Value == MotelyVoucher.Hieroglyph)
                            {
                                DebugLogger.Log($"[FilterVouchers] Hieroglyph activated in ante {ante}, next ante will have upgraded voucher");
                            }
                        }
                    }
                }
            }
            
            // Final check: All clauses must have at least one match
            foreach (var clause in clausesList)
            {
                mask &= clauseResults[clause];
                if (mask.IsAllFalse())
                {
                    DebugLogger.Log($"[FilterVouchers] FAILED: {clause.VoucherEnum.Value} not found in any specified antes");
                    return mask;
                }
                
                DebugLogger.Log($"[FilterVouchers] PASSED: {clause.VoucherEnum.Value} found");
            }
            
            return mask;
        }
        
        private static int GetMaxVoucherAnteFromConfig(OuijaConfig config)
        {
            int maxAnte = 0;
            if (config.Must != null)
            {
                foreach (var clause in config.Must.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher))
                {
                    if (clause.EffectiveAntes != null)
                        maxAnte = Math.Max(maxAnte, clause.EffectiveAntes.Max());
                }
            }
            return maxAnte;
        }

        public bool FilterSingle(ref MotelySingleSearchContext searchContext, ReadOnlySpan<char> seed)
        {
            var runState = new MotelyRunState();
            
            // Check all MUST clauses
            if (_config.Must?.Count > 0)
            {
                foreach (var clause in _config.Must)
                {
                    if (!CheckSingleClause(ref searchContext, clause, ref runState))
                        return false;
                }
            }
            
            // Check all MUST NOT clauses
            if (_config.MustNot?.Count > 0)
            {
                foreach (var clause in _config.MustNot)
                {
                    if (CheckSingleClause(ref searchContext, clause, ref runState))
                        return false;
                }
            }
            
            // Calculate scores for SHOULD clauses
            int totalScore = 1; // Base score for passing MUST
            var scores = new List<int>();
            
            if (_config.Should?.Count > 0)
            {
                foreach (var should in _config.Should)
                {
                    int count = CountOccurrences(ref searchContext, should, ref runState);
                    int score = count * should.Score;
                    scores.Add(count);
                    totalScore += score;
                }
            }
            
            // Output CSV row with scores
            var row = $"{seed},{totalScore}";
            foreach (var score in scores)
            {
                row += $",{score}";
            }
            Console.WriteLine(row);
            
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask ProcessClause(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, VectorMask mask, bool isMust, ref MotelyVectorRunStateVoucher voucherState)
        {
            var result = clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => CheckJoker(ref ctx, clause),
                MotelyFilterItemType.SoulJoker => CheckSoulJoker(ref ctx, clause),
                MotelyFilterItemType.TarotCard => CheckTarot(ref ctx, clause),
                MotelyFilterItemType.PlanetCard => CheckPlanet(ref ctx, clause),
                MotelyFilterItemType.SpectralCard => CheckSpectral(ref ctx, clause),
                MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => CheckTag(ref ctx, clause),
                MotelyFilterItemType.PlayingCard => CheckPlayingCard(ref ctx, clause),
                MotelyFilterItemType.Boss => CheckBoss(ref ctx, clause),
                MotelyFilterItemType.Voucher => CheckVoucherVector(ref ctx, clause, ref voucherState),
                _ => throw new ArgumentOutOfRangeException(nameof(clause.ItemTypeEnum), clause.ItemTypeEnum, null)

            };

            return isMust ? mask & result : mask & (result ^ VectorMask.AllBitsSet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckJoker(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {

            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes)
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                var packStream = ctx.CreateBoosterPackStream(ante);

                if (clause.Sources?.ShopSlots?.Length > 0)
                {
                    var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                    int maxSlot = clause.Sources.ShopSlots.Max();
                    for (int slot = 0; slot <= maxSlot; slot++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        if (!slotSet.Contains(slot) || VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker) == Vector256<int>.Zero) continue;

                        var joker = new MotelyItem(item.Value[0]).GetJoker();
                        var matches = clause.JokerEnum.HasValue ?
                            joker == clause.JokerEnum.Value :
                            CheckWildcardMatch(joker, clause.WildcardEnum);

                        if (matches && CheckEditionAndStickers(new MotelyItem(item.Value[0]), clause))
                        {
                            mask &= VectorMask.AllBitsSet;
                            break;
                        }
                    }
                }

                if (clause.Sources?.PackSlots?.Length > 0)
                {
                    var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                    int maxPacks = clause.Sources.PackSlots.Max() + 1;
                    for (int i = 0; i < maxPacks; i++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        if (VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Buffoon) == Vector256<int>.Zero) continue;
                        if (clause.Sources.RequireMega == true && VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega) == Vector256<int>.Zero) continue;

                        var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize()[0]);
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents[j];
                            var joker = new MotelyItem(item.Value[0]).GetJoker();
                            var matches = clause.JokerEnum.HasValue ?
                                joker == clause.JokerEnum.Value :
                                CheckWildcardMatch(joker, clause.WildcardEnum);

                            if (matches && CheckEditionAndStickers(new MotelyItem(item.Value[0]), clause))
                            {
                                mask &= VectorMask.AllBitsSet;
                                goto NextAnte;
                            }
                        }
                    }
                }

            NextAnte:;
                if (mask.IsAllFalse()) break;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckSoulJoker(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            // For soul jokers, use SearchIndividualSeeds to properly handle pack slots
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                
                // Check all antes specified in the filter
                foreach (var ante in clause.EffectiveAntes)
                {
                    if (CheckSoulJokerSingle(ref singleCtx, clause, ante, ref runState) > 0)
                        return true;
                }
                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckTarot(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.TarotEnum.HasValue) return VectorMask.AllBitsSet;

            // OR logic across antes - tarot can be found in ANY of the specified antes
            var clauseMask = VectorMask.NoBitsSet;
            
            foreach (var ante in clause.EffectiveAntes)
            {
                bool found = CheckShopForTarot(ref ctx, clause, ante) || CheckPacksForTarot(ref ctx, clause, ante);
                if (found)
                {
                    clauseMask = VectorMask.AllBitsSet; // Found in this ante
                    break; // OR logic - found in any ante is enough
                }
            }

            return clauseMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPlanet(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.PlanetEnum.HasValue) return VectorMask.AllBitsSet;

            // OR logic across antes - planet can be found in ANY of the specified antes
            var clauseMask = VectorMask.NoBitsSet;
            
            foreach (var ante in clause.EffectiveAntes)
            {
                bool found = CheckShopForPlanet(ref ctx, clause, ante) || CheckPacksForPlanet(ref ctx, clause, ante);
                if (found)
                {
                    clauseMask = VectorMask.AllBitsSet; // Found in this ante
                    break; // OR logic - found in any ante is enough
                }
            }

            return clauseMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckSpectral(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            // OR logic across antes - spectral can be found in ANY of the specified antes
            var clauseMask = VectorMask.NoBitsSet;
            
            foreach (var ante in clause.EffectiveAntes)
            {
                bool found = CheckShopForSpectral(ref ctx, clause, ante) || CheckPacksForSpectral(ref ctx, clause, ante);
                if (found)
                {
                    clauseMask = VectorMask.AllBitsSet; // Found in this ante
                    break; // OR logic - found in any ante is enough
                }
            }

            return clauseMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckTag(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.TagEnum.HasValue) return VectorMask.AllBitsSet;

            // OR logic across antes - tag can be found in ANY of the specified antes
            var clauseMask = VectorMask.NoBitsSet;
            
            foreach (var ante in clause.EffectiveAntes)
            {
                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                var tagMatches = clause.TagTypeEnum switch
                {
                    MotelyTagType.SmallBlind => VectorEnum256.Equals(smallTag, clause.TagEnum.Value),
                    MotelyTagType.BigBlind => VectorEnum256.Equals(bigTag, clause.TagEnum.Value),
                    _ => VectorEnum256.Equals(smallTag, clause.TagEnum.Value) | VectorEnum256.Equals(bigTag, clause.TagEnum.Value)
                };

                clauseMask |= tagMatches; // OR logic
            }

            return clauseMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckPlayingCard(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes)
            {
                bool found = CheckPacksForPlayingCard(ref ctx, clause, ante);
                if (!found)
                {
                    mask = VectorMask.NoBitsSet;
                    break;
                }
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckBoss(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause)
        {
            if (!clause.BossEnum.HasValue) return VectorMask.AllBitsSet;

            var mask = VectorMask.AllBitsSet;
            foreach (var ante in clause.EffectiveAntes)
            {
                var bossStream = ctx.CreateBossStream(ante);
                var boss = ctx.GetNextBoss(ref bossStream);
                var bossMatches = VectorEnum256.Equals(boss, clause.BossEnum.Value);
                mask &= bossMatches;
                if (mask.IsAllFalse()) break;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask CheckVoucher(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyVectorRunStateVoucher voucherState)
        {
            if (!clause.VoucherEnum.HasValue) return VectorMask.AllBitsSet;

            var voucherStream = ctx.CreateVoucherStream(ante);
            var voucher = ctx.GetNextVoucher(ref voucherStream, voucherState);
            return VectorEnum256.Equals(voucher, clause.VoucherEnum.Value);
        }

        private static VectorMask CheckVoucherVector(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyVectorRunStateVoucher voucherState)
        {
            if (!clause.VoucherEnum.HasValue) return VectorMask.AllBitsSet;

            // OR logic across antes like PerkeoObservatoryDesc pattern
            var clauseMask = VectorMask.NoBitsSet;
            
            foreach (var ante in clause.EffectiveAntes)
            {
                VectorEnum256<MotelyVoucher> vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                DebugLogger.Log($"[CheckVoucherVector] Ante {ante}: Found vouchers {vouchers}, looking for {clause.VoucherEnum.Value}");
                
                VectorMask voucherMatches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                clauseMask |= voucherMatches; // OR logic like CheckTag
                
                voucherState.ActivateVoucher(clause.VoucherEnum.Value);
                DebugLogger.Log($"[CheckVoucherVector] Activated {clause.VoucherEnum.Value} in state for upgrades");
                
            }
            
            DebugLogger.Log($"[CheckVoucherVector] Final result for {clause.VoucherEnum.Value}: {(clauseMask.IsAllFalse() ? "NO MATCH" : "MATCH")}");
            return clauseMask;
        }

        private static int CountOccurrences(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyRunState voucherState)
        {
            // Special case for vouchers: count differently since they should only be counted once
            if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
            {
                return CountVoucherOccurrences(ref ctx, clause, ref voucherState);
            }
            
            int totalCount = 0;
            foreach (var ante in clause.EffectiveAntes)
            {
                var anteCount = clause.ItemTypeEnum switch
                {
                    MotelyFilterItemType.Joker => CheckJokerSingle(ref ctx, clause, ante, ref voucherState),
                    MotelyFilterItemType.SoulJoker => CheckSoulJokerSingle(ref ctx, clause, ante, ref voucherState),
                    MotelyFilterItemType.TarotCard => CheckTarotSingle(ref ctx, clause, ante, ref voucherState) ? 1 : 0,
                    MotelyFilterItemType.PlanetCard => CheckPlanetSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.SpectralCard => CheckSpectralSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.PlayingCard => CheckPlayingCardSingle(ref ctx, clause, ante) ? 1 : 0,
                    MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, clause, ante) ? 1 : 0,
                    _ => 0
                };
                totalCount += anteCount;
            }
            return totalCount;
        }
        
        private static int CountVoucherOccurrences(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyRunState voucherState)
        {
            if (!clause.VoucherEnum.HasValue) return 0;
            
            // Simple: just check if the voucher is active (it was activated during ActivateAllVouchers)
            if (voucherState.IsVoucherActive(clause.VoucherEnum.Value))
            {
                DebugLogger.Log($"[VoucherScoring] {clause.VoucherEnum.Value} is active, giving 1 point");
                return 1;
            }
            
            return 0;
        }

        private static bool CheckSingleClause(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, ref MotelyRunState voucherState)
        {
            if (clause.EffectiveAntes?.Length == 0) return false;

            foreach (var ante in clause.EffectiveAntes)
            {
                var found = clause.ItemTypeEnum switch
                {
                    MotelyFilterItemType.Joker => CheckJokerSingle(ref ctx, clause, ante, ref voucherState) > 0,
                    MotelyFilterItemType.SoulJoker => CheckSoulJokerSingle(ref ctx, clause, ante, ref voucherState) > 0,
                    MotelyFilterItemType.TarotCard => CheckTarotSingle(ref ctx, clause, ante, ref voucherState),
                    MotelyFilterItemType.PlanetCard => CheckPlanetSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.SpectralCard => CheckSpectralSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => CheckTagSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.PlayingCard => CheckPlayingCardSingle(ref ctx, clause, ante),
                    MotelyFilterItemType.Boss => CheckBossSingle(ref ctx, clause, ante),
                    _ => false
                };

                if (found)
                {
                    if (clause.ItemTypeEnum == MotelyFilterItemType.Joker && clause.JokerEnum == MotelyJoker.Showman)
                        voucherState.ActivateShowman();
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckWildcardMatch(MotelyJoker joker, JokerWildcard? wildcard)
        {
            if (!wildcard.HasValue) return false;
            if (wildcard == JokerWildcard.AnyJoker) return true;

            var rarity = (MotelyJokerRarity)((int)joker & Motely.JokerRarityMask);
            return wildcard switch
            {
                JokerWildcard.AnyCommon => rarity == MotelyJokerRarity.Common,
                JokerWildcard.AnyUncommon => rarity == MotelyJokerRarity.Uncommon,
                JokerWildcard.AnyRare => rarity == MotelyJokerRarity.Rare,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckEditionAndStickers(in MotelyItem item, OuijaConfig.FilterItem clause)
        {
            if (clause.EditionEnum.HasValue && item.Edition != clause.EditionEnum.Value)
                return false;

            if (clause.StickerEnums?.Count > 0)
            {
                foreach (var sticker in clause.StickerEnums)
                {
                    var hasSticker = sticker switch
                    {
                        MotelyJokerSticker.Eternal => item.IsEternal,
                        MotelyJokerSticker.Perishable => item.IsPerishable,
                        MotelyJokerSticker.Rental => item.IsRental,
                        _ => true
                    };
                    if (!hasSticker) return false;
                }
            }

            return true;
        }

        private static bool CheckArcanaForSoul(ref MotelyVectorSearchContext ctx, int ante, MotelyBoosterPackSize size)
        {
            var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
            var contents = ctx.GetNextArcanaPackContents(ref tarotStream, size);
            for (int i = 0; i < contents.Length; i++)
                    {
                        if (VectorEnum256.Equals(contents[i].Type, MotelyItemType.Soul) != Vector256<int>.Zero)
                    return true;
            }
            return false;
        }

        private static bool CheckSpectralForSoul(ref MotelyVectorSearchContext ctx, int ante, MotelyBoosterPackSize size)
        {
            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
            var contents = ctx.GetNextSpectralPackContents(ref spectralStream, size);
            for (int i = 0; i < contents.Length; i++)
                    {
                        if (VectorEnum256.Equals(contents[i].Type, MotelyItemType.Soul) != Vector256<int>.Zero)
                    return true;
            }
            return false;
        }

        private static bool CheckShopForTarot(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.ShopSlots?.Length == 0) return false;

            var shopStream = ctx.CreateShopItemStream(ante);
            var slots = clause.Sources?.ShopSlots ?? new[] { 0, 1, 2, 3, 4, 5 };
            var slotSet = new HashSet<int>(slots);

            for (int i = 0; i < slots.Max(); i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (slotSet.Contains(i) && VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.TarotCard) != Vector256<int>.Zero)
                {
                    var tarot = new MotelyItem(item.Value[0]).GetTarot();
                    Debug.Assert(clause.TarotEnum.HasValue, "TarotEnum should be set for tarot card checks");
                    if (tarot == clause.TarotEnum.Value)
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPacksForTarot(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.PackSlots?.Length == 0) return false;

            var packStream = ctx.CreateBoosterPackStream(ante);
            Debug.Assert(clause.TarotEnum.HasValue, "TarotEnum should be set for tarot card checks");
            Debug.Assert(clause.Sources != null, "Sources should be set for tarot card checks");
            var packSlots = clause.Sources.PackSlots;

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots != null && packSlots.Contains(i) && VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Arcana) != Vector256<int>.Zero)
                {
                    if (clause.Sources?.RequireMega == true && VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega) == Vector256<int>.Zero) continue;

                    var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                    var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize()[0]);
                    for (int j = 0; j < contents.Length; j++)
                    {
                        if (VectorEnum256.Equals(contents[j].Type, (MotelyItemType)clause.TarotEnum.Value) != Vector256<int>.Zero)
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool CheckShopForPlanet(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.ShopSlots?.Length == 0) return false;

            var shopStream = ctx.CreateShopItemStream(ante);
            var slots = clause.Sources?.ShopSlots ?? new[] { 0, 1, 2, 3, 4, 5 };
            var slotSet = new HashSet<int>(slots);

            for (int i = 0; i < slots.Max(); i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (slotSet.Contains(i) && VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.PlanetCard) != Vector256<int>.Zero)
                {
                    var planet = new MotelyItem(item.Value[0]).GetPlanet();
                    if (planet == clause.PlanetEnum.Value)
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPacksForPlanet(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.PackSlots?.Length == 0) return false;

            var packStream = ctx.CreateBoosterPackStream(ante);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots != null && packSlots.Contains(i) && VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Celestial) != Vector256<int>.Zero)
                {
                    if (clause.Sources?.RequireMega == true && VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega) == Vector256<int>.Zero) continue;

                    var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                    var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize()[0]);
                    for (int j = 0; j < contents.Length; j++)
                    {
                        if (VectorEnum256.Equals(contents[j].Type, (MotelyItemType)clause.PlanetEnum.Value) != Vector256<int>.Zero)
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool CheckShopForSpectral(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.ShopSlots?.Length == 0) return false;

            var shopStream = ctx.CreateShopItemStream(ante);
            var slots = clause.Sources?.ShopSlots ?? new[] { 0, 1, 2, 3, 4, 5 };
            var slotSet = new HashSet<int>(slots);

            for (int i = 0; i < slots.Max(); i++)
            {
                var item = ctx.GetNextShopItem(ref shopStream);
                if (slotSet.Contains(i) && VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.SpectralCard) != Vector256<int>.Zero)
                {
                    if (clause.SpectralEnum.HasValue)
                    {
                        var spectral = new MotelyItem(item.Value[0]).GetSpectral();
                        if (spectral == clause.SpectralEnum.Value)
                            return true;
                    }
                    else
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPacksForSpectral(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (clause.Sources?.PackSlots?.Length == 0) return false;

            var packStream = ctx.CreateBoosterPackStream(ante);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots != null && packSlots.Contains(i) && VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Spectral) != Vector256<int>.Zero)
                {
                    if (clause.Sources?.RequireMega == true && VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega) == Vector256<int>.Zero) continue;

                    var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
                    var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize()[0]);
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents[j];
                        if (VectorEnum256.Equals(item.Type, MotelyItemType.Soul) != Vector256<int>.Zero || VectorEnum256.Equals(item.Type, MotelyItemType.BlackHole) != Vector256<int>.Zero)
                        {
                            if (!clause.SpectralEnum.HasValue ||
                                (VectorEnum256.Equals(item.Type, MotelyItemType.Soul) != Vector256<int>.Zero && clause.SpectralEnum == MotelySpectralCard.Soul) ||
                                (VectorEnum256.Equals(item.Type, MotelyItemType.BlackHole) != Vector256<int>.Zero && clause.SpectralEnum == MotelySpectralCard.BlackHole))
                                return true;
                        }
                        else if (VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.SpectralCard) != Vector256<int>.Zero)
                        {
                            if (!clause.SpectralEnum.HasValue)
                                return true;
                            var spectral = new MotelyItem(item.Value[0]).GetSpectral();
                            if (spectral == clause.SpectralEnum.Value)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool CheckPacksForPlayingCard(ref MotelyVectorSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var packStream = ctx.CreateBoosterPackStream(ante);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };

            for (int i = 0; i < (ante == 1 ? 4 : 6); i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots != null && packSlots.Contains(i) && VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Standard) != Vector256<int>.Zero)
                {
                    if (clause.Sources?.RequireMega == true && VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega) == Vector256<int>.Zero) continue;

                    var cardStream = ctx.CreateStandardPackCardStream(ante);
                    var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize()[0]);
                    for (int j = 0; j < contents.Length; j++)
                    {
                        var item = contents.GetItem(j);
                        var isPlayingCard = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.PlayingCard) != Vector256<int>.Zero;
                        var suitMatches = !clause.SuitEnum.HasValue || VectorEnum256.Equals(item.PlayingCardSuit, clause.SuitEnum.Value) != Vector256<int>.Zero;
                        var rankMatches = !clause.RankEnum.HasValue || VectorEnum256.Equals(item.PlayingCardRank, clause.RankEnum.Value) != Vector256<int>.Zero;
                        var enhancementMatches = !clause.EnhancementEnum.HasValue || VectorEnum256.Equals(item.Enhancement, clause.EnhancementEnum.Value) != Vector256<int>.Zero;
                        var sealMatches = !clause.SealEnum.HasValue || VectorEnum256.Equals(item.Seal, clause.SealEnum.Value) != Vector256<int>.Zero;
                        var editionMatches = !clause.EditionEnum.HasValue || VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value) != Vector256<int>.Zero;
                        
                        if (isPlayingCard && suitMatches && rankMatches && enhancementMatches && sealMatches && editionMatches)
                            return true;
                    }
                }
            }
            return false;
        }

        private static int CheckJokerSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState runState)
        {
            int foundCount = 0;
            var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.Joker)
                    {
                        var joker = new MotelyItem(item.Value).GetJoker();
                        var matches = clause.JokerEnum.HasValue ?
                            joker == clause.JokerEnum.Value :
                            CheckWildcardMatch(joker, clause.WildcardEnum);
                        if (matches && CheckEditionAndStickers(item, clause))
                        {
                            // Track this joker as owned
                            runState.AddOwnedJoker(item);
                            
                            // Track Showman ONLY if we're searching for Showman
                            if (item.Type == MotelyItemType.Showman && clause.JokerEnum == MotelyJoker.Showman)
                            {
                                runState.ActivateShowman();
                                DebugLogger.Log($"[Joker] Activated Showman - duplicates now allowed!");
                            }
                            
                            foundCount++;
                            if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                                return foundCount;
                        }
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                var packSlots = clause.Sources.PackSlots;
                int maxPackSlot = packSlots.Max();
                for (int i = 0; i <= maxPackSlot; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (packSlots != null && packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents[j];
                            var joker = item.GetJoker();
                            var matches = clause.JokerEnum.HasValue ?
                                joker == clause.JokerEnum.Value :
                                CheckWildcardMatch(joker, clause.WildcardEnum);
                            if (matches && CheckEditionAndStickers(item, clause))
                            {
                                // Track this joker as owned
                                runState.AddOwnedJoker(item);
                                
                                // Track Showman ONLY if we're searching for Showman
                                if (item.Type == MotelyItemType.Showman && clause.JokerEnum == MotelyJoker.Showman)
                                {
                                    runState.ActivateShowman();
                                    DebugLogger.Log($"[Joker] Activated Showman from pack - duplicates now allowed!");
                                }
                                
                                foundCount++;
                                if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                                    return foundCount;
                            }
                        }
                    }
                }
            }

            return foundCount;
        }

        private static int CheckSoulJokerSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState runState)
        {
            int foundCount = 0;
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
            var soulStream = ctx.CreateSoulJokerStream(ante, MotelyJokerStreamFlags.Default);
            bool soulStreamInit = false;

            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };
            int packCount = packSlots.Length > 0 ? packSlots.Max() + 1 : (ante == 1 ? 4 : 6);

            if (DebugLogger.IsEnabled)
            {
                DebugLogger.Log($"[SoulJoker] Checking ante {ante}, slots [{string.Join(",", packSlots)}], packCount: {packCount}, target: {clause.Value ?? "any"}");
            }

            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                
                if (DebugLogger.IsEnabled)
                {
                    DebugLogger.Log($"[SoulJoker] Ante {ante} Pack {i}: Type={pack.GetPackType()}, Size={pack.GetPackSize()}");
                }
                
                if (packSlots != null && packSlots.Contains(i) && (pack.GetPackType() == MotelyBoosterPackType.Arcana || pack.GetPackType() == MotelyBoosterPackType.Spectral))
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    bool hasSoul = false;
                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: true);
                        var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                        
                        if (DebugLogger.IsEnabled)
                        {
                            var itemList = new List<string>();
                            for (int k = 0; k < contents.Length; k++)
                                itemList.Add(contents[k].ToString());
                            DebugLogger.Log($"[SoulJoker] Arcana pack contents: [{string.Join(",", itemList)}]");
                        }
                        
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (contents[j] == MotelyItemType.Soul)
                            {
                                hasSoul = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
                        var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                        
                        if (DebugLogger.IsEnabled)
                        {
                            var itemList = new List<string>();
                            for (int k = 0; k < contents.Length; k++)
                                itemList.Add(contents[k].ToString());
                            DebugLogger.Log($"[SoulJoker] Spectral pack contents: [{string.Join(",", itemList)}]");
                        }
                        
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (contents[j] == MotelyItemType.Soul)
                            {
                                hasSoul = true;
                                break;
                            }
                        }
                    }

                    if (hasSoul)
                    {
                        // Check if this soul pack has already been consumed by another clause
                        if (runState.IsSoulPackConsumed(ante, i))
                        {
                            if (DebugLogger.IsEnabled)
                            {
                                DebugLogger.Log($"[SoulJoker] Pack {i} soul already consumed by another clause, skipping");
                            }
                            continue;
                        }
                        
                        if (!soulStreamInit)
                        {
                            soulStreamInit = true;
                        }
                        
                        // Trust Motely to handle duplicate prevention automatically
                        var soulJoker = ctx.GetNextJoker(ref soulStream);
                        
                        if (DebugLogger.IsEnabled)
                        {
                            DebugLogger.Log($"[SoulJoker] Found Soul! Joker = {soulJoker.Type}, Edition = {soulJoker.Edition}");
                        }
                        
                        var matches = !clause.JokerEnum.HasValue || soulJoker.Type == new MotelyItem(clause.JokerEnum.Value).Type;

                        if (DebugLogger.IsEnabled)
                        {
                            if (matches)
                            {
                                DebugLogger.Log($"[SoulJoker] Type matches! Checking edition/stickers...");
                            }
                            else
                            {
                                DebugLogger.Log($"[SoulJoker] Type does not match! Looking for {new MotelyItem(clause.JokerEnum.Value).Type}... but found {soulJoker.Type}");
                            }
                        }
                        
                        if (matches && CheckEditionAndStickers(soulJoker, clause))
                        {
                            // ONLY mark pack as consumed when we actually match what we're looking for
                            runState.MarkSoulPackConsumed(ante, i);
                            
                            // Track this matching joker as owned
                            runState.AddOwnedJoker(soulJoker);

                            foundCount++;
                            DebugLogger.Log($"[SoulJoker] *** MATCH FOUND *** Type: {soulJoker.Type}, Edition: {soulJoker.Edition}, Count: {foundCount}");
                            DebugLogger.Log($"[SoulJoker] Marked pack {i} as consumed");

                            // Track Showman ONLY if we're searching for Showman
                            if (soulJoker.Type == MotelyItemType.Showman && clause.JokerEnum == MotelyJoker.Showman)
                            {
                                runState.ActivateShowman();
                                DebugLogger.Log($"[SoulJoker] Activated Showman - duplicates now allowed!");
                            }

                            if (clause.Min.HasValue && foundCount >= clause.Min.Value)
                                return foundCount;
                        }
                    }
                }
            }

            return foundCount;
        }

        private static bool CheckTarotSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState runState)
        {
            if (!clause.TarotEnum.HasValue) return false;

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.TarotCard)
                    {
                        var tarot = new MotelyItem(item.Value).GetTarot();
                        if (tarot == clause.TarotEnum.Value)
                            return true;
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
                var packSlots = clause.Sources.PackSlots;
                int packCount = packSlots.Max() + 1;
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (packSlots != null && packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                        var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (contents[j].Type == (MotelyItemType)clause.TarotEnum.Value)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool CheckPlanetSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!clause.PlanetEnum.HasValue) return false;

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.PlanetCard)
                    {
                        var planet = new MotelyItem(item.Value).GetPlanet();
                        if (planet == clause.PlanetEnum.Value)
                            return true;
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
                var packSlots = clause.Sources.PackSlots;
                int packCount = packSlots.Max() + 1;
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (packSlots != null && packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Celestial)
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                        var contents = ctx.GetNextCelestialPackContents(ref planetStream, pack.GetPackSize());
                        for (int j = 0; j < contents.Length; j++)
                        {
                            if (contents[j].Type == (MotelyItemType)clause.PlanetEnum.Value)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool CheckSpectralSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            bool searchAnySpectral = !clause.SpectralEnum.HasValue;

            if (clause.Sources?.ShopSlots?.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante, isCached: false);
                var slotSet = new HashSet<int>(clause.Sources.ShopSlots);
                int maxSlot = clause.Sources.ShopSlots.Max();
                for (int i = 0; i <= maxSlot; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (slotSet.Contains(i) && item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                    {
                        if (searchAnySpectral)
                            return true;
                        var spectral = new MotelyItem(item.Value).GetSpectral();
                        Debug.Assert(clause.SpectralEnum != null, "Spectral card must provide a 'value' field, or use the Wildcard keyword: 'Any'");
                        if (spectral == clause.SpectralEnum.Value)
                            return true;
                    }
                }
            }

            if (clause.Sources?.PackSlots?.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante, isCached: false);
                var packSlots = clause.Sources.PackSlots;
                int packCount = packSlots.Max() + 1;
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    // Soul can appear in both Arcana and Spectral packs!
                    if (packSlots != null && packSlots.Contains(i) && 
                        (pack.GetPackType() == MotelyBoosterPackType.Spectral || pack.GetPackType() == MotelyBoosterPackType.Arcana))
                    {
                        if (clause.Sources.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                        if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                        {
                            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: false);
                            var contents = ctx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
                            for (int j = 0; j < contents.Length; j++)
                            {
                                var item = contents[j];
                                if (item.Type == MotelyItemType.Soul || item.Type == MotelyItemType.BlackHole)
                                {
                                    if (searchAnySpectral ||
                                        (item.Type == MotelyItemType.Soul && clause.SpectralEnum == MotelySpectralCard.Soul) ||
                                        (item.Type == MotelyItemType.BlackHole && clause.SpectralEnum == MotelySpectralCard.BlackHole))
                                        return true;
                                }
                                else if (item.TypeCategory == MotelyItemTypeCategory.SpectralCard)
                                {
                                    if (searchAnySpectral)
                                        return true;
                                    var spectral = new MotelyItem(item.Value).GetSpectral();
                                    Debug.Assert(clause.SpectralEnum != null, "Spectral card must provide a 'value' field, or use the Wildcard keyword: 'Any'");
                                    if (spectral == clause.SpectralEnum.Value)
                                        return true;
                                }
                            }
                        }
                        else if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                        {
                            var tarotStream = ctx.CreateArcanaPackTarotStream(ante, soulOnly: false);
                            var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                            for (int j = 0; j < contents.Length; j++)
                            {
                                var item = contents[j];
                                if (item.Type == MotelyItemType.Soul)
                                {
                                    if (searchAnySpectral || clause.SpectralEnum == MotelySpectralCard.Soul)
                                        return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool CheckTagSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!clause.TagEnum.HasValue) return false;

            var tagStream = ctx.CreateTagStream(ante);
            var smallTag = ctx.GetNextTag(ref tagStream);
            var bigTag = ctx.GetNextTag(ref tagStream);

            return clause.TagTypeEnum switch
            {
                MotelyTagType.SmallBlind => smallTag == clause.TagEnum.Value,
                MotelyTagType.BigBlind => bigTag == clause.TagEnum.Value,
                _ => smallTag == clause.TagEnum.Value || bigTag == clause.TagEnum.Value
            };
        }

        private static bool CheckVoucherSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante, ref MotelyRunState voucherState)
        {
            if (!clause.VoucherEnum.HasValue) return false;

            // Simple: get voucher for this ante and check if it matches
            var voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
            if (voucher == clause.VoucherEnum.Value)
            {
                voucherState.ActivateVoucher(voucher);
                DebugLogger.Log($"[CheckVoucherSingle] Found {voucher} in ante {ante}");
                return true;
            }
            
            return false;
        }

        private static bool CheckPlayingCardSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            var packStream = ctx.CreateBoosterPackStream(ante, isCached: true);
            var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3 };
            int packCount = packSlots.Max() + 1;

            for (int i = 0; i < packCount; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (packSlots != null && packSlots.Contains(i) && pack.GetPackType() == MotelyBoosterPackType.Standard)
                {
                    if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega) continue;

                    // TODO not advancing stream correctly
                    var cardStream = ctx.CreateStandardPackCardStream(ante);
                    var contents = ctx.GetNextStandardPackContents(ref cardStream, pack.GetPackSize());
                    for (int j = 0; j < contents.Length; j++)
                        {
                            var item = contents[j];
                        if (item.TypeCategory == MotelyItemTypeCategory.PlayingCard &&
                            (!clause.SuitEnum.HasValue || item.PlayingCardSuit == clause.SuitEnum.Value) &&
                            (!clause.RankEnum.HasValue || item.PlayingCardRank == clause.RankEnum.Value) &&
                            (!clause.EnhancementEnum.HasValue || item.Enhancement == clause.EnhancementEnum.Value) &&
                            (!clause.SealEnum.HasValue || item.Seal == clause.SealEnum.Value) &&
                            (!clause.EditionEnum.HasValue || item.Edition == clause.EditionEnum.Value))
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckBossSingle(ref MotelySingleSearchContext ctx, OuijaConfig.FilterItem clause, int ante)
        {
            if (!clause.BossEnum.HasValue) return false;
            return ctx.GetBossForAnte(ante) == clause.BossEnum.Value;
        }
    }
}
