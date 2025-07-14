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
                foreach (var ante in need.SearchAntes)
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
                foreach (var ante in want.SearchAntes)
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
                ctx.CachePseudoHash(MotelyPrngKeys.JokerSoul + ante);
                ctx.CacheTarotStream(ante);
                return;
            }
            else if (string.Equals(desire.Type, "Tag", StringComparison.OrdinalIgnoreCase))
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
                ctx.CachePseudoHash(MotelyPrngKeys.JokerCommon + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerUncommon + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerRare + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerLegendary + "sho" + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.JokerEdition + "sho" + ante);
                break;
            case MotelyItemTypeCategory.PlayingCard:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] PlayingCard streams not yet implemented for ante {0}", ante);
                break;
            case MotelyItemTypeCategory.TarotCard:
                ctx.CacheTarotStream(ante);
                ctx.CachePseudoHash(MotelyPrngKeys.Tarot + MotelyPrngKeys.ArcanaPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.TarotSoul + MotelyPrngKeys.Tarot + ante);
                break;
            case MotelyItemTypeCategory.PlanetCard:
                ctx.CachePseudoHash(MotelyPrngKeys.Planet + MotelyPrngKeys.Shop + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.Planet + MotelyPrngKeys.CelestialPack + ante);
                break;
            case MotelyItemTypeCategory.SpectralCard:
                ctx.CachePseudoHash(MotelyPrngKeys.Spectral + MotelyPrngKeys.Shop + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.Spectral + MotelyPrngKeys.SpectralPack + ante);
                ctx.CachePseudoHash(MotelyPrngKeys.SpectralSoul + ante);
                break;
            default:
                DebugLogger.LogFormat("[OuijaJsonFilterDesc] Unhandled canonical type '{0}' (ante {1}) - no PRNG key used", parsedType, ante);
                break;
        }
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        private readonly OuijaConfig _config;
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
                mask = ProcessNeeds(ref searchContext, mask, localConfig!);
                if (mask.IsAllFalse())
                {
                    DebugLogger.Log("All needs failed to match");
                    return mask;
                }
                DebugLogger.LogFormat("After needs, {0} seeds passing", CountBitsSet(mask));
                int cutoff = _cutoff;
                // Process results immediately as they are found
                var searchInstance = searchContext.SearchInstance;
                mask = searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
                {
                    if (!CheckNonVectorizedNeeds(ref singleCtx, localConfig!))
                    {
                        DebugLogger.Log("Non-vectorized needs failed to match");
                        return false;
                    }
                    var result = ProcessWantsAndScore(ref singleCtx, localConfig!);
                    if (result.Success && result.TotalScore >= cutoff)
                    {
                        // Print CSV directly with limited columns
                        var wantColumns = OuijaJsonFilterDesc.GetWantsColumnNames(localConfig!);
                        FancyConsole.WriteLine(result.ToCsvRow(localConfig!, wantColumns.Length));
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

        private static VectorMask ProcessNeeds(ref MotelyVectorSearchContext searchContext, VectorMask mask, OuijaConfig config)
        {
            if (config?.Needs == null || config.Needs.Length == 0)
            {
                DebugLogger.Log("No needs to process - returning full mask");
                return mask;
            }

            var voucherNeeds = config.Needs.Where(n => n != null && string.Equals(n.Type, "Voucher", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (voucherNeeds.Length > 0)
            {
                VectorMask voucherMask = VectorMask.AllBitsClear;
                foreach (var need in voucherNeeds)
                {
                    foreach (var ante in need.SearchAntes)
                    {
                        var voucherVec = searchContext.GetAnteFirstVoucher(ante);
                        if (!need.VoucherEnum.HasValue)
                        {
                            DebugLogger.Log("Missing VoucherEnum for Voucher need");
                            continue;
                        }
                        var targetVoucher = need.VoucherEnum.Value;
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
                mask &= voucherMask;
                if (mask.IsAllFalse())
                {
                    DebugLogger.Log("All seeds filtered out by voucher needs - no seeds passed requirements");
                    return mask;
                }
            }

            // Process each need
            foreach (var need in config.Needs.Where(n => n != null && !string.Equals(n.Type, "Voucher", StringComparison.OrdinalIgnoreCase)))
            {
                // Check if this is a non-vectorizable need
                bool isNonVectorizable = false;
                
                // SoulJoker needs can't be vectorized
                if (string.Equals(need.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.LogFormat("SoulJoker need {0} can't be vectorized", need.Value);
                    isNonVectorizable = true;
                }
                // ALL Joker needs currently can't be vectorized (shop generation not vectorized)
                else if (need.TypeCategory == MotelyItemTypeCategory.Joker)
                {
                    isNonVectorizable = true;
                }
                // Tarot needs can't be vectorized (pack checking not vectorized)
                else if (need.TypeCategory == MotelyItemTypeCategory.TarotCard)
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
                
                // For each need, we want to find seeds that have it in ANY of the specified antes (OR logic)
                VectorMask needMask = VectorMask.AllBitsClear;
                foreach (var ante in need.SearchAntes)
                {
                    var anteMask = ProcessNeedVector(ref searchContext, need, ante, VectorMask.AllBitsSet);
                    needMask |= anteMask; // OR operation - seed passes if found in ANY ante
                }
                // After collecting all antes for this need, mask is ANDed with needMask (all needs must be satisfied)
                mask &= needMask;
                int passing = CountBitsSet(mask);
                // Print the relevant enum property for debug clarity
                string itemStr = need.GetDisplayString();
                DebugLogger.LogFormat("After need (type={0}, item={1}): {2} seeds passing", need.Type, itemStr, passing);
                if (mask.IsAllFalse()) 
                {
                    DebugLogger.Log("All seeds filtered out by need - no seeds passed requirements");
                    return mask;
                }
            }
            return mask;
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
                if (string.Equals(need.Type, "Tag", StringComparison.OrdinalIgnoreCase))
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
            
            // Use vectorized spectral pack filter!
            var spectralPackMask = searchContext.FilterSpectralPack(ante, need.SpectralEnum.Value);
            return mask & spectralPackMask;
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
            var smallBlindMatch = VectorEnum256.Equals(smallBlindTag, targetTag);
            // Only require small blind to match
            DebugLogger.LogFormat("[DEBUG] Tag check ante {0}: SmallBlind={1} BigBlind={2} Target={3}", ante, smallBlindTag, bigBlindTag, targetTag);
            DebugLogger.LogFormat("[DEBUG] Tag match ante {0}: SmallMatch={1} FinalMask={2}", ante, smallBlindMatch, smallBlindMatch);
            return mask & smallBlindMatch;
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
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
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
                                return ((int)joker.Edition << Motely.ItemEditionOffset) == (int)requiredEdition.Value;
                            return true;
                        }
                        else
                        {
                            DebugLogger.LogFormat("[----------------] Joker {0} does not match target {1} in ante {2}",
                                (int)joker.Type, (int)targetItemType, ante);
                        }
                    }
                }
            }
            return false;
        }

        private static bool CheckPlayingCard(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire need, int ante)
        {
            // TODO: Playing card checking is not yet implemented
            // Shops don't contain booster packs - only jokers, tarots, and planets
            // Playing cards would need to check standard packs (not in shops) and starting deck
            return false;
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

        // Check needs that couldn't be vectorized
        private static bool CheckNonVectorizedNeeds(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            if (config?.Needs == null || config.Needs.Length == 0)
                return true;

            var voucherNeeds = config.Needs.Where(n => n != null && string.Equals(n.Type, "Voucher", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (voucherNeeds.Length > 0)
            {
                bool foundAnyVoucher = false;
                foreach (var need in voucherNeeds)
                {
                    foreach (var ante in need.SearchAntes)
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
                bool foundInAnyAnte = false;
                
                foreach (var ante in need.SearchAntes)
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
                        // Check tarot in packs
                        if (CheckTarotInPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.PlanetCard)
                    {
                        // Check planet in celestial packs (shop already checked by vector)
                        if (CheckPlanetInPacks(ref singleCtx, need, ante))
                        {
                            foundInAnyAnte = true;
                            break;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.SpectralCard)
                    {
                        // Check spectral in packs (shop already checked by vector)
                        if (CheckSpectralInPacks(ref singleCtx, need, ante))
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
                }
                
                if (!foundInAnyAnte)
                    return false; // Need not satisfied
            }
            
            return true;
        }

        private static bool CheckShopJoker(ref MotelySingleSearchContext singleCtx, OuijaConfig.Desire need, int ante)
        {
            if (!need.JokerEnum.HasValue)
            {
                DebugLogger.LogFormat("CheckShopJoker: No JokerEnum for need {0}", need.Value);
                return false;
            }
            
            var targetJoker = need.JokerEnum.Value;
            var shop = singleCtx.GenerateFullShop(ante);
            
            for (int i = 0; i < ShopState.ShopSlots; i++)
            {
                ref var item = ref shop.Items[i];
                if (item.Type == ShopState.ShopItem.ShopItemType.Joker && item.Joker == targetJoker)
                {
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
            {
                DebugLogger.Log("Missing SpectralEnum for Spectral need");
                throw new ArgumentException("SpectralEnum must be provided for spectral needs", nameof(need));
            }
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)need.SpectralEnum.Value;
            int packsToCheck = ante == 1 ? 4 : 6;
            for (int packIndex = 0; packIndex < packsToCheck; packIndex++)
            {
                var spectralStream = singleCtx.CreateSpectralPackStream(ante);
                var packContents = singleCtx.GetSpectralPackContents(ref spectralStream, MotelySingleItemSet.MaxLength); // Use correct pack size if available
                string spectralContentsStr = "";
                for (int i = 0; i < packContents.Length; i++)
                {
                    if (i > 0) spectralContentsStr += ",";
                    spectralContentsStr += packContents.GetItem(i).ToString();
                }
                DebugLogger.LogFormat("[DEBUG][CheckSpectralInPacks] Spectral pack ante={0}, packIndex={1}, contents=[{2}]", ante, packIndex + 1, spectralContentsStr);
                if (packContents.Contains(targetSpectral))
                {
                    DebugLogger.LogFormat("[DEBUG][CheckSpectralInPacks] Found targetSpectral={0} in Spectral pack ante={1}, packIndex={2}", targetSpectral, ante, packIndex + 1);
                    return true;
                }
            }
            return false;
        }

        public static ConcurrentQueue<OuijaResult>? ResultsQueue { get; set; } = new ConcurrentQueue<OuijaResult>();

        private static OuijaResult ProcessWantsAndScore(ref MotelySingleSearchContext singleCtx, OuijaConfig config)
        {
            string seed = singleCtx.GetCurrentSeed();
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
                
                var searchAntes = want.SearchAntes;
                
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
                            DebugLogger.LogFormat("[DEBUG][WantsMatch] wantIndex={0}, type={1}, value={2}, ante={3}, SCORE={4}", wantIndex, want.Type, want.Value, ante, want.Score);
                            result.ScoreWants[wantIndex] += want.Score;
                            if (want.ParsedEdition == MotelyItemEdition.Negative)
                                result.DesiredNegativeJokers++;
                        }
                    }
                    else if (typeCat == MotelyItemTypeCategory.Joker)
                    {
                        if (!want.JokerEnum.HasValue)
                        {
                            DebugLogger.LogFormat("ProcessWantsAndScore: No JokerEnum for joker want {0}", want.Value);
                            continue;
                        }
                        
                        // Handle shop jokers with proper edition checking
                        var shop = singleCtx.GenerateFullShop(ante);
                        var targetJoker = want.JokerEnum.Value;
                        
                        for (int i = 0; i < ShopState.ShopSlots; i++)
                        {
                            ref var item = ref shop.Items[i];
                            if (item.Type == ShopState.ShopItem.ShopItemType.Joker && item.Joker == targetJoker)
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
                        if (foundInThisAnte) {
                            DebugLogger.LogFormat("[DEBUG][WantsMatch] wantIndex={0}, type={1}, value={2}, ante={3}, SCORE={4} | foundInThisAnte={5}", wantIndex, want.Type, want.Value, ante, want.Score, foundInThisAnte);
                            // For SpectralCard wants, log pack contents for this ante
                            DebugLogger.LogFormat("[DEBUG][WantsMatch][Spectral] wantIndex={0}, ante={1}, foundInThisAnte={2}", wantIndex, ante, foundInThisAnte);
                        }
                        if (foundInThisAnte) result.ScoreWants[wantIndex] += want.Score;
                    }
                    else if (string.Equals(want.Type, "Tag", StringComparison.OrdinalIgnoreCase))
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
            
            // Check shop first
            var planetStream = singleCtx.CreateShopPlanetStream(ante);
            var planet = singleCtx.GetNextShopPlanet(ref planetStream);
            if (planet.Type == targetPlanet)
                return true;
                
            // Check packs
            return CheckPlanetInPacks(ref singleCtx, want, ante);
        }

        private static bool CheckTarotInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            // Tarot cards don't appear in shops, only in Arcana packs
            return CheckTarotInPacks(ref singleCtx, want, ante);
        }

        private static bool CheckSpectralInShopOrPacks(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.SpectralEnum.HasValue)
                return false;
                
            var targetSpectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)want.SpectralEnum.Value;
            DebugLogger.LogFormat("[DEBUG][CheckSpectralInShopOrPacks] Checking spectral {0} in ante {1}", targetSpectral, ante);
            
            // Check shop first
            var spectralStream = singleCtx.CreateShopSpectralStream(ante);


            var spectral = singleCtx.GetNextShopSpectral(ref spectralStream);

            DebugLogger.LogFormat("[DEBUG][CheckSpectralInShopOrPacks] Shop spectral: {0}", spectral.Type);
            // If spectral matches, return true
            if (spectral.Type == targetSpectral)
            {
                DebugLogger.LogFormat("[DEBUG][CheckSpectralInShopOrPacks] Found matching spectral: {0}", spectral.Type);
                return true;
            }
            DebugLogger.LogFormat("[DEBUG][CheckSpectralInShopOrPacks] No matching spectral in shop, checking packs");
            // Check packs
            return CheckSpectralInPacks(ref singleCtx, want, ante);
        }

        private static bool CheckTagForAnte(ref MotelySingleSearchContext singleCtx, 
            OuijaConfig.Desire want, int ante)
        {
            if (!want.TagEnum.HasValue)
                return false;
                
            var targetTag = want.TagEnum.Value;
            var tagStream = singleCtx.CreateTagStreamCached(ante);
            
            // Only require small blind to match
            var smallBlindTag = singleCtx.GetNextTag(ref tagStream);
            var bigBlindTag = singleCtx.GetNextTag(ref tagStream);
            return smallBlindTag == targetTag;
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
                        DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetCurrentSeed());
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
                            DebugLogger.LogFormat("[DEBUG] PerkeoStyleSoulJokerCheck: MATCH! Seed: {0}", singleCtx.GetCurrentSeed());
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