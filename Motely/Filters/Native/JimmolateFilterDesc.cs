using System.Runtime.CompilerServices;

namespace Motely.Filters.Native;

/// <summary>
/// Additional filter that runs a <see cref="MotelyIndividualSeedSearcher"/> on each lane
/// </summary>
public readonly struct JimmolateFilterDesc(MotelyIndividualSeedSearcher searcher)
    : IMotelySeedFilterDesc<JimmolateFilterDesc.JimmolateFilter>
{
    private readonly MotelyIndividualSeedSearcher _searcher = searcher;

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
