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
public struct MotelyJsonFilterDesc(FilterCategory category, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilterDesc<MotelyJsonFilterDesc.MotelyFilter>
{
    private readonly FilterCategory _category = category;
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses = clauses;

    public readonly string Name => $"MotelyFilter_{_category}";
    public readonly string Description => $"Filters {_category} items with vectorized optimization";

    public MotelyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in Clauses)
        {
            if (clause.Type != _category)
                Debug.Assert(false, $"MotelyJsonFilterDesc({_category}) contains clause of different type: {clause.Type}");
        }

        // Cache relevant streams based on category
        switch (_category)
        {
            case FilterCategory.Voucher:
                for (int ante = 1; ante <= 8; ante++)
                    ctx.CacheAnteFirstVoucher(ante);
                break;
            case FilterCategory.SoulJoker:
                ctx.CacheBoosterPackStream(1);
                ctx.CacheBoosterPackStream(2);
                ctx.CacheBoosterPackStream(8);
                break;
                // Add other categories as needed
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
            Debug.Assert(Clauses.Count > 0, $"MotelyFilter({_category}) called with empty clauses");

            return _category switch
            {
                FilterCategory.Voucher => FilterVouchers(ref searchContext),
                FilterCategory.Tag => FilterTags(ref searchContext),
                FilterCategory.Tarot => FilterTarots(ref searchContext),
                FilterCategory.Planet => FilterPlanets(ref searchContext),
                FilterCategory.Spectral => FilterSpectrals(ref searchContext),
                FilterCategory.Joker => FilterJokers(ref searchContext),
                FilterCategory.SoulJoker => FilterSoulJokers(ref searchContext),
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
            var state = new MotelyVectorRunStateVoucher();

            // Find max ante needed
            int maxAnte = Clauses.Max(c => c.EffectiveAntes?.Max() ?? 1);

            foreach (var clause in Clauses)
            {
                var clauseMask = VectorMask.NoBitsSet;

                foreach (var ante in clause.EffectiveAntes ?? [])
                {
                    if (ante <= maxAnte)
                    {
                        var vouchers = ctx.GetAnteFirstVoucher(ante, state);
                        var matches = VectorEnum256.Equals(vouchers, clause.VoucherEnum.Value);
                        clauseMask |= matches;

                        if (((VectorMask)matches).IsPartiallyTrue())
                        {
                            state.ActivateVoucher(clause.VoucherEnum.Value);
                        }
                    }
                }

                mask &= clauseMask;
                if (mask.IsAllFalse()) break;
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterTags(ref MotelyVectorSearchContext ctx)
        {
            var mask = VectorMask.AllBitsSet;

            foreach (var clause in Clauses)
            {
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
            // TODO: Implement vectorized planet filtering
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes ?? [])
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
            // TODO: Implement vectorized spectral filtering
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes ?? [])
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
            // TODO: Implement vectorized joker filtering for shop/pack slots
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes ?? [])
                    {
                        if (MotelyJsonScoring.CountJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
                            return true;
                    }
                }
                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterSoulJokers(ref MotelyVectorSearchContext ctx)
        {
            var clauses = Clauses; // Copy to local variable for lambda access
            return ctx.SearchIndividualSeeds((ref MotelySingleSearchContext singleCtx) =>
            {
                var runState = new MotelyRunState();
                foreach (var clause in clauses)
                {
                    foreach (var ante in clause.EffectiveAntes ?? [])
                    {
                        if (MotelyJsonScoring.CountSoulJokerOccurrences(ref singleCtx, clause, ante, ref runState, earlyExit: true) > 0)
                            return true;
                    }
                }
                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VectorMask FilterPlayingCards(ref MotelyVectorSearchContext ctx)
        {
            var clauses = Clauses; // Copy to local variable for lambda access
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
            // TODO: Implement after boss PRNG is fixed
            return VectorMask.AllBitsSet; // Pass-through for now
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