using System.Runtime.CompilerServices;

namespace Motely.Filters.Native;

/// <summary>
/// Additional filter that runs a <see cref="MotelyIndividualSeedSearcher"/> on each lane
/// (same hook as PerkeoObservatory's <c>SearchIndividualSeeds</c> block). Pair with JAML/SIMD
/// filters upstream when you need narrowing first.
/// </summary>
public readonly struct JimmolateFilterDesc(MotelyIndividualSeedSearcher searcher)
    : IMotelySeedFilterDesc<JimmolateFilterDesc.JimmolateFilter>
{
    private readonly MotelyIndividualSeedSearcher _searcher =
        searcher ?? throw new ArgumentNullException(nameof(searcher));

    public readonly JimmolateFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new(_searcher);

    public readonly struct JimmolateFilter(MotelyIndividualSeedSearcher searcher)
        : IMotelySeedFilter
    {
        private readonly MotelyIndividualSeedSearcher _searcher = searcher;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx) =>
            ctx.SearchIndividualSeeds(_searcher);
    }
}
