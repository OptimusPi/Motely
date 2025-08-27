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
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses = clauses;

    public readonly string Name => $"Everybody loves Wee Joker";
    public readonly string Description => $"pifreak loves you!";

    public MotelyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache relevant streams based on what clauses need
        if (_category == FilterCategory.Voucher && Clauses.Count > 0)
        {
            // Cache only the antes we actually need
            var neededAntes = Clauses
                .Where(c => c.EffectiveAntes != null)
                .SelectMany(c => c.EffectiveAntes)
                .Distinct()
                .OrderBy(a => a);
            
            foreach (var ante in neededAntes)
            {
                ctx.CacheAnteFirstVoucher(ante);
            }
        }
        else if (_category == FilterCategory.Joker && Clauses.Count > 0)
        {
            // Cache pack streams for antes that need them
            var neededAntes = Clauses
                .Where(c => c.EffectiveAntes != null)
                .SelectMany(c => c.EffectiveAntes)
                .Distinct()
                .OrderBy(a => a);
            
            foreach (var ante in neededAntes)
            {
                ctx.CacheBoosterPackStream(ante);
            }
        }
        
        return new MotelyFilter(_category, Clauses);
    }

    public struct MotelyFilter : IMotelySeedFilter
    {
        private readonly FilterCategory _category;
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses;
        private readonly int _maxAnte;
        
        public MotelyFilter(FilterCategory category, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
        {
            _category = category;
            Clauses = clauses;
            
            // Pre-compute max ante to avoid doing it every time
            _maxAnte = 1;
            if (clauses.Count > 0)
            {
                foreach (var clause in clauses)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // If no clauses, pass everything through (for scoreOnly mode)
            // Don't call ANY filter methods if there's nothing to filter!
            if (Clauses.Count == 0)
                return VectorMask.AllBitsSet;

            return _category switch
            {
                FilterCategory.Voucher => FilterVouchers(ref searchContext),
                FilterCategory.Tag => FilterTags(ref searchContext),
                FilterCategory.TarotCard => FilterTarots(ref searchContext),
                FilterCategory.PlanetCard => FilterPlanets(ref searchContext),
                FilterCategory.SpectralCard => FilterSpectrals(ref searchContext),
                FilterCategory.Joker => FilterJokers(ref searchContext),
                FilterCategory.PlayingCard => FilterPlayingCards(ref searchContext),
                FilterCategory.Boss => FilterBosses(ref searchContext),
                _ => throw new ArgumentException($"Unknown filter category: {_category}")
            };
        }

        #region Vector Filtering Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterVouchers(ref MotelyVectorSearchContext ctx)
        {
            var mask = VectorMask.AllBitsSet;
            var state = new MotelyVectorRunState();
            
            // Early exit optimization: check ante 1 Telescope first  
            // This is the most selective filter for PerkeoObservatory
            foreach (var clause in Clauses)
            {
                if (clause.VoucherEnum == MotelyVoucher.Telescope && 
                    (clause.EffectiveAntes?.Contains(1) ?? false))
                {
                    var vouchers = ctx.GetAnteFirstVoucher(1);
                    VectorMask telescopeMask = VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
                    if (telescopeMask.IsAllFalse())
                        return VectorMask.NoBitsSet;  // Early exit - no Telescope, no point continuing
                    mask = telescopeMask;
                    state.ActivateVoucher(MotelyVoucher.Telescope);
                    break;
                }
            }

            // Check remaining voucher clauses
            foreach (var clause in Clauses)
            {
                Debug.Assert(clause.VoucherEnum.HasValue, "FilterVouchers requires VoucherEnum");
                
                // Skip Telescope if already handled
                if (clause.VoucherEnum == MotelyVoucher.Telescope && 
                    (clause.EffectiveAntes?.Contains(1) ?? false))
                    continue;
                
                var clauseMask = VectorMask.NoBitsSet;

                // Check if this voucher appears at ANY of its required antes
                foreach (var ante in clause.EffectiveAntes ?? [])
                {
                    if (ante <= _maxAnte)
                    {
                        var vouchers = ctx.GetAnteFirstVoucher(ante, state);
                        var matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                        clauseMask |= matches;  // OR - voucher can appear at any of its antes
                    }
                }

                // This voucher must appear at at least one of its antes
                if (clauseMask.IsAllFalse())
                    return VectorMask.NoBitsSet;  // Required voucher not found
                
                // Activate voucher for all matching lanes (imperfect but simpler)
                if (!clauseMask.IsAllFalse())
                    state.ActivateVoucher(clause.VoucherEnum.Value);
                
                mask &= clauseMask;  // AND - all required vouchers must be present
                
                // Early exit if no seeds left
                if (mask.IsAllFalse())
                    return VectorMask.NoBitsSet;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterJokers(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(Joker) called with empty clauses");
            
            // Use scalar path for now - soul joker logic is too complex for vectorization
            var clauses = Clauses;
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                foreach (var clause in clauses)
                {
                    bool found = false;
                    foreach (var ante in clause.EffectiveAntes ?? [])
                    {
                        int count = 0;
                        if (clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                        {
                            count = MotelyJsonScoring.CountSoulJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true);
                        }
                        else
                        {
                            count = MotelyJsonScoring.CountJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true);
                        }
                        
                        if (count > 0)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) return false; // This clause not satisfied
                }
                return true; // All clauses satisfied
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterJokersVectorized(ref MotelyVectorSearchContext ctx, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)
        {
            // Better approach: iterate through each ante's items ONCE and check all clauses
            
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
            var sortedAntes = clausesByAnte.Keys.OrderBy(a => a).ToList();
            foreach (var ante in sortedAntes)
            {
                var anteClauses = clausesByAnte[ante];
                
                // Check if any clauses need shops
                bool needShops = anteClauses.Any(c => c.clause.Sources?.ShopSlots?.Length > 0);
                if (needShops)
                {
                    // Find max shop slot we need to check
                    int maxShopSlot = anteClauses
                        .Where(c => c.clause.Sources?.ShopSlots != null)
                        .SelectMany(c => c.clause.Sources!.ShopSlots!)
                        .DefaultIfEmpty(-1)
                        .Max();
                    
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
                bool needPacks = anteClauses.Any(c => c.clause.Sources?.PackSlots?.Length > 0);
                if (needPacks)
                {
                    int maxPackSlot = anteClauses
                        .Where(c => c.clause.Sources?.PackSlots != null)
                        .SelectMany(c => c.clause.Sources!.PackSlots!)
                        .DefaultIfEmpty(-1)
                        .Max();
                    
                    if (maxPackSlot >= 0)
                    {
                        var boosterStream = ctx.CreateBoosterPackStream(ante, isCached: false, generatedFirstPack: ante != 1);
                        var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante, isCached: false);
                        
                        for (int slot = 0; slot <= maxPackSlot; slot++)
                        {
                            var pack = ctx.GetNextBoosterPack(ref boosterStream);
                            VectorMask isBuffoon = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Buffoon);
                            
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
                
                // Check soul jokers
                var soulJokerClauses = anteClauses.Where(c => c.clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker).ToList();
                if (soulJokerClauses.Any())
                {
                    // Soul jokers need special handling - check packs for The Soul
                    // For now, fall back to scalar for soul jokers
                    // TODO: Implement proper vectorized soul joker checking
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
}