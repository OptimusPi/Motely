namespace Motely.Filters.Native;

public struct PassthroughFilterDesc()
    : IMotelySeedFilterDesc<PassthroughFilterDesc.PassthroughFilter>
{
    public readonly PassthroughFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new PassthroughFilter();
    }

    public struct PassthroughFilter() : IMotelySeedFilter
    {
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            return VectorMask.AllBitsSet;
        }
    }
}
