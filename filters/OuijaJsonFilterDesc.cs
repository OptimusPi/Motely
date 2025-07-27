using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;
using System.Collections.Generic;
using Motely;
using Motely.Filters;
using System.Diagnostics;
using System.Numerics;

namespace Motely;

public struct OuijaJsonFilterDesc : IMotelySeedFilterDesc<OuijaJsonFilterDesc.OuijaJsonFilter>
{
    public OuijaConfig Config { get; }
    public int Cutoff { get; set; } = 0;

    public OuijaJsonFilterDesc(OuijaConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        if (Config == null)
        {
            DebugLogger.Log("ERROR: Config is null in CreateFilter");
            throw new InvalidOperationException("Config is null");
        }

        DebugLogger.LogFormat("Creating OuijaJsonFilter with {0} needs and {1} wants", 
        Config.Needs?.Length ?? 0, 
        Config.Wants?.Length ?? 0);
        DebugLogger.LogFormat("[OuijaJsonFilter] Deck: {0}, Stake: {1}", Config.Deck, Config.Stake);
        
        // Cache streams for all filter types and antes used in needs/wants
        var allAntes = new HashSet<int>();
        bool cacheTagStream = false;
        if (Config.Needs != null)
        {
            foreach (var need in Config.Needs)
            {
                if (need == null) continue;
                if (string.Equals(need.Type, "Tag", StringComparison.OrdinalIgnoreCase)) cacheTagStream = true;
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : Array.Empty<int>();
                foreach (var ante in searchAntes)
                {
                    allAntes.Add(ante);
                    CacheStreamForType(ctx, need, ante);
                }
            }
        }
        if (Config.Wants != null)
        {
            foreach (var want in Config.Wants)
            {
                if (want == null) continue;
                if (string.Equals(want.Type, "Tag", StringComparison.OrdinalIgnoreCase)) cacheTagStream = true;
                var searchAntes = want.SearchAntes?.Length > 0 ? want.SearchAntes : Array.Empty<int>();
                foreach (var ante in searchAntes)
                {
                    allAntes.Add(ante);
                    CacheStreamForType(ctx, want, ante);
                }
            }
        }
        // Cache tag streams for all antes if any need/want is a tag
        if (cacheTagStream)
        {
            foreach (var ante in allAntes)
            {
                ctx.CacheTagStream(ante);
            }
        }
        return new OuijaJsonFilter(Config, Cutoff);
    }

    private static void CacheStreamForType(MotelyFilterCreationContext ctx, OuijaConfig.Desire desire, int ante)
    {
        // Use pre-parsed TypeCategory from config loader
        var typeCategory = desire.TypeCategory;
        
        if (!typeCategory.HasValue)
        {
            // Handle special cases that don't map directly to MotelyItemTypeCategory
            if (string.Equals(desire.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CachePseudoHash(MotelyPrngKeys.ShopPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.ArcanaPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.CelestialPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerSoul + ante);
                ctx.CacheTarotStream(ante);
                ctx.CacheCelestialPackPlanetStream(ante);
                return;
            }
            else if (string.Equals(desire.Type, "Tag", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(desire.Type, "SmallBlindTag", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(desire.Type, "BigBlindTag", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CacheTagStream(ante);
                return;
            }
            else if (string.Equals(desire.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
            {
                ctx.CacheVoucherStream(ante);
                return;
            }
            else if (string.Equals(desire.Type, "Boss", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Boss blind generation not yet implemented in Motely
                // Bosses have their own RNG stream (R_Boss) separate from tags
                // ctx.CachePseudoHash(MotelyPrngKeys.Boss); // Need to add Boss key
                return;
            }
            else if (string.Equals(desire.Type, "PlayingCard", StringComparison.OrdinalIgnoreCase))
            {
                    // Playing cards can appear in many places
                    ctx.CachePseudoHash(MotelyPrngKeys.Shop + ante);
                    // TODO: Add more caching as playing card generation is implemented
                    return;
                }
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unknown type '{0}' (ante {1}) - no canonical PRNG key used", desire.Type, ante);
                return;
        }
        
        var parsedType = typeCategory.Value;
        
        switch (parsedType)
        {
            case MotelyItemTypeCategory.Joker:
                // Only cache shop streams if we're searching shops
                if (desire.IncludeShopStream)
                {
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerCommon + "sho" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerUncommon + "sho" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerRare + "sho" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerLegendary + "sho" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerEdition + "sho" + ante);
                }
                // Cache buffoon pack streams if we're searching packs
                if (desire.IncludeBoosterPacks)
                {
                    ctx.CachePseudoHash(MotelyPrngKeys.ShopPack + ante); // Booster pack generation
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerCommon + "buf" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerUncommon + "buf" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerRare + "buf" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.JokerEdition + "buf" + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.BuffoonJokerEternalPerishableSource + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.BuffoonJokerRentalSource + ante);
                }
                break;
            case MotelyItemTypeCategory.PlayingCard:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] PlayingCard streams not yet implemented for ante {0}", ante);
                break;
            case MotelyItemTypeCategory.TarotCard:
                if (desire.IncludeShopStream)
                {
                    ctx.CacheTarotStream(ante);
                }
                if (desire.IncludeBoosterPacks)
                {
                    ctx.CachePseudoHash(MotelyPrngKeys.Tarot + MotelyPrngKeys.ArcanaPack + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.TarotSoul + MotelyPrngKeys.Tarot + ante);
                }
                break;
            case MotelyItemTypeCategory.PlanetCard:
                if (desire.IncludeShopStream)
                {
                    ctx.CachePseudoHash(MotelyPrngKeys.Planet + MotelyPrngKeys.Shop + ante);
                }
                if (desire.IncludeBoosterPacks)
                {
                    ctx.CachePseudoHash(MotelyPrngKeys.Planet + MotelyPrngKeys.CelestialPack + ante);
                }
                break;
            case MotelyItemTypeCategory.SpectralCard:
                if (desire.IncludeShopStream)
                {
                    ctx.CachePseudoHash(MotelyPrngKeys.Spectral + MotelyPrngKeys.Shop + ante);
                }
                if (desire.IncludeBoosterPacks)
                {
                    ctx.CachePseudoHash(MotelyPrngKeys.Spectral + MotelyPrngKeys.SpectralPack + ante);
                    ctx.CachePseudoHash(MotelyPrngKeys.SpectralSoul + ante);
                }
                break;
            default:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unhandled canonical type '{0}' (ante {1}) - no PRNG key used", parsedType, ante);
                break;
        }
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        private readonly OuijaConfig _config;
        private readonly TypedOuijaConfig _typedConfig;
        private readonly int _cutoff;
        private readonly MotelyDeck _deck;
        private readonly MotelyStake _stake;

        public OuijaJsonFilter(OuijaConfig config, int cutoff = 0)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _cutoff = cutoff;
            
            // Use pre-parsed enums from config
            _deck = config.ParsedDeck;
            _stake = config.ParsedStake;
            
            // Convert to typed config ONCE here to avoid string comparisons in hot path
            _typedConfig = TypedOuijaConfig.FromOuijaConfig(config);
            
            DebugLogger.LogFormat("[OuijaJsonFilter] Using deck: {0}, stake: {1}", _deck, _stake);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            try
            {
                VectorMask mask = VectorMask.AllBitsSet;
                if (_config == null)
                {
                    DebugLogger.Log("ERROR: Config is null in Filter method");
                    return default;
                }
                var localConfig = _config;
                // Remove per-thread header printing - this is now handled by the main search
                mask = ProcessNeedsTyped(ref searchContext, mask, _typedConfig);
                if (mask.IsAllFalse())
                {
                    DebugLogger.Log("All needs failed to match");
                    return mask;
                }
                DebugLogger.LogFormat("After needs, {0} seeds passing", CountBitsSet(mask));
                int cutoff = _cutoff;
                // Process results immediately as they are found
                var searchInstance = searchContext.SearchInstance;
                var typedConfigCopy = _typedConfig; // Copy to local variable for lambda
                mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    if (!CheckNonVectorizedNeedsTyped(ref singleCtx, typedConfigCopy))
                    {
                        DebugLogger.Log("Non-vectorized needs failed to match");
                        return false;
                    }
                    var result = ProcessWantsAndScoreTyped(ref singleCtx, typedConfigCopy, localConfig!);
                    if (result.Success && result.TotalScore >= cutoff)
                    {
                        // Print CSV directly with limited columns
                        var wantColumns = OuijaJsonFilterDesc.GetWantsColumnNames(localConfig!);
                        FancyConsole.WriteLine(result.ToCsvRow(localConfig!, wantColumns.Length));
                        
                        // Also enqueue the result for external consumers (unless cancelled)
                        if (!IsCancelled)
                        {
                            ResultsQueue?.Enqueue(result);
                        }
                    }
                    return result.Success && result.TotalScore >= cutoff;
                });
                return mask;
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("ERROR in Filter: {0}\n{1}", ex.Message, ex.StackTrace ?? "No stack trace");
                return default;
            }
        }

        // Keep original ProcessNeeds for backward compatibility
        private static VectorMask ProcessNeeds(ref MotelyVectorSearchContext searchContext, VectorMask mask, OuijaConfig config)
        {
            // Convert to typed config and delegate
            var typedConfig = TypedOuijaConfig.FromOuijaConfig(config);
            return ProcessNeedsTyped(ref searchContext, mask, typedConfig);
        }

        private static VectorMask ProcessNeedsTyped(ref MotelyVectorSearchContext searchContext, VectorMask mask, TypedOuijaConfig config)
        {
            if (config.Needs == null || config.Needs.Length == 0)
            {
                DebugLogger.Log("No needs to process - returning full mask");
                return mask;
            }

            // Perkeo-style: If any voucher need is present in any ante, activate/pass
            var voucherNeeds = config.Needs.Where(n => n.Type == TypedOuijaConfig.DesireType.Voucher).ToArray();
            if (voucherNeeds.Length > 0)
            {
                VectorMask voucherMask = VectorMask.AllBitsClear;
                foreach (var need in voucherNeeds)
                {
                    var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : Array.Empty<int>();
                    foreach (var ante in searchAntes)
                    {
                        var voucherVec = searchContext.GetAnteFirstVoucher(ante);
                        var targetVoucher = need.VoucherValue;
                        var matchMask = VectorEnum256.Equals(voucherVec, targetVoucher);
                        for (int lane = 0; lane < Vector512<double>.Count; lane++)
                        {
                            var foundVoucher = voucherVec[lane];
                            DebugLogger.LogFormat("[DEBUG] Voucher check ante {0}, lane {1}: Found={2}, Target={3}", ante, lane, foundVoucher, targetVoucher);
                        }
                        voucherMask |= matchMask;
                    }
                }
                int passing = CountBitsSet(voucherMask);
                DebugLogger.LogFormat("[PerkeoStyle] After voucher needs (OR): {0} seeds passing", passing);
                mask &= voucherMask;
                if (mask.IsAllFalse())
                {
                    DebugLogger.Log("All seeds filtered out by voucher needs - no seeds passed requirements");
                    return mask;
                }
            }

            // Process each need
            foreach (var need in config.Needs.Where(n => n.Type != TypedOuijaConfig.DesireType.Voucher))
            {
                // Check if this is a non-vectorizable need
                bool isNonVectorizable = false;
                
                // SoulJoker needs can't be vectorized
                if (need.Type == TypedOuijaConfig.DesireType.SoulJoker)
                {
                    DebugLogger.LogFormat("SoulJoker need can't be vectorized");
                    isNonVectorizable = true;
                }
                // ALL Joker needs currently can't be vectorized (shop generation not vectorized)
                else if (need.Type == TypedOuijaConfig.DesireType.Joker)
                {
                    isNonVectorizable = true;
                }
                // Tarot needs can't be vectorized (pack checking not vectorized)
                else if (need.Type == TypedOuijaConfig.DesireType.Tarot)
                {
                    isNonVectorizable = true;
                }
                // Planet/Spectral needs are partially vectorizable (shop yes, packs no)
                // Since we need to check packs too, we'll handle them in individual checking
                // TODO: Could optimize by doing shop vector check + pack individual check
                
                if (isNonVectorizable)
                {
                    continue;
                }
                
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : Array.Empty<int>();
                // For each need, we want to find seeds that have it in ANY of the specified antes (OR logic)
                VectorMask needMask = VectorMask.AllBitsClear;
                foreach (var ante in searchAntes)
                {
                    var anteMask = ProcessNeedVectorTyped(ref searchContext, need, ante, VectorMask.AllBitsSet);
                    needMask |= anteMask; // OR operation - seed passes if found in ANY ante
                }
                // After collecting all antes for this need, mask is ANDed with needMask (all needs must be satisfied)
                mask &= needMask;
                int passing = CountBitsSet(mask);
                // Print the relevant enum property for debug clarity
                DebugLogger.LogFormat("After need (type={0}): {1} seeds passing", need.Type, passing);
                if (mask.IsAllFalse()) 
                {
                    DebugLogger.Log("All seeds filtered out by need - no seeds passed requirements");
                    return mask;
                }
            }
            return mask;
        }

        private static VectorMask ProcessNeedVectorTyped(ref MotelyVectorSearchContext searchContext, 
            TypedOuijaConfig.TypedDesire need, int ante, VectorMask mask)
        {
            switch (need.Type)
            {
                case TypedOuijaConfig.DesireType.SoulJoker:
                    // Soul jokers can't be vectorized but we still need to filter!
                    // Return original mask - individual processing will happen in CheckNonVectorizedNeeds
                    return mask;
                    
                case TypedOuijaConfig.DesireType.Joker:
                    // For shop jokers, we need vectorized shop generation which isn't available yet
                    DebugLogger.LogFormat("[WARNING] Shop joker filtering not yet vectorized for {0}", need.JokerValue);
                    return mask;
                    
                case TypedOuijaConfig.DesireType.Tag:
                case TypedOuijaConfig.DesireType.SmallBlindTag:
                case TypedOuijaConfig.DesireType.BigBlindTag:
                    return ProcessTagNeedTyped(ref searchContext, need, ante, mask);
                    
                case TypedOuijaConfig.DesireType.Voucher:
                    return ProcessVoucherNeedTyped(ref searchContext, need, ante, mask);
                    
                case TypedOuijaConfig.DesireType.Tarot:
                    // Tarot cards in Arcana packs can't be vectorized yet
                    DebugLogger.LogFormat("[WARNING] Tarot pack filtering not yet vectorized");
                    return mask;
                    
                case TypedOuijaConfig.DesireType.Planet:
                    // Use vectorized shop planet filter!
                    var shopPlanetMask = searchContext.FilterPlanetCard(ante, need.PlanetValue, MotelyPrngKeys.Shop);
                    // Celestial packs can't be vectorized yet
                    DebugLogger.LogFormat("[WARNING] Celestial pack filtering not yet vectorized - only checking shop planets");
                    return mask & shopPlanetMask;
                    
                case TypedOuijaConfig.DesireType.Spectral:
                    // Use vectorized shop spectral filter!
                    var shopSpectralMask = searchContext.FilterSpectralCard(ante, need.SpectralValue, MotelyPrngKeys.Shop);
                    // Spectral packs can't be vectorized yet
                    DebugLogger.LogFormat("[WARNING] Spectral pack filtering not yet vectorized - only checking shop spectrals");
                    return mask & shopSpectralMask;
                    
                // Boss type doesn't exist in TypedOuijaConfig.DesireType yet
                // TODO: Add Boss to TypedOuijaConfig.DesireType when boss blind generation is implemented
                    
                case TypedOuijaConfig.DesireType.PlayingCard:
                    // Playing cards are complex - handled in individual seed processing
                    DebugLogger.LogFormat("[WARNING] PlayingCard filtering not yet vectorized");
                    return mask;
                    
                default:
                    DebugLogger.LogFormat("❌ Unhandled need type: {0}", need.Type);
                    return mask;
            }
        }

        private static int CountBitsSet(VectorMask mask)
        {
            int count = 0;
            for (int i = 0; i < Vector512<double>.Count; i++)
            {
                if (mask[i]) count++;
            }
            return count;
        }

        private static VectorMask ProcessNeedVector(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, 
            int ante, VectorMask mask)
        {
            if (need == null)
            {
                DebugLogger.Log("Invalid need: null");
                return mask;
            }

            // Use pre-parsed TypeCategory from config loader
            var typeCat = need.TypeCategory;
            if (!typeCat.HasValue)
            {
                // Handle special types that don't map to MotelyItemTypeCategory
                if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    // Soul jokers can't be vectorized but we still need to filter!
                    // Return original mask - individual processing will happen in CheckNonVectorizedNeeds
                    // This is because we can't check soul jokers in vector context
                    return mask;
                }
                if (string.Equals(need.Type, "Tag", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(need.Type, "SmallBlindTag", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(need.Type, "BigBlindTag", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessTagNeed(ref searchContext, need, ante, mask);
                }
                if (string.Equals(need.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessVoucherNeed(ref searchContext, need, ante, mask);
                }
                if (string.Equals(need.Type, "Boss", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessBossNeed(ref searchContext, need, ante, mask);
                }
                Console.WriteLine("⚠️  Unknown need type: {0}", need.Type);
                return mask;
            }
            
            switch(typeCat.Value)
            {
                case MotelyItemTypeCategory.Joker:
                    return ProcessJokerNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.TarotCard:
                    return ProcessTarotNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.PlanetCard:
                    return ProcessPlanetNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.SpectralCard:
                    return ProcessSpectralNeed(ref searchContext, need, ante, mask);

                case MotelyItemTypeCategory.PlayingCard:
                    // Playing cards are complex - handled in individual seed processing
                    DebugLogger.LogFormat("[WARNING] PlayingCard filtering not yet vectorized");
                    return mask; // Return original mask for now

                default:
                    DebugLogger.LogFormat("❌ Unhandled need type: {0}", need.Type);
                    return mask;
            }
        }

        private static VectorMask ProcessJokerNeed(ref MotelyVectorSearchContext searchContext, 
            OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.JokerEnum.HasValue)
            {
                DebugLogger.LogFormat("❌ No JokerEnum for joker need: {0}", need.Value);
                return default; // Return empty mask
            }

            var targetJoker = need.JokerEnum.Value;
            var jokerRarity = GetJokerRarity(targetJoker);
            
            // Legendary jokers come from Soul cards - can't be vectorized yet
            if (jokerRarity == MotelyJokerRarity.Legendary)
            {
                DebugLogger.LogFormat("[WARNING] Legendary joker {0} needs cannot be vectorized - skipping", targetJoker);
                // TODO: When soul joker vectorization is available, implement here
                return mask; // For now, return original mask (don't filter)
            }

            // For shop jokers, we need vectorized shop generation which isn't available yet
            // TODO: When vectorized shop generation is available, use it here
            DebugLogger.LogFormat("[WARNING] Shop joker filtering not yet vectorized for {0}", targetJoker);
            
            // For now, return the original mask - filtering will happen in individual seed processing
            return mask;
        }

        private static VectorMask ProcessTarotNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.TarotEnum.HasValue)
            {
                DebugLogger.Log("Missing TarotEnum for Tarot need");
                return mask;
            }
            
            // Tarot cards in Arcana packs can't be vectorized yet
            DebugLogger.LogFormat("[WARNING] Tarot pack filtering not yet vectorized");
            return mask; // Return original mask - will be handled in individual processing
        }

        private static VectorMask ProcessPlanetNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.PlanetEnum.HasValue)
            {
                DebugLogger.Log("Missing PlanetEnum for Planet need");
                return mask;
            }
            
            // Use vectorized shop planet filter!
            var shopPlanetMask = searchContext.FilterPlanetCard(ante, need.PlanetEnum.Value, MotelyPrngKeys.Shop);
            
            // Celestial packs can't be vectorized yet
            DebugLogger.LogFormat("[WARNING] Celestial pack filtering not yet vectorized - only checking shop planets");
            
            return mask & shopPlanetMask;
        }

        private static VectorMask ProcessSpectralNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.SpectralEnum.HasValue)
            {
                DebugLogger.Log("Missing SpectralEnum for Spectral need");
                return mask;
            }
            
            // Use vectorized shop spectral filter!
            var shopSpectralMask = searchContext.FilterSpectralCard(ante, need.SpectralEnum.Value, MotelyPrngKeys.Shop);
            
            // Spectral packs can't be vectorized yet
            DebugLogger.LogFormat("[WARNING] Spectral pack filtering not yet vectorized - only checking shop spectrals");
            
            return mask & shopSpectralMask;
        }

        private static VectorMask ProcessTagNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.TagEnum.HasValue)
            {
                DebugLogger.Log("Missing TagEnum for Tag need");
                return mask;
            }
            var targetTag = need.TagEnum.Value;
            MotelyVectorTagStream tagStream = searchContext.CreateTagStreamCached(ante);
            var smallBlindTag = searchContext.GetNextTag(ref tagStream);
            var bigBlindTag = searchContext.GetNextTag(ref tagStream);
            
            VectorMask matchMask = VectorMask.NoBitsSet;
            
            // Check based on the type
            if (string.Equals(need.Type, "SmallBlindTag", StringComparison.OrdinalIgnoreCase))
            {
                matchMask = VectorEnum256.Equals(smallBlindTag, targetTag);
                DebugLogger.LogFormat("[DEBUG] SmallBlindTag check ante {0}: SmallBlind={1} Target={2} Match={3}", 
                    ante, smallBlindTag, targetTag, matchMask);
            }
            else if (string.Equals(need.Type, "BigBlindTag", StringComparison.OrdinalIgnoreCase))
            {
                matchMask = VectorEnum256.Equals(bigBlindTag, targetTag);
                DebugLogger.LogFormat("[DEBUG] BigBlindTag check ante {0}: BigBlind={1} Target={2} Match={3}", 
                    ante, bigBlindTag, targetTag, matchMask);
            }
            else // Default "Tag" matches either
            {
                var smallMatch = VectorEnum256.Equals(smallBlindTag, targetTag);
                var bigMatch = VectorEnum256.Equals(bigBlindTag, targetTag);
                matchMask = smallMatch | bigMatch;
                DebugLogger.LogFormat("[DEBUG] Tag check ante {0}: SmallBlind={1} BigBlind={2} Target={3} Match={4}", 
                    ante, smallBlindTag, bigBlindTag, targetTag, matchMask);
            }
            
            return mask & matchMask;
        }

        private static VectorMask ProcessVoucherNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            if (!need.VoucherEnum.HasValue)
            {
                DebugLogger.Log("Missing VoucherEnum for Voucher need");
                return mask;
            }
            var targetVoucher = need.VoucherEnum.Value;
            var voucherVec = searchContext.GetAnteFirstVoucher(ante);
            // Debug output for each lane
            for (int lane = 0; lane < Vector512<double>.Count; lane++)
            {
                var foundVoucher = voucherVec[lane];
                DebugLogger.LogFormat("[DEBUG] Voucher check ante {0}, lane {1}: Found={2}, Target={3}", ante, lane, foundVoucher, targetVoucher);
            }
            return VectorEnum256.Equals(voucherVec, targetVoucher) & mask;
        }

        private static VectorMask ProcessBossNeed(ref MotelyVectorSearchContext searchContext, OuijaConfig.Desire need, int ante, VectorMask mask)
        {
            // TODO: Boss blind generation not yet implemented in Motely
            // According to Immolate source, bosses have their own RNG stream (R_Boss) separate from tags
            DebugLogger.LogFormat("[WARNING] Boss blind filtering not yet implemented");
            return mask;
        }

        private static VectorMask ProcessTagNeedTyped(ref MotelyVectorSearchContext searchContext, 
            TypedOuijaConfig.TypedDesire need, int ante, VectorMask mask)
        {
            var targetTag = need.TagValue;
            MotelyVectorTagStream tagStream = searchContext.CreateTagStreamCached(ante);
            var smallBlindTag = searchContext.GetNextTag(ref tagStream);
            var bigBlindTag = searchContext.GetNextTag(ref tagStream);
            
            VectorMask matchMask = VectorMask.NoBitsSet;
            
            // Check based on the type
            switch (need.Type)
            {
                case TypedOuijaConfig.DesireType.SmallBlindTag:
                    matchMask = VectorEnum256.Equals(smallBlindTag, targetTag);
                    DebugLogger.LogFormat("[DEBUG] SmallBlindTag check ante {0}: Target={1} Match={2}", 
                        ante, targetTag, matchMask);
                    break;
                    
                case TypedOuijaConfig.DesireType.BigBlindTag:
                    matchMask = VectorEnum256.Equals(bigBlindTag, targetTag);
                    DebugLogger.LogFormat("[DEBUG] BigBlindTag check ante {0}: Target={1} Match={2}", 
                        ante, targetTag, matchMask);
                    break;
                    
                default: // Regular Tag matches either
                    var smallMatch = VectorEnum256.Equals(smallBlindTag, targetTag);
                    var bigMatch = VectorEnum256.Equals(bigBlindTag, targetTag);
                    matchMask = smallMatch | bigMatch;
                    DebugLogger.LogFormat("[DEBUG] Tag check ante {0}: Target={1} Match={2}", 
                        ante, targetTag, matchMask);
                    break;
            }
            
            return mask & matchMask;
        }

        private static VectorMask ProcessVoucherNeedTyped(ref MotelyVectorSearchContext searchContext, 
            TypedOuijaConfig.TypedDesire need, int ante, VectorMask mask)
        {
            var targetVoucher = need.VoucherValue;
            var voucherVec = searchContext.GetAnteFirstVoucher(ante);
            
            // Debug output for each lane
            for (int lane = 0; lane < Vector512<double>.Count; lane++)
            {
                var foundVoucher = voucherVec[lane];
                DebugLogger.LogFormat("[DEBUG] Voucher check ante {0}, lane {1}: Found={2}, Target={3}", 
                    ante, lane, foundVoucher, targetVoucher);
            }
            
            return VectorEnum256.Equals(voucherVec, targetVoucher) & mask;
        }

        private static bool ProcessLegendaryJokerFromSoul(ref MotelySingleSearchContext singleCtx, MotelyJoker targetJoker, int ante, MotelyItemEdition? requiredEdition = null)
        {
            // Just cast MotelyJoker to MotelyItemType - the enum already handles the conversion!
            var targetItemType = (MotelyItemType)targetJoker;
            int totalPacks = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante);
            
            bool tarotStreamInit = false;
            MotelySingleTarotStream tarotStream = default;

            DebugLogger.LogFormat("[====ProcessLegendaryJokerFromSoul] Processing legendary joker {0} in ante {1} with total packs {2}",
                targetItemType, ante, totalPacks);
            for (int i = 0; i < totalPacks; i++)
            {
                DebugLogger.LogFormat("===ProcessLegendaryJokerFromSoul] Processing booster pack {0} in ante {1}", i + 1, ante);
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                var packType = pack.GetPackType();
                
                if (packType == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                    }
                    if (singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                    {
                        DebugLogger.LogFormat("[=ProcessLegendaryJokerFromSoul] Found Soul card in Arcana pack in ante {0}", ante);
                        var stream = singleCtx.CreateSoulJokerStream(ante);
                        var joker = singleCtx.NextJoker(ref stream);

                        // Check like PerkeoObservatoryDesc does
                        if (joker.Type == (targetItemType | MotelyItemType.Joker))
                        {
                            DebugLogger.LogFormat("[ProcessLegendaryJokerFromSoul] Found legendary joker {0} in ante {1}",
                                joker, ante);
                            if (requiredEdition.HasValue)
                            {
                                DebugLogger.LogFormat("[ProcessLegendaryJokerFromSoul] Checking edition: joker has {0}, required {1}",
                                    joker.Edition, requiredEdition.Value);
                                return joker.Edition == requiredEdition.Value;
                            }
                            return true;
                        }
                        else
                        {
                            DebugLogger.LogFormat("[----------------] Joker {0} does not match target {1} in ante {2}",
                                (int)joker.Type, (int)targetItemType, ante);
                        }
                    }
                }
                else if (packType == MotelyBoosterPackType.Celestial)
                {
                    // Celestial packs can also contain Soul cards
                    var celestialStream = singleCtx.CreateCelestialPackPlanetStream(ante);
                    var contents = singleCtx.GetCelestialPackContents(ref celestialStream, pack.GetPackSize());
                    
                    // Check if Soul card is in the pack
                    if (contents.Contains(MotelyItemType.Soul))
                    {
                        DebugLogger.LogFormat("[=ProcessLegendaryJokerFromSoul] Found Soul card in Celestial pack in ante {0}", ante);
                        var stream = singleCtx.CreateSoulJokerStream(ante);
                        var joker = singleCtx.NextJoker(ref stream);
                        
                        // Check like PerkeoObservatoryDesc does
                        if (joker.Type == (targetItemType | MotelyItemType.Joker))
                        {
                            DebugLogger.LogFormat("[ProcessLegendaryJokerFromSoul] Found legendary joker {0} from Celestial Soul in ante {1}",
                                joker, ante);
                            if (requiredEdition.HasValue)
                            {
                                DebugLogger.LogFormat("[ProcessLegendaryJokerFromSoul] Checking edition: joker has {0}, required {1}",
                                    joker.Edition, requiredEdition.Value);
                                return joker.Edition == requiredEdition.Value;
                            }
                            return true;
                        }
                        else
                        {
                            DebugLogger.LogFormat("[----------------] Joker {0} does not match target {1} in ante {2} from Celestial pack",
                                (int)joker.Type, (int)targetItemType, ante);
                        }
                    }
                }
            }
            return false;
        }

        private static bool CheckForAnySoulJokerWithEdition(ref MotelySingleSearchContext singleCtx, int ante, MotelyItemEdition? requiredEdition)
        {
            int totalPacks = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante);
            
            bool spectralStreamInit = false;
            MotelySingleSpectralStream spectralStream = default;

            DebugLogger.LogFormat("[CheckForAnySoulJokerWithEdition] Looking for any soul joker with edition {0} in ante {1}",
                requiredEdition?.ToString() ?? "None", ante);
            
            for (int i = 0; i < totalPacks; i++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                var packType = pack.GetPackType();
                
                if (packType == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = singleCtx.CreateSpectralPackStream(ante);
                    }
                    if (singleCtx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                    {
                        DebugLogger.LogFormat("[CheckForAnySoulJokerWithEdition] Found Soul card in Spectral pack in ante {0}", ante);
                        var stream = singleCtx.CreateSoulJokerStream(ante);
                        var joker = singleCtx.NextJoker(ref stream);
                        
                        // We found a soul joker - check if edition matches (if required)
                        if (!requiredEdition.HasValue || joker.Edition == requiredEdition.Value)
                        {
                            DebugLogger.LogFormat("[CheckForAnySoulJokerWithEdition] Found soul joker {0} with matching edition {1}",
                                joker.Type, joker.Edition);
                            return true;
                        }
                        else
                        {
                            DebugLogger.LogFormat("[CheckForAnySoulJokerWithEdition] Soul joker {0} has edition {1}, but required {2}",
                                joker.Type, joker.Edition, requiredEdition.Value);
                        }
                    }
                }
            }
            return false;
        }

        private static bool CheckPlayingCard(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire need, int ante)
        {
            // For now, only check the starting deck for a matching card
            // TODO: StartingDeck not yet implemented in upstream
            DebugLogger.LogFormat("[CheckPlayingCard] PlayingCard check not yet implemented - StartingDeck support needed");
            if (true) // singleCtx.StartingDeck == null || singleCtx.StartingDeck.Count == 0)
                return false;

            // TODO: Fix type conversion from MotelyItem to MotelyPlayingCard
            // foreach (var card in singleCtx.StartingDeck)
            // {
            //     if (MatchesPlayingCardCriteria(card, need))
            //     {
            //         DebugLogger.LogFormat("[CheckPlayingCard] Matched card in starting deck: {0}", card);
            //         return true;
            //     }
            // }
            // return false;
        }
        
        private static bool MatchesPlayingCardCriteria(MotelyPlayingCard card, 
            OuijaConfig.Desire need)
        {
            // Extract rank and suit from the card enum
            var cardRank = (MotelyPlayingCardRank)((int)card & 0xF);
            var cardSuit = (MotelyPlayingCardSuit)((int)card & (0b11 << Motely.PlayingCardSuitOffset));
            
            // Check rank
            if (!need.AnyRank && need.RankEnum.HasValue && cardRank != need.RankEnum.Value)
                return false;
                
            // Check suit  
            if (!need.AnySuit && need.SuitEnum.HasValue && cardSuit != need.SuitEnum.Value)
                return false;
            
            // TODO: Check enhancement, seal, edition
            // This requires accessing the full card state, not just the base enum
            
            return true;
        }

        // Typed versions of check methods for hot path optimization
        private static bool CheckShopJokerTyped(ref MotelySingleSearchContext singleCtx, 
            TypedOuijaConfig.TypedDesire need, int ante)
        {
            // Only check shop if IncludeShopStream is true
            if (!need.IncludeShopStream)
                return false;
            
            var shop = singleCtx.GenerateFullShop(ante);
            int maxShopSlots = (ante == 1) ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
            
            var targetJoker = need.JokerValue;
            bool isAnyJoker = ((int)targetJoker == 0); // 0 means "any" joker
            
            DebugLogger.LogFormat("[CheckShopJokerTyped] Checking shop in ante {0} for {1} with edition {2}", 
                ante, isAnyJoker ? "ANY joker" : targetJoker.ToString(), need.RequiredEdition);
                
            for (int i = 0; i < maxShopSlots; i++)
            {
                ref var item = ref shop.Items[i];
                if (item.Type == ShopState.ShopItem.ShopItemType.Joker)
                {
                    // For "any" joker, just check edition
                    if (isAnyJoker)
                    {
                        // Check edition if required
                        if (need.RequiredEdition != MotelyItemEdition.None && item.Edition != need.RequiredEdition)
                            continue;
                            
                        DebugLogger.LogFormat("[CheckShopJokerTyped] MATCH! Found ANY joker {0} with required edition {1}!", 
                            item.Joker, need.RequiredEdition);
                        return true;
                    }
                    else
                    {
                        // Check specific joker
                        var itemMotelyItem = new MotelyItem(item.Joker, item.Edition);
                        var targetMotelyItem = new MotelyItem(targetJoker, MotelyItemEdition.None);
                        
                        if (itemMotelyItem.Type == targetMotelyItem.Type)
                        {
                            // Check edition if required
                            if (need.RequiredEdition != MotelyItemEdition.None && item.Edition != need.RequiredEdition)
                                continue;
                                
                            DebugLogger.LogFormat("[CheckShopJokerTyped] MATCH! Found {0} with required edition {1}!", 
                                targetJoker, need.RequiredEdition);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool CheckJokerInBuffoonPacks(ref MotelySingleSearchContext singleCtx,
            TypedOuijaConfig.TypedDesire need, int ante)
        {
            // For now, just return false - buffoon pack checking needs to be implemented
            // TODO: Implement when buffoon pack joker stream methods are available
            DebugLogger.LogFormat("[CheckJokerInBuffoonPacks] Buffoon pack checking not yet implemented for ante {0}", ante);
            return false;
        }

        private static bool CheckJokerInTags(ref MotelySingleSearchContext singleCtx,
            TypedOuijaConfig.TypedDesire need, int ante)
        {
            var targetJoker = need.JokerValue;
            bool isAnyJoker = ((int)targetJoker == 0); // 0 means "any" joker
            
            // Check tags that can spawn jokers
            var tagStream = singleCtx.CreateTagStream(ante);
            
            // Check small blind tag
            var smallBlindTag = singleCtx.GetNextTag(ref tagStream);
            if (smallBlindTag == MotelyTag.RareTag || smallBlindTag == MotelyTag.UncommonTag)
            {
                // These tags spawn jokers - check if they match
                var jokerRarity = smallBlindTag == MotelyTag.RareTag ? MotelyJokerRarity.Rare : MotelyJokerRarity.Uncommon;
                
                // We'd need to check what joker the tag would spawn
                // For now, return true if we're looking for any joker
                if (isAnyJoker)
                {
                    DebugLogger.LogFormat("[CheckJokerInTags] Found joker-spawning tag {0} in ante {1}", smallBlindTag, ante);
                    return true;
                }
            }
            
            // Check big blind tag
            var bigBlindTag = singleCtx.GetNextTag(ref tagStream);
            if (bigBlindTag == MotelyTag.RareTag || bigBlindTag == MotelyTag.UncommonTag)
            {
                if (isAnyJoker)
                {
                    DebugLogger.LogFormat("[CheckJokerInTags] Found joker-spawning tag {0} in ante {1}", bigBlindTag, ante);
                    return true;
                }
            }
            
            // Check boss blind tag (if applicable)
            if (ante % 3 == 0)
            {
                var bossBlindTag = singleCtx.GetNextTag(ref tagStream);
                if (bossBlindTag == MotelyTag.RareTag || bossBlindTag == MotelyTag.UncommonTag)
                {
                    if (isAnyJoker)
                    {
                        DebugLogger.LogFormat("[CheckJokerInTags] Found joker-spawning tag {0} in ante {1}", bossBlindTag, ante);
                        return true;
                    }
                }
            }
            
            return false;
        }

        private static bool CheckTarotInShopOrPacksTyped(ref MotelySingleSearchContext singleCtx, 
            TypedOuijaConfig.TypedDesire need, int ante)
        {
            bool found = false;
            
            // Check shop if enabled
            if (need.IncludeShopStream)
            {
                // TODO: Implement shop tarot checking
                // found = CheckTarotInShop(ref singleCtx, need.TarotValue, ante);
            }
            
            // Check packs if enabled and not found yet
            if (!found && need.IncludeBoosterPacks)
            {
                found = CheckTarotInPacks(ref singleCtx, need.TarotValue, ante);
            }
            
            // Check tags if enabled and not found yet
            if (!found && need.IncludeSkipTags)
            {
                // TODO: Check if tarot tags exist
            }
            
            return found;
        }

        private static bool CheckTarotInPacks(ref MotelySingleSearchContext singleCtx, 
            MotelyTarotCard targetTarot, int ante)
        {
            var targetTarotType = (MotelyItemType)MotelyItemTypeCategory.TarotCard | (MotelyItemType)targetTarot;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool tarotStreamInit = false;
            MotelySingleTarotStream tarotStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                    }
                    
                    var packContents = singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize());
                    if (packContents.Contains(targetTarotType))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPlanetInShopOrPacksTyped(ref MotelySingleSearchContext singleCtx, 
            TypedOuijaConfig.TypedDesire need, int ante)
        {
            bool found = false;
            
            // Check shop if enabled
            if (need.IncludeShopStream)
            {
                // TODO: Implement shop planet checking
                // found = CheckPlanetInShop(ref singleCtx, need.PlanetValue, ante);
            }
            
            // Check packs if enabled and not found yet
            if (!found && need.IncludeBoosterPacks)
            {
                found = CheckPlanetInPacks(ref singleCtx, need.PlanetValue, ante);
            }
            
            // Check tags if enabled and not found yet
            if (!found && need.IncludeSkipTags)
            {
                // TODO: Check if planet tags exist (e.g., Meteor tag)
            }
            
            return found;
        }

        private static bool CheckPlanetInPacks(ref MotelySingleSearchContext singleCtx, 
            MotelyPlanetCard targetPlanet, int ante)
        {
            var targetPlanetType = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)targetPlanet;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool planetStreamInit = false;
            MotelySinglePlanetStream planetStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                {
                    if (!planetStreamInit)
                    {
                        planetStreamInit = true;
                        planetStream = singleCtx.CreateCelestialPackPlanetStreamCached(ante);
                    }
                    
                    var packContents = singleCtx.GetCelestialPackContents(ref planetStream, pack.GetPackSize());
                    if (packContents.Contains(targetPlanetType))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckSpectralInShopOrPacksTyped(ref MotelySingleSearchContext singleCtx, 
            TypedOuijaConfig.TypedDesire need, int ante)
        {
            bool found = false;
            
            // Check shop if enabled
            if (need.IncludeShopStream)
            {
                // TODO: Implement shop spectral checking
                // found = CheckSpectralInShop(ref singleCtx, need.SpectralValue, ante);
            }
            
            // Check packs if enabled and not found yet
            if (!found && need.IncludeBoosterPacks)
            {
                found = CheckSpectralInPacks(ref singleCtx, need.SpectralValue, ante);
            }
            
            // Check tags if enabled and not found yet
            if (!found && need.IncludeSkipTags)
            {
                // TODO: Check if spectral tags exist
            }
            
            return found;
        }

        private static bool CheckSpectralInPacks(ref MotelySingleSearchContext singleCtx, 
            MotelySpectralCard targetSpectral, int ante)
        {
            var targetSpectralType = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)targetSpectral;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool spectralStreamInit = false;
            MotelySingleSpectralStream spectralStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = singleCtx.CreateSpectralPackStream(ante);
                    }
                    
                    var packContents = singleCtx.GetSpectralPackContents(ref spectralStream, pack.GetPackSize());
                    if (packContents.Contains(targetSpectralType))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPlayingCardTyped(ref MotelySingleSearchContext singleCtx, 
            TypedOuijaConfig.TypedDesire need, int ante)
        {
            // TODO: Implement playing card checking when deck access is available
            DebugLogger.LogFormat("[CheckPlayingCardTyped] PlayingCard check not yet implemented");
            return false;
        }

        // Check needs that couldn't be vectorized - typed version for hot path
        private static bool CheckNonVectorizedNeedsTyped(ref MotelySingleSearchContext singleCtx, TypedOuijaConfig config)
        {
            if (config.Needs == null || config.Needs.Length == 0)
                return true;

            // Perkeo-style: If any voucher need is present in any ante, activate/pass
            var voucherNeeds = config.Needs.Where(n => n.Type == TypedOuijaConfig.DesireType.Voucher).ToArray();
            if (voucherNeeds.Length > 0)
            {
                bool foundAnyVoucher = false;
                foreach (var need in voucherNeeds)
                {
                    var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : Array.Empty<int>();
                    foreach (var ante in searchAntes)
                    {
                        var voucher = singleCtx.GetAnteFirstVoucher(ante);
                        DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Voucher check ante {0}: Found={1}, Target={2}", 
                            ante, voucher, need.VoucherValue);
                        if (voucher == need.VoucherValue)
                        {
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] MATCH: Voucher {0} found at ante {1}", voucher, ante);
                            foundAnyVoucher = true;
                        }
                    }
                }
                if (!foundAnyVoucher)
                    return false; // No voucher need satisfied
            }

            // Only process truly non-vectorizable needs
            var nonVectorizableNeeds = config.Needs.Where(n => 
                n.Type == TypedOuijaConfig.DesireType.SoulJoker ||
                n.Type == TypedOuijaConfig.DesireType.Joker ||
                n.Type == TypedOuijaConfig.DesireType.Tarot ||
                n.Type == TypedOuijaConfig.DesireType.Planet ||
                n.Type == TypedOuijaConfig.DesireType.Spectral ||
                n.Type == TypedOuijaConfig.DesireType.PlayingCard ||
false // Boss type doesn't exist yet
            ).ToArray();

            if (nonVectorizableNeeds.Length == 0)
                return true; // No non-vectorizable needs, always pass

            foreach (var need in nonVectorizableNeeds)
            {
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : Array.Empty<int>();
                bool foundInAnyAnte = false;
                
                DebugLogger.LogFormat("[Search Antes.......]  {0}", searchAntes.Length);

                foreach (var ante in searchAntes)
                {
                    DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Checking ante {0} for need type {1}", ante, need.Type);
                    
                    switch (need.Type)
                    {
                        case TypedOuijaConfig.DesireType.SoulJoker:
                        case TypedOuijaConfig.DesireType.Joker when GetJokerRarity(need.JokerValue) == MotelyJokerRarity.Legendary:
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Processing SoulJoker {0} with edition {1} in ante {2}",
                                need.JokerValue, need.RequiredEdition, ante);
                            
                            // Special handling for "any" soul joker (JokerValue = 0)
                            if (need.JokerValue == (MotelyJoker)0)
                            {
                                // Check if The Soul card appears and generates ANY legendary joker with the required edition
                                if (CheckForAnySoulJokerWithEdition(ref singleCtx, ante, need.RequiredEdition))
                                {
                                    foundInAnyAnte = true;
                                }
                            }
                            else
                            {
                                if (ProcessLegendaryJokerFromSoul(ref singleCtx, need.JokerValue, ante, need.RequiredEdition))
                                {
                                    foundInAnyAnte = true;
                                }
                            }
                            break;
                            
                        case TypedOuijaConfig.DesireType.Joker:
                            bool foundJoker = false;
                            
                            // Check shop jokers if enabled
                            if (need.IncludeShopStream && CheckShopJokerTyped(ref singleCtx, need, ante))
                            {
                                DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Found shop joker {0} with edition {1} in ante {2}",
                                    need.JokerValue, need.RequiredEdition, ante);
                                foundJoker = true;
                            }
                            
                            // Check buffoon packs if enabled
                            if (!foundJoker && need.IncludeBoosterPacks)
                            {
                                if (CheckJokerInBuffoonPacks(ref singleCtx, need, ante))
                                {
                                    DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Found joker {0} with edition {1} in buffoon pack in ante {2}",
                                        need.JokerValue, need.RequiredEdition, ante);
                                    foundJoker = true;
                                }
                            }
                            
                            // Check tags if enabled (for regular jokers in tags like Rare Tag, Uncommon Tag, etc.)
                            if (!foundJoker && need.IncludeSkipTags)
                            {
                                if (CheckJokerInTags(ref singleCtx, need, ante))
                                {
                                    DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Found joker {0} with edition {1} from tag in ante {2}",
                                        need.JokerValue, need.RequiredEdition, ante);
                                    foundJoker = true;
                                }
                            }
                            
                            if (foundJoker)
                                foundInAnyAnte = true;
                            break;
                            
                        case TypedOuijaConfig.DesireType.Tarot:
                            // Check tarot in shop or packs based on source filters
                            if (CheckTarotInShopOrPacksTyped(ref singleCtx, need, ante))
                            {
                                foundInAnyAnte = true;
                            }
                            break;
                            
                        case TypedOuijaConfig.DesireType.Planet:
                            // Check planet in shop or packs based on source filters
                            if (CheckPlanetInShopOrPacksTyped(ref singleCtx, need, ante))
                            {
                                foundInAnyAnte = true;
                            }
                            break;
                            
                        case TypedOuijaConfig.DesireType.Spectral:
                            // Check spectral in shop or packs based on source filters
                            if (CheckSpectralInShopOrPacksTyped(ref singleCtx, need, ante))
                            {
                                foundInAnyAnte = true;
                            }
                            break;
                            
                        case TypedOuijaConfig.DesireType.PlayingCard:
                            // Check playing cards in various locations
                            if (CheckPlayingCardTyped(ref singleCtx, need, ante))
                            {
                                foundInAnyAnte = true;
                            }
                            break;
                            
                        // Boss type doesn't exist in TypedOuijaConfig.DesireType yet
                        // TODO: Add Boss case when boss blind generation is implemented
                            
                        case TypedOuijaConfig.DesireType.Voucher:
                            // Non-vectorized voucher need check
                            var voucher = singleCtx.GetAnteFirstVoucher(ante);
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Voucher check ante {0}: Found={1}, Target={2}", 
                                ante, voucher, need.VoucherValue);
                            if (voucher == need.VoucherValue)
                            {
                                DebugLogger.LogFormat("[CheckNonVectorizedNeeds] MATCH: Voucher {0} found at ante {1}", voucher, ante);
                                foundInAnyAnte = true;
                            }
                            break;
                    }
                    
                    if (foundInAnyAnte)
                        break;
                }
                
                if (!foundInAnyAnte)
                    return false; // Need not satisfied
            }
            
            return true;
        }

        // Check needs that couldn't be vectorized - original version for backward compatibility
        private static bool CheckNonVectorizedNeeds(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            if (config?.Needs == null || config.Needs.Length == 0)
                return true;

            // Perkeo-style: If any voucher need is present in any ante, activate/pass
            var voucherNeeds = config.Needs.Where(n => n != null && string.Equals(n.Type, "Voucher", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (voucherNeeds.Length > 0)
            {
                bool foundAnyVoucher = false;
                foreach (var need in voucherNeeds)
                {
                    var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : Array.Empty<int>();
                    foreach (var ante in searchAntes)
                    {
                        if (need.VoucherEnum.HasValue)
                        {
                            var voucher = singleCtx.GetAnteFirstVoucher(ante);
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Voucher check ante {0}: Found={1}, Target={2}", ante, voucher, need.VoucherEnum.Value);
                            if (voucher == need.VoucherEnum.Value)
                            {
                                DebugLogger.LogFormat("[CheckNonVectorizedNeeds] MATCH: Voucher {0} found at ante {1}", voucher, ante);
                                foundAnyVoucher = true;
                            }
                        }
                        else
                        {
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] VoucherEnum missing for need {0}", need.Value);
                        }
                    }
                }
                if (!foundAnyVoucher)
                    return false; // No voucher need satisfied
            }

            // Only process truly non-vectorizable needs
            var nonVectorizableNeeds = config.Needs.Where(n => n != null && (
                string.Equals(n.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase) ||
                n.TypeCategory == MotelyItemTypeCategory.Joker ||
                n.TypeCategory == MotelyItemTypeCategory.TarotCard ||
                n.TypeCategory == MotelyItemTypeCategory.PlanetCard ||
                n.TypeCategory == MotelyItemTypeCategory.SpectralCard ||
                n.TypeCategory == MotelyItemTypeCategory.PlayingCard ||
                string.Equals(n.Type, "Boss", StringComparison.OrdinalIgnoreCase)
            )).ToArray();

            if (nonVectorizableNeeds.Length == 0)
                return true; // No non-vectorizable needs, always pass

            foreach (var need in nonVectorizableNeeds)
            {
                var searchAntes = need.SearchAntes?.Length > 0 ? need.SearchAntes : Array.Empty<int>();
                bool foundInAnyAnte = false;
                
                DebugLogger.LogFormat("[Search Antes.......]  {0}", searchAntes.Length);

                foreach (int ante in searchAntes)
                {
                    DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Processing ante {0} for need {1}", ante, need.Value);
                }
                foreach (var ante in searchAntes)
                {
                    DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Checking ante {0} for need {1}", ante, need.Value);
                    // Check needs that require individual processing
                    var typeCat = need.TypeCategory;

                    if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase) ||
                        (typeCat == MotelyItemTypeCategory.Joker && GetJokerRarity(need.JokerEnum ?? default) == MotelyJokerRarity.Legendary))
                    {
                        DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Processing SoulJoker need {0} with edition {1} in ante {2}",
                            need.Value, need.ParsedEdition?.ToString() ?? "None", ante);
                        // Check legendary/soul jokers
                        if (need.JokerEnum.HasValue)
                        {
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Checking legendary joker {0} in ante {1}",
                                need.JokerEnum.Value, ante);

                            if (ProcessLegendaryJokerFromSoul(ref singleCtx, need.JokerEnum.Value, ante, need.ParsedEdition))
                            {
                                foundInAnyAnte = true;
                                break;
                            }
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.Joker)
                    {
                        // Check shop jokers
                        if (CheckShopJoker(ref singleCtx, need, ante))
                        {
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Found shop joker {0} with edition {1} in ante {2}",
                                need.JokerEnum?.ToString() ?? need.Value, need.ParsedEdition?.ToString() ?? "None", ante);
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.TarotCard)
                    {
                        // Check tarot in shop or packs based on source filters
                        if (CheckTarotInShopOrPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.PlanetCard)
                    {
                        // Check planet in shop or packs based on source filters
                        if (CheckPlanetInShopOrPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.SpectralCard)
                    {
                        // Check spectral in shop or packs based on source filters
                        if (CheckSpectralInShopOrPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.PlayingCard)
                    {
                        // Check playing cards in various locations
                        if (CheckPlayingCard(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (string.Equals(need.Type, "Boss", StringComparison.OrdinalIgnoreCase))
                    {
                        // Boss blind checking would go here
                        // TODO: Implement when boss blind generation is added to Motely
                        DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Boss blind checking not yet implemented");
                    }
                    else if (string.Equals(need.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
                    {
                        // Non-vectorized voucher need check
                        if (need.VoucherEnum.HasValue)
                        {
                            var voucher = singleCtx.GetAnteFirstVoucher(ante);
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] Voucher check ante {0}: Found={1}, Target={2}", ante, voucher, need.VoucherEnum.Value);
                            if (voucher == need.VoucherEnum.Value)
                            {
                                DebugLogger.LogFormat("[CheckNonVectorizedNeeds] MATCH: Voucher {0} found at ante {1}", voucher, ante);
                                foundInAnyAnte = true;
                                break;
                            }
                        }
                        else
                        {
                            DebugLogger.LogFormat("[CheckNonVectorizedNeeds] VoucherEnum missing for need {0}", need.Value);
                        }
                    }
                }
                
                if (!foundInAnyAnte)
                    return false; // Need not satisfied
            }
            
            return true;
        }

        private static bool CheckShopJoker(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            // Check if shop searching is enabled
            if (!need.IncludeShopStream)
            {
                DebugLogger.LogFormat("[CheckShopJoker] Shop search disabled for {0}", need.Value);
                return false;
            }
            
            var shop = singleCtx.GenerateFullShop(ante);
            int maxShopSlots = (ante == 1) ? ShopState.ShopSlotsAnteOne : ShopState.ShopSlots;
            
            // Handle "any" joker
            if (!need.JokerEnum.HasValue || string.Equals(need.Value, "any", StringComparison.OrdinalIgnoreCase))
            {
                DebugLogger.LogFormat("[CheckShopJoker] Looking for ANY joker with edition {0}", need.ParsedEdition?.ToString() ?? "any");
                
                for (int i = 0; i < maxShopSlots; i++)
                {
                    ref var item = ref shop.Items[i];
                    if (item.Type == ShopState.ShopItem.ShopItemType.Joker)
                    {
                        // Check edition if needed
                        if (need.ParsedEdition.HasValue && item.Edition != need.ParsedEdition.Value)
                            continue;
                            
                        DebugLogger.LogFormat("[CheckShopJoker] MATCH! Found ANY joker {0} with edition {1}!", 
                            item.Joker, item.Edition);
                        return true;
                    }
                }
                return false;
            }
            
            var targetJoker = need.JokerEnum.Value;
            
            DebugLogger.LogFormat("[CheckShopJoker] Checking shop in ante {0} for {1} with edition {2}", 
                ante, targetJoker, need.ParsedEdition?.ToString() ?? "any");
            for (int i = 0; i < maxShopSlots; i++)
            {
                ref var item = ref shop.Items[i];
                if (item.Type == ShopState.ShopItem.ShopItemType.Joker)
                {
                    var jokerItem = new MotelyItem(item.Joker, item.Edition);
                    var itemJokerBaseDebug = (MotelyJokerCommon)((int)item.Joker & ~Motely.JokerRarityMask);
                    var targetJokerBaseDebug = (MotelyJokerCommon)((int)targetJoker & ~Motely.JokerRarityMask);
                    DebugLogger.LogFormat("[CheckShopJoker] Shop slot {0}: {1} ({2}) base={3} ({4}) vs target base={5} ({6}), edition {7}, mask={8}", 
                        i, item.Joker, (int)item.Joker, itemJokerBaseDebug, (int)itemJokerBaseDebug, targetJokerBaseDebug, (int)targetJokerBaseDebug, item.Edition, Motely.JokerRarityMask);
                }
                // Create MotelyItem wrappers for proper comparison
                var itemMotelyItem = new MotelyItem(item.Joker, item.Edition);
                var targetMotelyItem = new MotelyItem(targetJoker, MotelyItemEdition.None);
                
                if (item.Type == ShopState.ShopItem.ShopItemType.Joker && itemMotelyItem.Type == targetMotelyItem.Type)
                {
                    DebugLogger.LogFormat("[CheckShopJoker] Found {0} with edition {1} (looking for {2})", 
                        item.Joker, item.Edition, need.ParsedEdition?.ToString() ?? "any");
                    
                    // Check edition if needed
                    if (need.ParsedEdition.HasValue && item.Edition != need.ParsedEdition.Value)
                        continue;
                        
                    // Check stickers if needed
                    if (need.ParsedStickers.Count > 0)
                    {
                        // TODO: Check if ShopState.ShopItem has sticker information
                        // For now, assume stickers aren't tracked in shop state
                        DebugLogger.LogFormat("[CheckShopJoker] Sticker checking not yet implemented");
                    }
                    
                    DebugLogger.LogFormat("[CheckShopJoker] MATCH! Found {0} with required edition {1}!", 
                        targetJoker, need.ParsedEdition?.ToString() ?? "any");
                    return true;
                }
            }
            return false;
        }

        private static bool CheckTarotInPacks(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.TarotEnum.HasValue)
                return false;
                
            var targetTarot = (MotelyItemType)MotelyItemTypeCategory.TarotCard | (MotelyItemType)need.TarotEnum.Value;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool tarotStreamInit = false;
            MotelySingleTarotStream tarotStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                    }
                    
                    var packContents = singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize());
                    if (packContents.Contains(targetTarot))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckPlanetInPacks(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.PlanetEnum.HasValue)
                return false;
                
            var targetPlanet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)need.PlanetEnum.Value;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool planetStreamInit = false;
            MotelySinglePlanetStream planetStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Celestial)
                {
                    if (!planetStreamInit)
                    {
                        planetStreamInit = true;
                        planetStream = singleCtx.CreateCelestialPackPlanetStreamCached(ante);
                    }
                    
                    var packContents = singleCtx.GetCelestialPackContents(ref planetStream, pack.GetPackSize());
                    if (packContents.Contains(targetPlanet))
                        return true;
                }
            }
            return false;
        }

        private static bool CheckSpectralInPacks(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.SpectralEnum.HasValue)
                return false;
                
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)need.SpectralEnum.Value;
            int packsToCheck = ante == 1 ? 4 : 6;
            var boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            
            bool spectralStreamInit = false;
            MotelySingleSpectralStream spectralStream = default;
            
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                
                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = singleCtx.CreateSpectralPackStream(ante);
                    }
                    var packContents = singleCtx.GetSpectralPackContents(ref spectralStream, pack.GetPackSize());

                    // Copy pack contents to array for safe logging
                    var packTypes = new string[packContents.Length];
                    for (int i = 0; i < packContents.Length; i++)
                        packTypes[i] = packContents.GetItem(i).Type.ToString();
                    DebugLogger.Log("[DEBUG] Spectral pack contents: " + string.Join(", ", packTypes));

                    // Check if the target spectral is in the pack contents
                    if (packContents.Contains(targetSpectral))
                        return true;
                }
            }
            return false;
        }

        public static ConcurrentQueue<OuijaResult>? ResultsQueue { get; set; } = new ConcurrentQueue<OuijaResult>();
        public static bool IsCancelled { get; set; } = false;

        private static OuijaResult ProcessWantsAndScoreTyped(ref MotelySingleSearchContext singleCtx, 
            TypedOuijaConfig typedConfig, OuijaConfig originalConfig)
        {
            string seed = singleCtx.GetSeed();
            var result = new OuijaResult
            {
                Seed = seed,
                TotalScore = 1,
                NaturalNegativeJokers = 0,
                DesiredNegativeJokers = 0,
                ScoreWants = new int[32],
                Success = true
            };

            if (typedConfig.Wants == null || typedConfig.Wants.Length == 0)
                return result;

            // Process each want individually
            for (int wantIndex = 0; wantIndex < typedConfig.Wants.Length && wantIndex < 32; wantIndex++)
            {
                var want = typedConfig.Wants[wantIndex];
                var searchAntes = want.SearchAntes?.Length > 0 ? want.SearchAntes : Array.Empty<int>();
                
                foreach (var ante in searchAntes)
                {
                    bool foundInThisAnte = false;
                    
                    switch (want.Type)
                    {
                        case TypedOuijaConfig.DesireType.Joker:
                        case TypedOuijaConfig.DesireType.SoulJoker:
                            // Check for jokers using typed methods
                            if (want.Type == TypedOuijaConfig.DesireType.SoulJoker || 
                                GetJokerRarity(want.JokerValue) == MotelyJokerRarity.Legendary)
                            {
                                // Special handling for "any" soul joker (JokerValue = 0)
                                if (want.JokerValue == (MotelyJoker)0)
                                {
                                    foundInThisAnte = CheckForAnySoulJokerWithEdition(ref singleCtx, ante, want.RequiredEdition);
                                }
                                else
                                {
                                    foundInThisAnte = ProcessLegendaryJokerFromSoul(ref singleCtx, want.JokerValue, ante, want.RequiredEdition);
                                }
                            }
                            else
                            {
                                // Check shop if enabled
                                if (want.IncludeShopStream)
                                {
                                    foundInThisAnte = CheckShopJokerTyped(ref singleCtx, want, ante);
                                }
                                
                                // Check buffoon packs if enabled and not found yet
                                if (!foundInThisAnte && want.IncludeBoosterPacks)
                                {
                                    foundInThisAnte = CheckJokerInBuffoonPacks(ref singleCtx, want, ante);
                                }
                                
                                // Check tags if enabled and not found yet
                                if (!foundInThisAnte && want.IncludeSkipTags)
                                {
                                    foundInThisAnte = CheckJokerInTags(ref singleCtx, want, ante);
                                }
                            }
                            break;
                            
                        case TypedOuijaConfig.DesireType.Tarot:
                            foundInThisAnte = CheckTarotInShopOrPacksTyped(ref singleCtx, want, ante);
                            break;
                            
                        case TypedOuijaConfig.DesireType.Planet:
                            foundInThisAnte = CheckPlanetInShopOrPacksTyped(ref singleCtx, want, ante);
                            break;
                            
                        case TypedOuijaConfig.DesireType.Spectral:
                            foundInThisAnte = CheckSpectralInShopOrPacksTyped(ref singleCtx, want, ante);
                            break;
                            
                        case TypedOuijaConfig.DesireType.Tag:
                        case TypedOuijaConfig.DesireType.SmallBlindTag:
                        case TypedOuijaConfig.DesireType.BigBlindTag:
                            // Check tags
                            var tagStream = singleCtx.CreateTagStream(ante);
                            var smallBlindTag = singleCtx.GetNextTag(ref tagStream);
                            var bigBlindTag = singleCtx.GetNextTag(ref tagStream);
                            
                            if (want.Type == TypedOuijaConfig.DesireType.SmallBlindTag)
                                foundInThisAnte = smallBlindTag == want.TagValue;
                            else if (want.Type == TypedOuijaConfig.DesireType.BigBlindTag)
                                foundInThisAnte = bigBlindTag == want.TagValue;
                            else
                                foundInThisAnte = smallBlindTag == want.TagValue || bigBlindTag == want.TagValue;
                            break;
                            
                        case TypedOuijaConfig.DesireType.Voucher:
                            var voucher = singleCtx.GetAnteFirstVoucher(ante);
                            foundInThisAnte = voucher == want.VoucherValue;
                            break;
                            
                        case TypedOuijaConfig.DesireType.PlayingCard:
                            foundInThisAnte = CheckPlayingCardTyped(ref singleCtx, want, ante);
                            break;
                    }
                    
                    if (foundInThisAnte)
                    {
                        result.ScoreWants[wantIndex] += want.Score;
                        result.TotalScore += want.Score;
                        
                        // Count negative jokers if configured
                        if ((want.Type == TypedOuijaConfig.DesireType.Joker || want.Type == TypedOuijaConfig.DesireType.SoulJoker) && 
                            want.RequiredEdition == MotelyItemEdition.Negative)
                        {
                            if (typedConfig.ScoreDesiredNegatives)
                                result.DesiredNegativeJokers++;
                        }
                    }
                }
            }
            
            // Check for natural negative jokers if configured
            if (typedConfig.ScoreNaturalNegatives)
            {
                // TODO: Implement natural negative joker counting
            }
            
            return result;
        }

        private static OuijaResult ProcessWantsAndScore(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            string seed = singleCtx.GetSeed();
            var result = new OuijaResult
            {
                Seed = seed,
                TotalScore = 1,
                NaturalNegativeJokers = 0,
                DesiredNegativeJokers = 0,
                ScoreWants = new int[32],
                Success = true
            };

            if (config?.Wants == null || config.Wants.Length == 0)
                return result;

            // Process each want individually
            for (int wantIndex = 0; wantIndex < config.Wants.Length && wantIndex < 32; wantIndex++)
            {
                var want = config.Wants[wantIndex];
                if (want == null) continue;
                
                var searchAntes = want.SearchAntes?.Length > 0 ? want.SearchAntes : Array.Empty<int>();
                
                foreach (var ante in searchAntes)
                {
                    bool foundInThisAnte = false;
                    
                    // Route to appropriate handler based on type
                    var typeCat = want.TypeCategory;
                    
                    if (string.Equals(want.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                    {
                        if (want.JokerEnum.HasValue)
                        {
                            foundInThisAnte = ProcessLegendaryJokerFromSoul(ref singleCtx, 
                                want.JokerEnum.Value, ante, want.ParsedEdition);
                        }
                            
                        if (foundInThisAnte)
                        {
                            result.ScoreWants[wantIndex] += want.Score;
                            if (want.ParsedEdition == MotelyItemEdition.Negative)
                                result.DesiredNegativeJokers++;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.Joker)
                    {
                        // Check if we should search in shop
                        if (!want.IncludeShopStream)
                        {
                            DebugLogger.LogFormat("[ProcessWantsAndScore] Shop search disabled for joker want {0}", want.Value);
                            // TODO: Check booster packs if IncludeBoosterPacks is true
                            continue;
                        }
                        
                        // Handle shop jokers with proper edition checking
                        var shop = singleCtx.GenerateFullShop(ante);
                        
                        if (!want.JokerEnum.HasValue || string.Equals(want.Value, "any", StringComparison.OrdinalIgnoreCase))
                        {
                            // "any" joker - check if ANY joker exists in shop with matching edition
                            DebugLogger.LogFormat("ProcessWantsAndScore: Looking for ANY joker with edition {0}", want.ParsedEdition?.ToString() ?? "any");
                            
                            for (int i = 0; i < ShopState.ShopSlots; i++)
                            {
                                ref var item = ref shop.Items[i];
                                
                                if (item.Type == ShopState.ShopItem.ShopItemType.Joker)
                                {
                                    // Check edition
                                    bool editionMatches = !want.ParsedEdition.HasValue || item.Edition == want.ParsedEdition.Value;
                                    
                                    if (editionMatches)
                                    {
                                        DebugLogger.LogFormat("[ProcessWantsAndScore] ANY Shop Joker MATCH: {0} with edition {1} at ante {2}", 
                                            item.Joker, item.Edition, ante);
                                        
                                        foundInThisAnte = true;
                                        result.ScoreWants[wantIndex] += want.Score;
                                        
                                        if (item.Edition == MotelyItemEdition.Negative && want.ParsedEdition == MotelyItemEdition.Negative)
                                        {
                                            result.DesiredNegativeJokers++;
                                        }
                                        break; // Found a joker, stop searching
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Specific joker
                            var targetJoker = want.JokerEnum.Value;
                            
                            for (int i = 0; i < ShopState.ShopSlots; i++)
                            {
                                ref var item = ref shop.Items[i];
                                // Strip rarity bits for comparison
                                var itemJokerBase = (MotelyJokerCommon)((int)item.Joker & ~Motely.JokerRarityMask);
                                var targetJokerBase = (MotelyJokerCommon)((int)targetJoker & ~Motely.JokerRarityMask);
                                
                                if (item.Type == ShopState.ShopItem.ShopItemType.Joker && itemJokerBase == targetJokerBase)
                                {
                                    // Check edition
                                    bool editionMatches = !want.ParsedEdition.HasValue || item.Edition == want.ParsedEdition.Value;
                                    
                                    if (editionMatches)
                                    {
                                        DebugLogger.LogFormat("[ProcessWantsAndScore] Shop Joker MATCH: {0} with edition {1} (required: {2}) at ante {3}", 
                                            item.Joker, item.Edition, want.ParsedEdition?.ToString() ?? "None", ante);
                                        
                                        foundInThisAnte = true;
                                        result.ScoreWants[wantIndex] += want.Score;
                                        
                                        if (item.Edition == MotelyItemEdition.Negative && want.ParsedEdition == MotelyItemEdition.Negative)
                                        {
                                            result.DesiredNegativeJokers++;
                                        }
                                        break; // Found the joker, stop searching
                                    }
                                }
                            }
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.TarotCard)
                    {
                        foundInThisAnte = CheckTarotInShopOrPacks(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (typeCat == MotelyItemTypeCategory.PlanetCard)
                    {
                        foundInThisAnte = CheckPlanetInShopOrPacks(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (typeCat == MotelyItemTypeCategory.SpectralCard)
                    {
                        foundInThisAnte = CheckSpectralInShopOrPacks(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (string.Equals(want.Type, "Tag", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(want.Type, "SmallBlindTag", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(want.Type, "BigBlindTag", StringComparison.OrdinalIgnoreCase))
                    {
                        foundInThisAnte = CheckTagForAnte(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (string.Equals(want.Type, "Voucher", StringComparison.OrdinalIgnoreCase))
                    {
                    foundInThisAnte = CheckVoucherForAnte(ref singleCtx, want, ante);
                    if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                        else if (string.Equals(want.Type, "Boss", StringComparison.OrdinalIgnoreCase))
                            {
                    foundInThisAnte = CheckBossForAnte(ref singleCtx, want, ante);
                    if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                }
                    else if (typeCat == MotelyItemTypeCategory.PlayingCard)
                    {
                        foundInThisAnte = CheckPlayingCard(ref singleCtx, want, ante);
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
            }
        }
            
            // Calculate total score
            for (int i = 0; i < 32; i++)
            {
                result.TotalScore += result.ScoreWants[i];
            }
            
            return result;
        }

        // Helper methods for checking in shop or packs
        private static bool CheckPlanetInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.PlanetEnum.HasValue)
                return false;
                
            var targetPlanet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | (MotelyItemType)want.PlanetEnum.Value;
            
            // Check shop first if enabled
            if (want.IncludeShopStream)
            {
                var planetStream = singleCtx.CreateShopPlanetStream(ante);
                var planet = singleCtx.GetNextShopPlanet(ref planetStream);
                if (planet.Type == targetPlanet)
                    return true;
            }
                
            // Check packs if enabled
            if (want.IncludeBoosterPacks)
            {
                return CheckPlanetInPacks(ref singleCtx, want, ante);
            }
            
            return false;
        }

        private static bool CheckTarotInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.TarotEnum.HasValue)
                return false;
                
            bool found = false;
            
            // Check shop if enabled (Tarots DO appear in shops!)
            if (want.IncludeShopStream)
            {
                // TODO: Implement shop tarot checking
                // var tarotStream = singleCtx.CreateShopTarotStream(ante);
                // found = CheckTarotInShop(ref singleCtx, want, ante);
            }
            
            // Check Arcana packs if enabled and not already found
            if (!found && want.IncludeBoosterPacks)
            {
                found = CheckTarotInPacks(ref singleCtx, want, ante);
            }
            
            return found;
        }

        private static bool CheckSpectralInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.SpectralEnum.HasValue)
                return false;
                
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)want.SpectralEnum.Value;

            // Check shop first if enabled
            if (want.IncludeShopStream)
            {
                // TODO only in ghost deck...
                //var spectralStream = singleCtx.CreateShopSpectralStream(ante);
                //var spectral = singleCtx.GetNextShopSpectral(ref spectralStream);
                //if (spectral.Type == targetSpectral)
                //    return true;
            }
                
            // Check packs if enabled
            if (want.IncludeBoosterPacks)
            {
                return CheckSpectralInPacks(ref singleCtx, want, ante);
            }
            
            return false;
        }

        private static bool CheckTagForAnte(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.TagEnum.HasValue)
                return false;
                
            var targetTag = want.TagEnum.Value;
            var tagStream = singleCtx.CreateTagStreamCached(ante);
            
            var smallBlindTag = singleCtx.GetNextTag(ref tagStream);
            var bigBlindTag = singleCtx.GetNextTag(ref tagStream);
            
            // Check based on the type
            if (string.Equals(want.Type, "SmallBlindTag", StringComparison.OrdinalIgnoreCase))
            {
                return smallBlindTag == targetTag;
            }
            else if (string.Equals(want.Type, "BigBlindTag", StringComparison.OrdinalIgnoreCase))
            {
                return bigBlindTag == targetTag;
            }
            else // Default "Tag" matches either
            {
                return smallBlindTag == targetTag || bigBlindTag == targetTag;
            }
        }

        private static bool CheckVoucherForAnte(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.VoucherEnum.HasValue)
                return false;
                
            var targetVoucher = want.VoucherEnum.Value;
            var voucher = singleCtx.GetAnteFirstVoucher(ante);
            return voucher == targetVoucher;
        }

        private static bool CheckBossForAnte(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            // TODO: Boss blind generation not yet implemented in Motely
            // According to Immolate source, bosses have their own RNG stream (R_Boss) separate from tags
            DebugLogger.LogFormat("[WARNING] Boss blind checking not yet implemented");
            return false;
        }

        // Reporting of results is now handled externally via Search.ReportResult(result).
        // This method only returns the OuijaResult object for reporting.
        // The obsolete PrintResult method has been removed.

        private static int GetMinimumScore(OuijaConfig? config)
        {
            return 1; // Require at least score 1 to filter out zero scores
        }

        private static MotelyJokerRarity GetJokerRarity(MotelyJoker joker)
        {
            return (MotelyJokerRarity)((int)joker & (0b11 << Motely.JokerRarityOffset));
        }

        // Check for legendary/soul jokers across all packs
        private static bool PerkeoStyleSoulJokerCheck(ref MotelySingleSearchContext singleCtx, MotelyJoker targetJoker, MotelyItemEdition? requiredEdition = null, int ante = 0)
        {
            // Convert MotelyJoker to the corresponding MotelyItemType for proper comparison
            int legendaryJokerBase = (int)targetJoker & ~(Motely.JokerRarityMask << Motely.JokerRarityOffset);
            MotelyItemType targetItemType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | legendaryJokerBase);
            // Check first booster pack for Arcana/Soul/targetJoker
            MotelySingleBoosterPackStream boosterPackStream = singleCtx.CreateBoosterPackStream(ante, true);
            MotelyBoosterPack pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
            {
                var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante);
                if (singleCtx.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                {
                    var soulJokerStream = singleCtx.CreateSoulJokerStream(ante);
                    var joker = singleCtx.NextJoker(ref soulJokerStream);
                    if (joker.Type == targetItemType)
                    {
                        if (requiredEdition.HasValue && joker.Edition != requiredEdition.Value)
                            return false;
                        DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetSeed());
                        return true;
                    }
                }
            }
            // Check all remaining packs
            int totalPacks = ante == 1 ? 4 : 6;
            boosterPackStream = singleCtx.CreateBoosterPackStream(ante, false);
            bool tarotStreamInit = false;
            MotelySingleTarotStream tarotStream2 = default;
            for (int i = 0; i < totalPacks; i++)
            {
                pack = singleCtx.GetNextBoosterPack(ref boosterPackStream);
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream2 = singleCtx.CreateArcanaPackTarotStream(ante);
                    }
                    if (singleCtx.GetArcanaPackContents(ref tarotStream2, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                    {
                        var soulJokerStream = singleCtx.CreateSoulJokerStream(ante);
                        var joker = singleCtx.NextJoker(ref soulJokerStream);
                        if (joker.Type == targetItemType)
                        {
                            if (requiredEdition.HasValue && joker.Edition != requiredEdition.Value)
                                continue;
                            DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetSeed());
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    public static OuijaJsonFilterDesc LoadFromFile(string configFileName)
    {
        var config = OuijaConfigLoader.Load(configFileName);
        return new OuijaJsonFilterDesc(config);
    }

    public static string[] GetWantsColumnNames(OuijaConfig config)
    {
        if (config?.Wants == null || config.Wants.Length == 0)
            return Array.Empty<string>();
        var names = new List<string>();
        foreach (var want in config.Wants)
        {
            if (want == null) continue;
            names.Add(want.GetDisplayString());
        }
        return names.ToArray();
    }
}