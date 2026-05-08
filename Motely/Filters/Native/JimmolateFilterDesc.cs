using System.Runtime.CompilerServices;

namespace Motely.Filters;

public delegate bool JimmolateSeedPredicate(ref MotelySingleSearchContext searchContext);

/// <summary>
/// Routes every surviving lane to a host-supplied predicate. The predicate
/// can inspect the full single-seed context and returns whether the seed matches.
/// No SIMD pre-filter — pair with an upstream JAML/native filter when narrowing helps.
/// </summary>
public readonly struct JimmolateFilterDesc
    : IMotelySeedFilterDesc<JimmolateFilterDesc.JimmolateFilter>
{
    private readonly JimmolateSeedPredicate _contextPredicate;

    public JimmolateFilterDesc(JimmolateSeedPredicate predicate)
    {
        _contextPredicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public readonly JimmolateFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new JimmolateFilter(_contextPredicate);

    public readonly struct JimmolateFilter(JimmolateSeedPredicate contextPredicate) : IMotelySeedFilter
    {
        private readonly JimmolateSeedPredicate _contextPredicate = contextPredicate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
            => ctx.SearchIndividualSeeds(MatchesSeedContext);

        private readonly bool MatchesSeedContext(ref MotelySingleSearchContext searchContext)
            => _contextPredicate(ref searchContext);
    }
}
