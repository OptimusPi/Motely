using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct PerkeoObservatoryFilterDesc() : IMotelySeedFilterDesc<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>
{

    public PerkeoObservatoryFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheAnteFirstVoucher(1);
        ctx.CacheAnteFirstVoucher(2);
        ctx.CacheBoosterPackStream(1);
        ctx.CacheBoosterPackStream(2);
        return new PerkeoObservatoryFilter();
    }

    public struct PerkeoObservatoryFilter() : IMotelySeedFilter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool CheckAnteForPerkeo(int ante, ref MotelySingleSearchContext searchContext)
        {
            MotelySingleTarotStream tarotStream = default;
            MotelySingleSpectralStream spectralStream = default;
            MotelySingleJokerFixedRarityStream soulStream = default;
            MotelySingleBoosterPackStream boosterPackStream = default;

            bool soulStreamInit = false;
            bool boosterPackStreamInit = false;


            bool tarotStreamInit = false, spectralStreamInit = false;

            if (!soulStreamInit)
            {
                soulStream = searchContext.CreateSoulJokerStream(ante);
                var wouldBe = searchContext.GetNextJoker(ref soulStream);
                if (wouldBe.Type != MotelyItemType.Perkeo) return false;
            }
            for (int i = 0; i < 2; i++)
            {
                if (!boosterPackStreamInit)
                {
                    boosterPackStream = searchContext.CreateBoosterPackStream(ante, ante != 1, false);
                    boosterPackStreamInit = true;
                }

                var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);
                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    }

                    if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                    {
                        return true;
                    }
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                    }

                    if (searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(1);

            VectorMask matching = VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);

            if (matching.IsAllFalse())
                return Vector512<double>.Zero;

            MotelyVectorRunState voucherState = new();
            voucherState.ActivateVoucher(MotelyVoucher.Telescope);

            vouchers = searchContext.GetAnteFirstVoucher(2, voucherState);

            matching &= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);


            return searchContext.SearchIndividualSeeds(matching, (ref MotelySingleSearchContext searchContext) =>
            {
                return CheckAnteForPerkeo(1, ref searchContext) || CheckAnteForPerkeo(2, ref searchContext);
            });
        }
    }
}
