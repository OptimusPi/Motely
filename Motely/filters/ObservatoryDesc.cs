using System.Runtime.CompilerServices;

namespace Motely.Filters;

public struct ObservatoryDesc : IMotelySeedFilterDesc<ObservatoryDesc.ObservatoryFilter>
{
    public ObservatoryFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        for (int ante = 1; ante <= 4; ante++)
        {
            ctx.CacheAnteFirstVoucher(ante);
        }
        return new ObservatoryFilter();
    }

    public struct ObservatoryFilter : IMotelySeedFilter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var resultMask = VectorMask.NoBitsSet;
            var runState = new MotelyVectorRunState();
            
            for (int ante = 1; ante <= 4; ante++)
            {
                var voucher = ctx.GetAnteFirstVoucher(ante, runState);
                
                var isObservatory = VectorEnum256.Equals(voucher, MotelyVoucher.Observatory);
                resultMask |= isObservatory;
                
                runState.ActivateVoucher(voucher);
                
                if (resultMask.IsAllTrue())
                    return resultMask;
            }
            
            return resultMask;
        }
    }
}