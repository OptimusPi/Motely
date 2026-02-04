using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Utils;
using static Motely.Utils.NullCheckExtensions;

namespace Motely.Filters;

/// <summary>
/// Composite filter that directly calls multiple filters and combines their results
/// BYPASSES the broken batching system entirely!
/// </summary>
public struct MotelyCompositeFilterDesc(List<MotelyJsonConfig.MotelyJsonFilterClause> mustClauses)
    : IMotelySeedFilterDesc<MotelyCompositeFilterDesc.MotelyCompositeFilter>
{
    private readonly List<MotelyJsonConfig.MotelyJsonFilterClause> _mustClauses = mustClauses;

    public MotelyCompositeFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(_mustClauses);

        // Create individual filters for each category, tracking which are inverted
        var filterEntries = new List<(IMotelySeedFilter filter, bool isInverted)>();

        foreach (var kvp in clausesByCategory)
        {
            var category = kvp.Key;
            var clauses = kvp.Value;

            // CRITICAL FIX: And/Or clauses should NOT be grouped together!
            // Each individual and:/or: clause must be a SEPARATE filter that gets ANDed with others.
            // Otherwise, 3 separate "or:" in must get merged into ONE OrFilter (wrong!)
            if (category == FilterCategory.Or)
            {
                // Create a SEPARATE OrFilter for EACH or clause (they get ANDed together)
                foreach (var orClause in clauses)
                {
                    var singleOrFilter = CreateSingleOrFilter(orClause, ref ctx);
                    filterEntries.Add((singleOrFilter, orClause.IsInverted));
                }
                continue;
            }

            if (category == FilterCategory.And)
            {
                // Create a SEPARATE AndFilter for EACH and clause (they get ANDed together)
                foreach (var andClause in clauses)
                {
                    var singleAndFilter = CreateSingleAndFilter(andClause, ref ctx);
                    filterEntries.Add((singleAndFilter, andClause.IsInverted));
                }
                continue;
            }

            // Check if ALL clauses in this category are inverted (mustNot)
            bool isInverted = clauses.All(c => c.IsInverted);

            DebugLogger.Log(
                $"[COMPOSITE DESC] Category={category}, clauses.Count={clauses.Count}, isInverted={isInverted}, clause.IsInverted values=[{string.Join(",", clauses.Select(c => c.IsInverted))}]"
            );

            IMotelySeedFilter filter = category switch
            {
                FilterCategory.Joker => new MotelyJsonJokerFilterDesc(
                    MotelyJsonJokerFilterClause.CreateCriteria(
                        MotelyJsonJokerFilterClause.ConvertClauses(clauses)
                    )
                ).CreateFilter(ref ctx),
                FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(
                    MotelyJsonSpectralFilterClause.CreateCriteria(
                        MotelyJsonSpectralFilterClause.ConvertClauses(clauses)
                    )
                ).CreateFilter(ref ctx),
                FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(
                    MotelyJsonSoulJokerFilterClause.CreateCriteria(
                        MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)
                    )
                ).CreateFilter(ref ctx),
                FilterCategory.SoulJokerEditionOnly => new MotelyJsonSoulJokerEditionOnlyFilterDesc(
                    MotelyJsonSoulJokerFilterClause.CreateCriteria(
                        MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)
                    )
                ).CreateFilter(ref ctx),
                FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(
                    MotelyJsonTarotFilterClause.CreateCriteria(
                        MotelyJsonTarotFilterClause.ConvertClauses(clauses)
                    )
                ).CreateFilter(ref ctx),
                FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(
                    MotelyJsonPlanetFilterClause.CreateCriteria(
                        MotelyJsonPlanetFilterClause.ConvertClauses(clauses)
                    )
                ).CreateFilter(ref ctx),
                FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreatePlayingCardCriteria(clauses)
                ).CreateFilter(ref ctx),
                FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(
                    MotelyJsonVoucherFilterClause.CreateCriteria(
                        MotelyJsonVoucherFilterClause.ConvertClauses(clauses)
                    )
                ).CreateFilter(ref ctx),
                FilterCategory.Boss => new MotelyJsonBossFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateBossCriteria(clauses)
                ).CreateFilter(ref ctx),
                FilterCategory.Tag => new MotelyJsonTagFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateTagCriteria(clauses)
                ).CreateFilter(ref ctx),
                FilterCategory.Event => new MotelyJsonEventFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateEventCriteria(clauses)
                ).CreateFilter(ref ctx),
                FilterCategory.ErraticRank => new MotelyJsonErraticRankFilterDesc(
                    clauses[0].RankEnum
                        ?? throw new InvalidOperationException(
                            $"erraticRank requires a rank value (clause: {clauses[0].Value ?? "<none>"})"
                        ),
                    clauses[0].Min ?? 1
                ).CreateFilter(ref ctx),
                FilterCategory.ErraticSuit => new MotelyJsonErraticSuitFilterDesc(
                    clauses[0].SuitEnum
                        ?? throw new InvalidOperationException(
                            $"erraticSuit requires a suit value (clause: {clauses[0].Value ?? "<none>"})"
                        ),
                    clauses[0].Min ?? 1
                ).CreateFilter(ref ctx),
                FilterCategory.ErraticRankAndSuit => new MotelyJsonErraticRankAndSuitFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateErraticRankAndSuitCriteria(
                        clauses
                            .Select(c =>
                            {
                                if (c.RankEnum == null && c.SuitEnum == null)
                                    throw new InvalidOperationException(
                                        "erraticRankAndSuit requires rank and/or suit values"
                                    );
                                return c;
                            })
                            .ToList()
                    )
                ).CreateFilter(ref ctx),
                _ => throw new ArgumentException($"Unsupported filter category: {category}"),
            };
            filterEntries.Add((filter, isInverted));
        }

        return new MotelyCompositeFilter(filterEntries);
    }

    // Helper method to recursively clone a clause with a specific ante, propagating to ALL descendants
    private static MotelyJsonConfig.MotelyJsonFilterClause CloneClauseWithAnte(
        MotelyJsonConfig.MotelyJsonFilterClause source,
        int ante
    )
    {
        var cloned = new MotelyJsonConfig.MotelyJsonFilterClause
        {
            Type = source.Type,
            Value = source.Value,
            Values = source.Values,
            Label = source.Label,
            Antes = new[] { ante }, // SINGLE ante! Override with the propagated ante
            AntesWasExplicitlySet = true, // Mark as explicitly set since we're propagating from parent
            IsInverted = source.IsInverted,
            Score = source.Score,
            Mode = source.Mode,
            Min = source.Min,
            FilterOrder = source.FilterOrder,
            Edition = source.Edition,
            Stickers = source.Stickers,
            Suit = source.Suit,
            Rank = source.Rank,
            Seal = source.Seal,
            Enhancement = source.Enhancement,
            Sources = source.Sources,
            PackSlots = source.PackSlots,
            ShopSlots = source.ShopSlots,
            MinShopSlot = source.MinShopSlot,
            MaxShopSlot = source.MaxShopSlot,
            MinPackSlot = source.MinPackSlot,
            MaxPackSlot = source.MaxPackSlot,
        };

        // Copy parsed enums (already initialized by parent)
        cloned.CopyParsedEnumsFrom(source);

        // Recursively clone nested clauses with the same ante!
        if (!source.Clauses.IsNullOrEmpty() && source.Clauses != null)
        {
            cloned.Clauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
            foreach (var nestedClause in source.Clauses)
            {
                cloned.Clauses.Add(CloneClauseWithAnte(nestedClause, ante));
            }
        }

        return cloned;
    }

    /// <summary>
    /// Propagates antes from parent clause to all children recursively.
    /// Creates separate filter groups for each ante, then ORs them together.
    /// </summary>
    private static List<IMotelySeedFilter> PropagateAntesToChildren(
        MotelyJsonConfig.MotelyJsonFilterClause parentClause,
        ref MotelyFilterCreationContext ctx,
        bool isAndClause
    )
    {
        if (
            !parentClause.AntesWasExplicitlySet
            || parentClause.Antes.IsNullOrEmpty()
            || parentClause.Clauses.IsNullOrEmpty()
        )
        {
            return new List<IMotelySeedFilter>();
        }

        var anteSpecificFilters = new List<IMotelySeedFilter>();

        foreach (var ante in parentClause.Antes!)
        {
            if (isAndClause)
            {
                // For AND: Clone all children with this ante, create AND filter
                var clonedChildren = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                foreach (var child in parentClause.Clauses!)
                {
                    clonedChildren.Add(CloneClauseWithAnte(child, ante));
                }
                var anteComposite = new MotelyCompositeFilterDesc(clonedChildren);
                anteSpecificFilters.Add(anteComposite.CreateFilter(ref ctx));
            }
            else
            {
                // For OR: Clone each child separately with this ante, create individual filters
                foreach (var child in parentClause.Clauses!)
                {
                    var clonedChild = CloneClauseWithAnte(child, ante);
                    var singleClauseList = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                    {
                        clonedChild,
                    };
                    var nestedComposite = new MotelyCompositeFilterDesc(singleClauseList);
                    anteSpecificFilters.Add(nestedComposite.CreateFilter(ref ctx));
                }
            }
        }

        return anteSpecificFilters;
    }

    private static IMotelySeedFilter CreateAndFilter(
        List<MotelyJsonConfig.MotelyJsonFilterClause> andClauses,
        ref MotelyFilterCreationContext ctx
    )
    {
        // AND filter: ALL nested clauses must pass
        var nestedFilters = new List<IMotelySeedFilter>();

        foreach (var andClause in andClauses)
        {
            if (andClause.Clauses.IsNullOrEmpty())
                continue; // Skip empty And clause

            // Check if Antes was EXPLICITLY SET (not just defaulted)
            // If explicitly set, use helper behavior (propagate to children)
            // If defaulted, respect individual child Antes
            var anteFilters = PropagateAntesToChildren(andClause, ref ctx, isAndClause: true);
            if (anteFilters.Count > 0)
            {
                // Wrap all ante-specific ANDs in an OR
                nestedFilters.Add(new OrFilter(anteFilters));
            }
            else
            {
                // No antes array on parent - just process normally
                if (andClause.Clauses != null)
                {
                    var nestedComposite = new MotelyCompositeFilterDesc(andClause.Clauses);
                    nestedFilters.Add(nestedComposite.CreateFilter(ref ctx));
                }
            }
        }

        return new AndFilter(nestedFilters);
    }

    private static IMotelySeedFilter CreateOrFilter(
        List<MotelyJsonConfig.MotelyJsonFilterClause> orClauses,
        ref MotelyFilterCreationContext ctx
    )
    {
        // OR filter: at least ONE nested clause must pass
        var nestedFilters = new List<IMotelySeedFilter>();

        foreach (var orClause in orClauses)
        {
            if (orClause.Clauses.IsNullOrEmpty())
                continue; // Skip empty Or clause

            // Check if parent OR clause has Antes EXPLICITLY SET (not just defaulted)
            // If Antes was explicitly set, use helper behavior (propagate to children)
            // If Antes was defaulted (not explicitly set), respect individual child Antes
            var anteFilters = PropagateAntesToChildren(orClause, ref ctx, isAndClause: false);
            if (anteFilters.Count > 0)
            {
                nestedFilters.AddRange(anteFilters);
            }
            else
            {
                // No antes array on parent - process normally
                // Each clause in the OR should be its own branch
                // If we have ["King", "Queen", "Jack"], we want "King OR Queen OR Jack"
                // NOT "(King AND Queen AND Jack) as one group"
                // So we create a separate filter for EACH individual clause
                if (orClause.Clauses != null)
                {
                    foreach (var individualClause in orClause.Clauses)
                    {
                        // Create a composite filter with just this one clause
                        // This prevents same-type items from being grouped together
                        var singleClauseList = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                        {
                            individualClause,
                        };
                        var nestedComposite = new MotelyCompositeFilterDesc(singleClauseList);
                        nestedFilters.Add(nestedComposite.CreateFilter(ref ctx));
                    }
                }
            }
        }

        return new OrFilter(nestedFilters);
    }

    /// <summary>
    /// Create an OrFilter for a SINGLE or: clause (used to ensure multiple or: clauses in must are ANDed)
    /// </summary>
    private static IMotelySeedFilter CreateSingleOrFilter(
        MotelyJsonConfig.MotelyJsonFilterClause orClause,
        ref MotelyFilterCreationContext ctx
    )
    {
        var nestedFilters = new List<IMotelySeedFilter>();

        if (orClause.Clauses == null || orClause.Clauses.Count == 0)
            return new OrFilter(nestedFilters); // Empty Or fails all

        // Check if parent OR clause has Antes EXPLICITLY SET
        if (orClause.AntesWasExplicitlySet && orClause.Antes != null && orClause.Antes.Length > 0)
        {
            // Clone each child clause for each ante, then OR them all together
            foreach (var ante in orClause.Antes)
            {
                foreach (var child in orClause.Clauses)
                {
                    var clonedChild = CloneClauseWithAnte(child, ante);
                    var singleClauseList = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                    {
                        clonedChild,
                    };
                    var nestedComposite = new MotelyCompositeFilterDesc(singleClauseList);
                    nestedFilters.Add(nestedComposite.CreateFilter(ref ctx));
                }
            }
        }
        else
        {
            // No antes array on parent - create separate filter for each child clause
            foreach (var individualClause in orClause.Clauses)
            {
                var singleClauseList = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                {
                    individualClause,
                };
                var nestedComposite = new MotelyCompositeFilterDesc(singleClauseList);
                nestedFilters.Add(nestedComposite.CreateFilter(ref ctx));
            }
        }

        return new OrFilter(nestedFilters);
    }

    /// <summary>
    /// Create an AndFilter for a SINGLE and: clause (used to ensure multiple and: clauses in must are ANDed)
    /// </summary>
    private static IMotelySeedFilter CreateSingleAndFilter(
        MotelyJsonConfig.MotelyJsonFilterClause andClause,
        ref MotelyFilterCreationContext ctx
    )
    {
        var nestedFilters = new List<IMotelySeedFilter>();

        if (andClause.Clauses.IsNullOrEmpty())
            return new AndFilter(nestedFilters); // Empty And fails all

        // Check if Antes was EXPLICITLY SET
        var anteFilters = PropagateAntesToChildren(andClause, ref ctx, isAndClause: true);
        if (anteFilters.Count > 0)
        {
            // Wrap all ante-specific ANDs in an OR
            return new OrFilter(anteFilters);
        }
        else
        {
            // No antes array on parent - just process normally
            if (andClause.Clauses != null)
            {
                var nestedComposite = new MotelyCompositeFilterDesc(andClause.Clauses);
                return nestedComposite.CreateFilter(ref ctx);
            }
            return new AndFilter(new List<IMotelySeedFilter>());
        }
    }

    public struct MotelyCompositeFilter : IMotelySeedFilter
    {
        private readonly List<(IMotelySeedFilter filter, bool isInverted)> _filterEntries;

        public MotelyCompositeFilter(
            List<(IMotelySeedFilter filter, bool isInverted)> filterEntries
        )
        {
            _filterEntries = filterEntries;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            // Start with all bits set
            VectorMask result = VectorMask.AllBitsSet;

            // Call each filter directly and AND the results (Must logic)
            foreach (var (filter, isInverted) in _filterEntries)
            {
                // Early exit BEFORE calling filter if no seeds left
                if (result.IsAllFalse())
                    return VectorMask.NoBitsSet;

                var filterMask = filter.Filter(ref ctx);

                // If this is a mustNot filter (inverted), negate the mask
                if (isInverted)
                {
                    var beforeInvert = filterMask.Value;
                    filterMask = ~filterMask;
                    var afterInvert = filterMask.Value;
                    DebugLogger.Log(
                        $"[COMPOSITE FILTER] Inverted filter: before=0x{beforeInvert:X2}, after=0x{afterInvert:X2}"
                    );
                }

                result &= filterMask;
            }

            return result;
        }
    }

    /// <summary>
    /// AND Filter - ALL nested filters must pass
    /// </summary>
    public struct AndFilter : IMotelySeedFilter
    {
        private readonly List<IMotelySeedFilter> _nestedFilters;

        public AndFilter(List<IMotelySeedFilter> nestedFilters)
        {
            _nestedFilters = nestedFilters;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_nestedFilters == null || _nestedFilters.Count == 0)
                return VectorMask.NoBitsSet; // Empty AND fails all

            // Start with all bits set, AND together all nested results
            VectorMask result = VectorMask.AllBitsSet;

            foreach (var filter in _nestedFilters)
            {
                result &= filter.Filter(ref ctx);

                if (result.IsAllFalse())
                    return VectorMask.NoBitsSet; // Early exit
            }

            return result;
        }
    }

    /// <summary>
    /// OR Filter - at least ONE nested filter must pass
    /// </summary>
    public struct OrFilter : IMotelySeedFilter
    {
        private readonly List<IMotelySeedFilter> _nestedFilters;

        public OrFilter(List<IMotelySeedFilter> nestedFilters)
        {
            _nestedFilters = nestedFilters;
        }

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_nestedFilters == null || _nestedFilters.Count == 0)
                return VectorMask.NoBitsSet; // Empty OR fails all

            // Start with no bits set, OR together all nested results
            VectorMask result = VectorMask.NoBitsSet;

            foreach (var filter in _nestedFilters)
            {
                result |= filter.Filter(ref ctx);
            }

            return result;
        }
    }
}
