using System.Runtime.Intrinsics;

namespace Motely;

public struct PerkeoObservatoryFilterDesc()
    : IMotelySeedFilterDesc<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>
{
    public PerkeoObservatoryFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheAnteFirstVoucher(1);

        return new PerkeoObservatoryFilter();
    }

    public struct PerkeoObservatoryFilter() : IMotelySeedFilter
    {
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Just check Telescope in ante 1 - SIMD only
            VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(1);
            return VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
        }
    }
}
