using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Universal filter descriptor that can filter any category and chain to itself
/// Single class handles all filter types with category-specific optimization
/// </summary>
public struct MotelyJsonFilterDesc(
    FilterCategory category,
    List<MotelyJsonConfig.MotleyJsonFilterClause> clauses
)
    : IMotelySeedFilterDesc<MotelyJsonFilterDesc.MotelyFilter>
{
    private readonly FilterCategory _category = category;
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses = clauses ?? new List<MotelyJsonConfig.MotleyJsonFilterClause>();

    public readonly string Name => $"Everybody loves Wee Joker";
    public readonly string Description => $"pifreak loves you!";

    public MotelyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Log filter creation for debugging
        DebugLogger.Log($"Creating MotelyJsonFilter: Category={_category}, Clauses={Clauses.Count}");
        if (Clauses != null)
        {
            foreach (var clause in Clauses)
            {
                DebugLogger.Log($"  - {clause.ItemTypeEnum}: {clause.Value} @ antes {string.Join(",", clause.EffectiveAntes ?? new int[0])}");
            }
        }
        
        // Cache relevant streams based on what clauses need - no LINQ!
        bool[] antesNeeded = new bool[9]; // Antes 1-8
        
        if (Clauses != null)
        {
            foreach (var clause in Clauses)
            {
                if (clause.EffectiveAntes != null)
                {
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        if (ante >= 1 && ante <= 8)
                            antesNeeded[ante] = true;
                    }
                }
            }
        }
        
        // Cache based on category
        if (_category == FilterCategory.Voucher || _category == FilterCategory.Mixed)
        {
            for (int ante = 1; ante <= 8; ante++)
            {
                if (antesNeeded[ante])
                    ctx.CacheAnteFirstVoucher(ante);
            }
        }
        
        if (_category == FilterCategory.Joker || _category == FilterCategory.Mixed)
        {
            for (int ante = 1; ante <= 8; ante++)
            {
                if (antesNeeded[ante])
                {
                    ctx.CacheBoosterPackStream(ante);
                    // Also cache shop jokers if any clauses need shop slots
                    bool needShopJokers = false;
                    if (Clauses != null)
                    {
                        foreach (var clause in Clauses)
                        {
                            if (clause.Sources?.ShopSlots?.Length > 0)
                            {
                                needShopJokers = true;
                                break;
                            }
                        }
                    }
                    if (needShopJokers)
                    {
                        ctx.CacheShopJokerStream(ante);
                    }
                }
            }
        }
        
        return new MotelyFilter(_category, Clauses);
    }

    // Precomputed voucher clause for hot path
    private readonly struct OptimizedVoucherClause
    {
        public readonly MotelyVoucher Voucher;
        public readonly int[] Antes;
        
        public OptimizedVoucherClause(MotelyJsonConfig.MotleyJsonFilterClause clause)
        {
            Voucher = clause.VoucherEnum!.Value;
            Antes = clause.EffectiveAntes ?? Array.Empty<int>();
        }
    }
    
    public struct MotelyFilter : IMotelySeedFilter
    {
        private readonly FilterCategory _category;
        private List<MotelyJsonConfig.MotleyJsonFilterClause>? Clauses; // Make nullable
        private readonly OptimizedVoucherClause[]? _voucherClauses;
        private readonly int _maxAnte;
        
        public MotelyFilter(FilterCategory category, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
        {
            _category = category;
            Clauses = clauses ?? new List<MotelyJsonConfig.MotleyJsonFilterClause>();
            
            // Precompute voucher clauses for hot path - no LINQ allocations!
            int voucherCount = 0;
            if (Clauses != null)
            {
                foreach (var clause in Clauses)
                {
                    if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher && clause.VoucherEnum.HasValue)
                        voucherCount++;
                }
            }
            
            if (voucherCount > 0)
            {
                _voucherClauses = new OptimizedVoucherClause[voucherCount];
                int index = 0;
                foreach (var clause in Clauses)
                {
                    if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher && clause.VoucherEnum.HasValue)
                    {
                        _voucherClauses[index++] = new OptimizedVoucherClause(clause);
                    }
                }
            }
            else
            {
                _voucherClauses = null;
            }
            
            // Pre-compute max ante to avoid doing it every time
            _maxAnte = 1;
            if (Clauses != null && Clauses.Count > 0)
            {
                foreach (var clause in Clauses)
                {
                    if (clause.EffectiveAntes != null && clause.EffectiveAntes.Length > 0)
                    {
                        foreach (var ante in clause.EffectiveAntes)
                        {
                            if (ante > _maxAnte) _maxAnte = ante;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Handle default-constructed struct
            if (Clauses == null)
            {
                DebugLogger.Log($"=== MotelyJsonFilter.Filter called with null Clauses (default struct?) ===");
                return VectorMask.AllBitsSet;
            }
            
            DebugLogger.Log($"=== MotelyJsonFilter.Filter called: Category={_category}, Clauses={Clauses.Count} ===");
            
            // If no clauses, pass everything through (for scoreOnly mode)
            // Don't call ANY filter methods if there's nothing to filter!
            if (Clauses.Count == 0)
            {
                DebugLogger.Log("  No clauses, passing all seeds through");
                return VectorMask.AllBitsSet;
            }

            DebugLogger.Log($"  Dispatching to {_category} filter method...");
            var result = _category switch
            {
                FilterCategory.Voucher => FilterVouchers(ref searchContext),
                FilterCategory.Tag => FilterTags(ref searchContext),
                FilterCategory.TarotCard => FilterTarots(ref searchContext),
                FilterCategory.PlanetCard => FilterPlanets(ref searchContext),
                FilterCategory.SpectralCard => FilterSpectrals(ref searchContext),
                FilterCategory.Joker => FilterJokers(ref searchContext),
                FilterCategory.PlayingCard => FilterPlayingCards(ref searchContext),
                FilterCategory.Boss => FilterBosses(ref searchContext),
                FilterCategory.Mixed => FilterMixed(ref searchContext),
                _ => throw new ArgumentException($"Unknown filter category: {_category}")
            };
            
            if (!result.IsAllFalse())
            {
                DebugLogger.Log($"  ✓ Filter result: Some seeds passed!");
            }
            else
            {
                DebugLogger.Log($"  ✗ Filter result: No seeds passed");
            }
            
            return result;
        }

        #region Vector Filtering Methods
        
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private VectorMask FilterMixed(ref MotelyVectorSearchContext ctx)
        {
            // This shouldn't be called with proper slicing!
            // But if it is, handle it properly
            
            // Empty filter - pass everything through
            if (Clauses == null || Clauses.Count == 0)
                return VectorMask.AllBitsSet;
                
            DebugLogger.Log($"FilterMixed called with {Clauses.Count} clauses - delegating to proper filters");
            
            // Check what we have
            bool hasVouchers = false;
            bool hasJokers = false;
            
            foreach (var clause in Clauses)
            {
                if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
                    hasVouchers = true;
                else if (clause.ItemTypeEnum == MotelyFilterItemType.Joker || clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                    hasJokers = true;
            }
            
            // Route to the appropriate filter
            if (hasVouchers && !hasJokers)
            {
                return FilterVouchers(ref ctx);
            }
            else if (hasJokers && !hasVouchers)
            {
                return FilterJokers(ref ctx);
            }
            else if (hasVouchers && hasJokers)
            {
                // Both - apply vouchers first then jokers
                var result = FilterVouchers(ref ctx);
                if (!result.IsAllFalse())
                {
                    result &= FilterJokers(ref ctx);
                }
                return result;
            }
            
            // Unknown clause types
            DebugLogger.Log($"WARNING: FilterMixed has unknown clause types");
            return VectorMask.AllBitsSet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private VectorMask FilterVouchers(ref MotelyVectorSearchContext ctx)
        {
            if (_voucherClauses == null || _voucherClauses.Length == 0)
            {
                DebugLogger.Log("FilterVouchers: No voucher clauses, passing all");
                return VectorMask.AllBitsSet;
            }
                
            DebugLogger.Log($"FilterVouchers: Checking {_voucherClauses.Length} voucher clauses");
            var mask = VectorMask.AllBitsSet;
            var state = new MotelyVectorRunState();
            
            // CRITICAL: Process vouchers in ANTE order, not clause order!
            // This ensures Telescope is activated before checking for Observatory
            var clauseMasks = new VectorMask[_voucherClauses.Length];
            
            // Process each ante in order
            for (int ante = 1; ante <= 8; ante++)
            {
                // Get the voucher for this ante ONCE
                var vouchers = ctx.GetAnteFirstVoucher(ante, state);
                
                // Now check all voucher clauses that care about this ante
                for (int i = 0; i < _voucherClauses.Length; i++)
                {
                    ref readonly var clause = ref _voucherClauses[i];
                    
                    // Skip if this clause doesn't check this ante
                    bool checkThisAnte = false;
                    foreach (var a in clause.Antes)
                    {
                        if (a == ante)
                        {
                            checkThisAnte = true;
                            break;
                        }
                    }
                    if (!checkThisAnte) continue;
                    
                    VectorMask matches = VectorEnum256.Equals(vouchers, clause.Voucher);
                    
                    clauseMasks[i] |= matches;  // OR - voucher can appear at any of its antes
                }
                
                // CRITICAL: Activate whatever voucher we found AFTER checking - this updates the state for FUTURE antes!
                // This ensures that if we find Telescope in ante 3, Observatory can appear in antes 4+
                state.ActivateVoucher(vouchers);
            }
            
            // Now check that all required vouchers were found
            for (int i = 0; i < _voucherClauses.Length; i++)
            {
                if (clauseMasks[i].IsAllFalse())
                {
                    DebugLogger.Log($"  Voucher {_voucherClauses[i].Voucher} not found in any ante, failing batch");
                    return VectorMask.NoBitsSet;  // Required voucher not found
                }
                mask &= clauseMasks[i];  // AND - all required vouchers must be present
                
                // Early exit if no seeds left
                if (mask.IsAllFalse())
                {
                    DebugLogger.Log($"  No seeds have all required vouchers");
                    return VectorMask.NoBitsSet;
                }
            }

            if (!mask.IsAllFalse())
            {
                // Count matching seeds (VectorMask is a wrapper around Vector512<double>)
                DebugLogger.Log($"  FilterVouchers: Some seeds passed all voucher checks in this batch");
            }
            
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterTags(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(Tag) called with empty clauses");
            var mask = VectorMask.AllBitsSet;

            foreach (var clause in Clauses)
            {
                Debug.Assert(clause.TagEnum.HasValue, "FilterTags requires TagEnum");
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

                    clauseMask |= tagMatches;
                }

                mask &= clauseMask;
                if (mask.IsAllFalse()) break;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterTarots(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(TarotCard) called with empty clauses");
            
            var mask = VectorMask.AllBitsSet;
            
            foreach (var clause in Clauses)
            {
                var clauseMask = VectorMask.NoBitsSet;
                
                // Handle wildcards with scalar fallback
                if (clause.IsWildcard || !clause.TarotEnum.HasValue)
                {
                    var localClause = clause;
                    return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
                    {
                        var tempState = new MotelyRunState();
                        foreach (var ante in localClause.EffectiveAntes ?? [])
                        {
                            if (MotelyJsonScoring.TarotCardsTally(ref singleCtx, localClause, ante, ref tempState, earlyExit: true) > 0)
                                return true;
                        }
                        return false;
                    });
                }
                
                var targetItemType = (MotelyItemType)clause.TarotEnum.Value;
                
                foreach (var ante in clause.EffectiveAntes ?? [])
                {
                    // Check shops
                    var shopSlots = clause.Sources?.ShopSlots;
                    if (shopSlots != null && shopSlots.Length > 0)
                    {
                        var shopStream = ctx.CreateShopItemStream(ante, 
                            MotelyShopStreamFlags.ExcludeJokers | MotelyShopStreamFlags.ExcludePlanets | MotelyShopStreamFlags.ExcludeSpectrals);
                        
                        int maxSlot = shopSlots.Max();
                        for (int i = 0; i <= maxSlot; i++)
                        {
                            var item = ctx.GetNextShopItem(ref shopStream);
                            if (shopSlots.Contains(i))
                            {
                                var matches = VectorEnum256.Equals(item.Type, targetItemType);
                                if (clause.EditionEnum.HasValue)
                                    matches &= VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
                                clauseMask |= matches;
                            }
                        }
                    }
                    
                    // Check arcana packs
                    var packSlots = clause.Sources?.PackSlots;
                    if (packSlots != null && packSlots.Length > 0)
                    {
                        var boosterStream = ctx.CreateBoosterPackStream(ante);
                        var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                        
                        int maxPackSlot = packSlots.Max();
                        for (int i = 0; i <= maxPackSlot; i++)
                        {
                            var pack = ctx.GetNextBoosterPack(ref boosterStream);
                            if (packSlots.Contains(i))
                            {
                                VectorMask isArcana = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Arcana);
                                if (isArcana.IsAllFalse()) continue;
                                
                                // Handle pack sizes - most packs are normal size
                                var normalSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Normal);
                                var jumboSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Jumbo);
                                var megaSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega);
                                
                                // Get contents for normal size (most common)
                                var contents = ctx.GetNextArcanaPackContents(ref tarotStream, MotelyBoosterPackSize.Normal);
                                for (int j = 0; j < contents.Length; j++)
                                {
                                    var matches = VectorEnum256.Equals(contents[j].Type, targetItemType);
                                    if (clause.EditionEnum.HasValue)
                                        matches &= VectorEnum256.Equals(contents[j].Edition, clause.EditionEnum.Value);
                                    clauseMask |= matches;
                                }
                            }
                        }
                    }
                }
                
                mask &= clauseMask;
                if (mask.IsAllFalse()) return mask;
            }
            
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterPlanets(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(PlanetCard) called with empty clauses");
            
            var mask = VectorMask.AllBitsSet;
            
            foreach (var clause in Clauses)
            {
                var clauseMask = VectorMask.NoBitsSet;
                
                // Handle wildcards with scalar fallback
                if (clause.IsWildcard || !clause.PlanetEnum.HasValue)
                {
                    var localClause = clause;
                    return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
                    {
                        foreach (var ante in localClause.EffectiveAntes ?? [])
                        {
                            if (MotelyJsonScoring.CountPlanetOccurrences(ref singleCtx, localClause, ante, earlyExit: true) > 0)
                                return true;
                        }
                        return false;
                    });
                }
                
                var targetItemType = (MotelyItemType)clause.PlanetEnum.Value;
                
                foreach (var ante in clause.EffectiveAntes ?? [])
                {
                    // Check shops
                    var shopSlots = clause.Sources?.ShopSlots;
                    if (shopSlots != null && shopSlots.Length > 0)
                    {
                        var shopStream = ctx.CreateShopItemStream(ante, 
                            MotelyShopStreamFlags.ExcludeJokers | MotelyShopStreamFlags.ExcludeTarots | MotelyShopStreamFlags.ExcludeSpectrals);
                        
                        int maxSlot = shopSlots.Max();
                        for (int i = 0; i <= maxSlot; i++)
                        {
                            var item = ctx.GetNextShopItem(ref shopStream);
                            if (shopSlots.Contains(i))
                            {
                                var matches = VectorEnum256.Equals(item.Type, targetItemType);
                                if (clause.EditionEnum.HasValue)
                                    matches &= VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
                                clauseMask |= matches;
                            }
                        }
                    }
                    
                    // Check celestial packs
                    var packSlots = clause.Sources?.PackSlots;
                    if (packSlots != null && packSlots.Length > 0)
                    {
                        var boosterStream = ctx.CreateBoosterPackStream(ante);
                        var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                        
                        int maxPackSlot = packSlots.Max();
                        for (int i = 0; i <= maxPackSlot; i++)
                        {
                            var pack = ctx.GetNextBoosterPack(ref boosterStream);
                            if (packSlots.Contains(i))
                            {
                                VectorMask isCelestial = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Celestial);
                                if (isCelestial.IsAllFalse()) continue;
                                
                                // Handle pack sizes - most packs are normal size
                                var normalSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Normal);
                                var jumboSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Jumbo);
                                var megaSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega);
                                
                                // Get contents for normal size (most common)
                                var contents = ctx.GetNextCelestialPackContents(ref planetStream, MotelyBoosterPackSize.Normal);
                                for (int j = 0; j < contents.Length; j++)
                                {
                                    var matches = VectorEnum256.Equals(contents[j].Type, targetItemType);
                                    if (clause.EditionEnum.HasValue)
                                        matches &= VectorEnum256.Equals(contents[j].Edition, clause.EditionEnum.Value);
                                    clauseMask |= matches;
                                }
                            }
                        }
                    }
                }
                
                mask &= clauseMask;
                if (mask.IsAllFalse()) return mask;
            }
            
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterSpectrals(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(SpectralCard) called with empty clauses");
            
            var mask = VectorMask.AllBitsSet;
            
            foreach (var clause in Clauses)
            {
                var clauseMask = VectorMask.NoBitsSet;
                
                // Handle wildcards with scalar fallback
                if (clause.IsWildcard || !clause.SpectralEnum.HasValue)
                {
                    var localClause = clause;
                    return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
                    {
                        foreach (var ante in localClause.EffectiveAntes ?? [])
                        {
                            if (MotelyJsonScoring.CountSpectralOccurrences(ref singleCtx, localClause, ante, earlyExit: true) > 0)
                                return true;
                        }
                        return false;
                    });
                }
                
                var targetItemType = (MotelyItemType)clause.SpectralEnum.Value;
                
                foreach (var ante in clause.EffectiveAntes ?? [])
                {
                    // Check shops (only available in Ghost deck)
                    var shopSlots = clause.Sources?.ShopSlots;
                    if (shopSlots != null && shopSlots.Length > 0 && ctx.Deck == MotelyDeck.Ghost)
                    {
                        var shopStream = ctx.CreateShopItemStream(ante);
                        
                        int maxSlot = shopSlots.Max();
                        for (int i = 0; i <= maxSlot; i++)
                        {
                            var item = ctx.GetNextShopItem(ref shopStream);
                            if (shopSlots.Contains(i))
                            {
                                var matches = VectorEnum256.Equals(item.Type, targetItemType);
                                if (clause.EditionEnum.HasValue)
                                    matches &= VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
                                clauseMask |= matches;
                            }
                        }
                    }
                    
                    // Check spectral packs
                    var packSlots = clause.Sources?.PackSlots;
                    if (packSlots != null && packSlots.Length > 0)
                    {
                        var boosterStream = ctx.CreateBoosterPackStream(ante);
                        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                        
                        int maxPackSlot = packSlots.Max();
                        for (int i = 0; i <= maxPackSlot; i++)
                        {
                            var pack = ctx.GetNextBoosterPack(ref boosterStream);
                            if (packSlots.Contains(i))
                            {
                                VectorMask isSpectral = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Spectral);
                                if (isSpectral.IsAllFalse()) continue;
                                
                                // Handle pack sizes - most packs are normal size  
                                var normalSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Normal);
                                var jumboSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Jumbo);
                                var megaSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Mega);
                                
                                // Get contents for normal size (most common)
                                var contents = ctx.GetNextSpectralPackContents(ref spectralStream, MotelyBoosterPackSize.Normal);
                                for (int j = 0; j < contents.Length; j++)
                                {
                                    var matches = VectorEnum256.Equals(contents[j].Type, targetItemType);
                                    if (clause.EditionEnum.HasValue)
                                        matches &= VectorEnum256.Equals(contents[j].Edition, clause.EditionEnum.Value);
                                    clauseMask |= matches;
                                }
                            }
                        }
                    }
                }
                
                mask &= clauseMask;
                if (mask.IsAllFalse()) return mask;
            }
            
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private VectorMask FilterJokers(ref MotelyVectorSearchContext ctx)
        {
            if (Clauses == null || Clauses.Count == 0)
            {
                DebugLogger.Log($"FilterJokers: No clauses, passing all seeds");
                return VectorMask.AllBitsSet;
            }
            
            // Filter to only joker-type clauses
            var jokerClauses = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
            foreach (var clause in Clauses)
            {
                if (clause.ItemTypeEnum == MotelyFilterItemType.Joker || clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                {
                    jokerClauses.Add(clause);
                }
            }
            
            if (jokerClauses.Count == 0)
            {
                DebugLogger.Log($"FilterJokers: No joker clauses, passing all seeds");
                return VectorMask.AllBitsSet;
            }
            
            // Try vectorized path for ALL jokers including soul jokers!
            DebugLogger.Log($"FilterJokers: Using VECTORIZED path for {jokerClauses.Count} joker clauses (including soul jokers)");
            return FilterJokersVectorized(ref ctx, jokerClauses);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private VectorMask FilterJokersVectorized(ref MotelyVectorSearchContext ctx, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
        {
            // Track which clauses have been satisfied (start with none satisfied)
            var clauseMasks = new VectorMask[clauses.Count];
            
            // Group clauses by ante for efficient checking
            var clausesByAnte = new Dictionary<int, List<(int clauseIndex, MotelyJsonConfig.MotleyJsonFilterClause clause)>>();
            for (int i = 0; i < clauses.Count; i++)
            {
                var clause = clauses[i];
                foreach (var ante in clause.EffectiveAntes ?? [])
                {
                    if (!clausesByAnte.ContainsKey(ante))
                        clausesByAnte[ante] = new();
                    clausesByAnte[ante].Add((i, clause));
                }
            }
            
            // Process each ante IN ORDER to preserve PRNG state consistency
            // Avoid LINQ allocation - process antes 1-8 in order
            for (int ante = 1; ante <= 8; ante++)
            {
                if (!clausesByAnte.TryGetValue(ante, out var anteClauses))
                    continue;
                
                // Check if any clauses need shops
                bool needShops = false;
                foreach (var c in anteClauses)
                {
                    if (c.clause.Sources?.ShopSlots?.Length > 0)
                    {
                        needShops = true;
                        break;
                    }
                }
                if (needShops)
                {
                    // Find max shop slot we need to check
                    int maxShopSlot = -1;
                    foreach (var c in anteClauses)
                    {
                        if (c.clause.Sources?.ShopSlots != null)
                        {
                            foreach (var slot in c.clause.Sources.ShopSlots)
                            {
                                if (slot > maxShopSlot)
                                    maxShopSlot = slot;
                            }
                        }
                    }
                    
                    if (maxShopSlot >= 0)
                    {
                        var shopStream = ctx.CreateShopItemStream(ante,
                            MotelyShopStreamFlags.ExcludeTarots | MotelyShopStreamFlags.ExcludePlanets | MotelyShopStreamFlags.ExcludeSpectrals);
                        
                        // Iterate through shop slots
                        for (int slot = 0; slot <= maxShopSlot; slot++)
                        {
                            var item = ctx.GetNextShopItem(ref shopStream);
                            
                            // Check this item against all clauses that care about this slot
                            foreach (var (clauseIndex, clause) in anteClauses)
                            {
                                if (clause.Sources?.ShopSlots?.Contains(slot) == true)
                                {
                                    VectorMask matches;
                                    if (clause.IsWildcard || !clause.JokerEnum.HasValue)
                                    {
                                        // Wildcard - any joker matches
                                        matches = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker);
                                    }
                                    else
                                    {
                                        // Specific joker
                                        var targetType = (MotelyItemType)clause.JokerEnum.Value;
                                        matches = VectorEnum256.Equals(item.Type, targetType);
                                        
                                        if (clause.EditionEnum.HasValue)
                                            matches &= VectorEnum256.Equals(item.Edition, clause.EditionEnum.Value);
                                    }
                                    
                                    clauseMasks[clauseIndex] |= matches;
                                }
                            }
                        }
                    }
                }
                
                // Check if any clauses need packs
                bool needPacks = false;
                foreach (var c in anteClauses)
                {
                    if (c.clause.Sources?.PackSlots?.Length > 0)
                    {
                        needPacks = true;
                        break;
                    }
                }
                if (needPacks)
                {
                    int maxPackSlot = -1;
                    foreach (var c in anteClauses)
                    {
                        if (c.clause.Sources?.PackSlots != null)
                        {
                            foreach (var slot in c.clause.Sources.PackSlots)
                            {
                                if (slot > maxPackSlot)
                                    maxPackSlot = slot;
                            }
                        }
                    }
                    
                    if (maxPackSlot >= 0)
                    {
                        // Use cached stream if available (we cached it in CreateFilter)
                        var boosterStream = ctx.CreateBoosterPackStream(ante, isCached: true, generatedFirstPack: false);
                        var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante, isCached: true);
                        
                        for (int slot = 0; slot <= maxPackSlot; slot++)
                        {
                            var pack = ctx.GetNextBoosterPack(ref boosterStream);
                            VectorMask isBuffoon = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Buffoon);
                            
                            // Debug: Log what pack types we're seeing in ante 1, slot 0
                            if (ante == 1 && slot == 0)
                            {
                                if (!isBuffoon.IsAllFalse())
                                {
                                    DebugLogger.Log($"Found Buffoon pack in ante {ante} slot {slot}!");
                                }
                                else
                                {
                                    // Just log it - we'll see if buffoon packs are rare in ante 1 slot 0
                                    DebugLogger.Log($"Ante 1 slot 0: Not a Buffoon pack");
                                }
                            }
                            
                            if (!isBuffoon.IsAllFalse())
                            {
                                var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, MotelyBoosterPackSize.Normal);
                                
                                // Check each joker in the pack against relevant clauses
                                foreach (var (clauseIndex, clause) in anteClauses)
                                {
                                    if (clause.Sources?.PackSlots?.Contains(slot) == true)
                                    {
                                        for (int j = 0; j < contents.Length; j++)
                                        {
                                            VectorMask matches;
                                            if (clause.IsWildcard || !clause.JokerEnum.HasValue)
                                            {
                                                matches = VectorMask.AllBitsSet; // Any joker from pack
                                            }
                                            else
                                            {
                                                var targetType = (MotelyItemType)clause.JokerEnum.Value;
                                                matches = VectorEnum256.Equals(contents[j].Type, targetType);
                                                
                                                if (clause.EditionEnum.HasValue)
                                                    matches &= VectorEnum256.Equals(contents[j].Edition, clause.EditionEnum.Value);
                                            }
                                            
                                            clauseMasks[clauseIndex] |= matches;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Check soul jokers (vectorized!)
                bool hasSoulJokerClauses = false;
                foreach (var c in anteClauses)
                {
                    if (c.clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                    {
                        hasSoulJokerClauses = true;
                        break;
                    }
                }
                if (hasSoulJokerClauses)
                {
                    // VECTORIZED soul joker checking!
                    // 1. First check what the soul joker WOULD be
                    var soulJokerStream = ctx.CreateSoulJokerStream(ante);
                    var soulJoker = ctx.GetNextJoker(ref soulJokerStream);
                    
                    // 2. Check if any soul joker clause matches this joker
                    foreach (var (clauseIndex, clause) in anteClauses)
                    {
                        if (clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                        {
                            VectorMask soulJokerMatches = VectorMask.NoBitsSet;
                            
                            if (clause.IsWildcard || !clause.JokerEnum.HasValue)
                            {
                                // Any soul joker is fine
                                soulJokerMatches = VectorMask.AllBitsSet;
                            }
                            else
                            {
                                // Check if it matches the requested soul joker
                                var targetType = (MotelyItemType)clause.JokerEnum.Value;
                                soulJokerMatches = VectorEnum256.Equals(soulJoker.Type, targetType);
                            }
                            
                            // 3. Now check if The Soul card appears in packs
                            if (!soulJokerMatches.IsAllFalse())
                            {
                                // Check first 3 packs for The Soul card
                                var packStream = ctx.CreateBoosterPackStream(ante, isCached: true, generatedFirstPack: false);
                                var tarotStream = ctx.CreateArcanaPackTarotStream(ante, isCached: true);
                                var spectralStream = ctx.CreateSpectralPackSpectralStream(ante, isCached: true);
                                
                                VectorMask hasTheSoul = VectorMask.NoBitsSet;
                                
                                for (int packIdx = 0; packIdx < 3; packIdx++)
                                {
                                    var pack = ctx.GetNextBoosterPack(ref packStream);
                                    var packType = pack.GetPackType();
                                    
                                    // Check if it's an Arcana pack
                                    VectorMask isArcana = VectorEnum256.Equals(packType, MotelyBoosterPackType.Arcana);
                                    if (!isArcana.IsAllFalse())
                                    {
                                        // Check if The Soul is in the Arcana pack
                                        // We need to handle different pack sizes - most are Normal
                                        var packSizeVector = pack.GetPackSize();
                                        var normalSize = VectorEnum256.Equals(packSizeVector, MotelyBoosterPackSize.Normal);
                                        var jumboSize = VectorEnum256.Equals(packSizeVector, MotelyBoosterPackSize.Jumbo);
                                        var megaSize = VectorEnum256.Equals(packSizeVector, MotelyBoosterPackSize.Mega);
                                        
                                        // Check normal size packs (most common)
                                        if (!Vector256.EqualsAll(normalSize, Vector256<int>.Zero))
                                        {
                                            var hasTheSoulNormal = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, MotelyBoosterPackSize.Normal);
                                            hasTheSoul |= isArcana & new VectorMask(MotelyVectorUtils.VectorMaskToIntMask(normalSize)) & hasTheSoulNormal;
                                        }
                                        
                                        // Check jumbo packs
                                        if (!Vector256.EqualsAll(jumboSize, Vector256<int>.Zero))
                                        {
                                            var hasTheSoulJumbo = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, MotelyBoosterPackSize.Jumbo);
                                            hasTheSoul |= isArcana & new VectorMask(MotelyVectorUtils.VectorMaskToIntMask(jumboSize)) & hasTheSoulJumbo;
                                        }
                                        
                                        // Check mega packs
                                        if (!Vector256.EqualsAll(megaSize, Vector256<int>.Zero))
                                        {
                                            var hasTheSoulMega = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, MotelyBoosterPackSize.Mega);
                                            hasTheSoul |= isArcana & new VectorMask(MotelyVectorUtils.VectorMaskToIntMask(megaSize)) & hasTheSoulMega;
                                        }
                                    }
                                    
                                    // Check if it's a Spectral pack
                                    VectorMask isSpectral = VectorEnum256.Equals(packType, MotelyBoosterPackType.Spectral);
                                    if (!isSpectral.IsAllFalse())
                                    {
                                        // Check if The Soul is in the Spectral pack
                                        var packSizeVector = pack.GetPackSize();
                                        var normalSize = VectorEnum256.Equals(packSizeVector, MotelyBoosterPackSize.Normal);
                                        
                                        // Spectral packs are always normal size
                                        if (!Vector256.EqualsAll(normalSize, Vector256<int>.Zero))
                                        {
                                            var hasTheSoulSpectral = ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, MotelyBoosterPackSize.Normal);
                                            hasTheSoul |= isSpectral & new VectorMask(MotelyVectorUtils.VectorMaskToIntMask(normalSize)) & hasTheSoulSpectral;
                                        }
                                    }
                                }
                                
                                // Only count as found if soul joker matches AND The Soul appears
                                clauseMasks[clauseIndex] |= soulJokerMatches & hasTheSoul;
                            }
                        }
                    }
                }
            }
            
            // All clauses must be satisfied (AND)
            var result = VectorMask.AllBitsSet;
            foreach (var clauseMask in clauseMasks)
            {
                result &= clauseMask;
                if (result.IsAllFalse()) return result; // Early exit
            }
            
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterPlayingCards(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(PlayingCard) called with empty clauses");
            
            // Playing cards are complex - they have suits and ranks, not simple item types
            // For now we need to use scalar for proper wildcard and rank/suit matching
            var clauses = Clauses;
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes ?? [])
                    {
                        if (MotelyJsonScoring.CountPlayingCardOccurrences(ref singleCtx, clause, ante, earlyExit: true) > 0)
                            return true;
                    }
                }
                return false;
            });
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterBosses(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(Boss) called with empty clauses");
            throw new NotImplementedException("Boss filtering is not yet implemented. The boss PRNG does not match the actual game behavior.");
        }

        #endregion
    }
}

/// <summary>
/// Filter categories for the universal MotelyFilterDesc
/// </summary>
public enum FilterCategory
{
    // Meta
    Voucher,
    Boss,
    Tag,

    // Consumables
    TarotCard,
    PlanetCard,
    SpectralCard,

    // Standard
    PlayingCard,

    // Jokers and "SoulJoker" in same category
    Joker,
    
    // Combined filter for multiple categories
    Mixed,
}