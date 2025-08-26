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
        //Debug.Assert(Clauses.Count > 0, $"MotelyJsonFilterDesc({_category}) called with empty clauses");
        //Debug.Assert(Clauses.All(c => c.Category == _category), $"MotelyJsonFilterDesc({_category}) called with mixed-category clauses");

        // Cache ALL relevant streams based on category (following NegativeCopyJokers pattern)
        switch (_category)
        {
            case FilterCategory.Voucher:
                // Cache all voucher antes like NegativeCopyJokers
                for (int ante = 1; ante <= 8; ante++)
                    ctx.CacheAnteFirstVoucher(ante);
                break;
            case FilterCategory.Joker:
                // Cache all pack streams like NegativeCopyJokers
                for (int ante = 1; ante <= 8; ante++)
                    ctx.CacheBoosterPackStream(ante);
                break;
            case FilterCategory.TarotCard:
            case FilterCategory.PlanetCard:
            case FilterCategory.SpectralCard:
            case FilterCategory.PlayingCard:
                // Cache all pack streams for comprehensive searching
                for (int ante = 1; ante <= 8; ante++)
                    ctx.CacheBoosterPackStream(ante);
                break;
        }
        
        return new MotelyFilter(_category, Clauses);
    }

    public struct MotelyFilter(FilterCategory category, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly FilterCategory _category = category;
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // If no clauses, pass everything through (for scoreOnly mode)
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
            // If no clauses, pass everything through (for scoreOnly mode)
            if (Clauses.Count == 0)
                return VectorMask.AllBitsSet;
                
            var mask = VectorMask.AllBitsSet;
            var state = new MotelyVectorRunState();

            // Find max ante needed
            int maxAnte = Clauses.Count > 0 ? Clauses.Max(c => c.EffectiveAntes?.Length > 0 ? c.EffectiveAntes.Max() : 1) : 1;

            // Check each voucher clause independently
            // Each clause needs to match at least one of its specified antes
            foreach (var clause in Clauses)
            {
                Debug.Assert(clause.VoucherEnum.HasValue, "FilterVouchers requires VoucherEnum");
                var clauseMask = VectorMask.NoBitsSet;

                // Check if this voucher appears at ANY of its required antes
                foreach (var ante in clause.EffectiveAntes ?? [])
                {
                    if (ante <= maxAnte)
                    {
                        var vouchers = ctx.GetAnteFirstVoucher(ante, state);
                        var matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                        
                        if (((VectorMask)matches).IsPartiallyTrue())
                        {
                            state.ActivateVoucher(clause.VoucherEnum.Value);
                            clauseMask |= matches;  // OR - voucher can appear at any of its antes
                        }
                    }
                }

                // This voucher must appear at at least one of its antes
                if (clauseMask.IsAllFalse())
                {
                    return VectorMask.NoBitsSet;  // Required voucher not found
                }
                
                mask &= clauseMask;  // AND - all required vouchers must be present
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
            // For now, fall back to individual processing until true vectorization
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes ?? [])
                    {
                        var tempState = new MotelyRunState();
                        if (MotelyJsonScoring.TarotCardsTally(ref singleCtx, clause, ante, ref tempState, earlyExit: true) > 0)
                            return true;
                    }
                }
                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterPlanets(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(PlanetCard) called with empty clauses");
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        if (MotelyJsonScoring.CountPlanetOccurrences(ref singleCtx, clause, ante, earlyExit: true) > 0)
                            return true;
                    }
                }
                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterSpectrals(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(SpectralCard) called with empty clauses");
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        if (MotelyJsonScoring.CountSpectralOccurrences(ref singleCtx, clause, ante, earlyExit: true) > 0)
                            return true;
                    }
                }
                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterJokers(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(Joker) called with empty clauses");
            var clauses = Clauses; // Copy to local variable for lambda access
            
            // Special optimization for PerkeoObservatory pattern
            if (clauses.Count == 1 && clauses[0].ItemTypeEnum == MotelyFilterItemType.SoulJoker && 
                clauses[0].JokerEnum == MotelyJoker.Perkeo)
            {
                // Capture clause to avoid closure issues
                var perkeoClause = clauses[0];
                var effectiveAntes = perkeoClause.EffectiveAntes ?? new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
                {
                    // Fast path for Perkeo check
                    foreach (var ante in effectiveAntes)
                    {
                        var packStream = singleCtx.CreateBoosterPackStream(ante, ante != 1, isCached: true);
                        for (int i = 0; i < 4; i++) // Check first 4 packs
                        {
                            var pack = singleCtx.GetNextBoosterPack(ref packStream);
                            
                            if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                            {
                                var tarotStream = singleCtx.CreateArcanaPackTarotStream(ante, soulOnly: true, isCached: true);
                                if (singleCtx.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                                {
                                    var soulStream = singleCtx.CreateSoulJokerStream(ante, isCached: true);
                                    if (singleCtx.GetNextJoker(ref soulStream).Type == MotelyItemType.Perkeo)
                                        return true;
                                }
                            }
                            else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                            {
                                var spectralStream = singleCtx.CreateSpectralPackSpectralStream(ante, soulOnly: true, isCached: true);
                                if (singleCtx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                                {
                                    var soulStream = singleCtx.CreateSoulJokerStream(ante, isCached: true);
                                    if (singleCtx.GetNextJoker(ref soulStream).Type == MotelyItemType.Perkeo)
                                        return true;
                                }
                            }
                        }
                    }
                    return false;
                });
            }
            
            // Regular path for other joker checks
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                
                // Each clause must be satisfied (AND between different jokers)
                foreach (var clause in clauses)
                {
                    bool clauseSatisfied = false;
                    
                    // Check if this joker appears in ANY of its required antes (OR across antes)
                    foreach (var ante in clause.EffectiveAntes)
                    {
                        // Handle both regular Jokers and SoulJokers
                        if (clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                        {
                            if (MotelyJsonScoring.CountSoulJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
                            {
                                clauseSatisfied = true;
                                break; // Found in this ante, clause is satisfied
                            }
                        }
                        else
                        {
                            if (MotelyJsonScoring.CountJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
                            {
                                clauseSatisfied = true;
                                break; // Found in this ante, clause is satisfied
                            }
                        }
                    }
                    
                    // If this joker wasn't found in any of its antes, fail
                    if (!clauseSatisfied)
                        return false;
                }
                
                // All joker clauses were satisfied
                return true;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterPlayingCards(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter(PlayingCard) called with empty clauses");
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes)
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