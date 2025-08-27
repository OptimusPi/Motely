using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct PassthroughFilterDesc() : IMotelySeedFilterDesc<PassthroughFilterDesc.PassthroughFilter>
{

    public PassthroughFilter CreateFilter(ref MotelyFilterCreationContext ctx)
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
