using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Routes every surviving lane to a host-supplied predicate. The predicate
/// receives the seed string and returns whether the seed matches. No SIMD
/// pre-filter — pair with an upstream JAML/native filter when narrowing helps.
/// </summary>
public struct JsSeedPredicateDesc(Func<string, bool> predicate)
    : IMotelySeedFilterDesc<JsSeedPredicateDesc.JsSeedPredicateFilter>
{
    public readonly JsSeedPredicateFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        => new JsSeedPredicateFilter(predicate);

    public struct JsSeedPredicateFilter(Func<string, bool> predicate) : IMotelySeedFilter
    {
        private readonly Func<string, bool> _predicate = predicate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var predicate = _predicate;
            return ctx.SearchIndividualSeeds(
                VectorMask.AllBitsSet,
                (ref MotelySingleSearchContext s) => predicate(s.GetSeed())
            );
        }
    }
}
