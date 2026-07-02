using System.Runtime.CompilerServices;

namespace Motely.Filters.Native;

/// <summary>
/// Jimmolate: a per-seed predicate ("does this seed pass?"), the classic Immolate <c>.cl</c>
/// filter mental model. Wraps a <see cref="MotelyIndividualSeedSearcher"/> and runs it against
/// every lane's live single-seed context. See <see cref="MotelyGlossary"/> for the full definition.
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
