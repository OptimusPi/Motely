
using System.Runtime.Intrinsics;

namespace Motely;

public struct PerkeoObservatoryFilterDesc() : IMotelySeedFilterDesc<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>
{

    public PerkeoObservatoryFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheVoucherStream(1);
        ctx.CacheVoucherStream(2);

        return new PerkeoObservatoryFilter();
    }

    public struct PerkeoObservatoryFilter() : IMotelySeedFilter
    {

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(1);

            VectorMask matching = VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);

            if (matching.IsAllFalse())
                return matching;

            MotelyVectorRunStateVoucher voucherState = new();
            voucherState.ActivateVoucher(MotelyVoucher.Telescope);

            vouchers = searchContext.GetAnteFirstVoucher(2, voucherState);

            matching &= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);

            return searchContext.SearchIndividualSeeds(matching, (ref MotelySingleSearchContext searchContext) =>
            {
                MotelySingleTarotStream tarotStream = default;

                MotelySingleBoosterPackStream boosterPackStream = searchContext.CreateBoosterPackStream(1, true);

                MotelyBoosterPack pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    tarotStream = searchContext.CreateArcanaPackTarotStream(1);

                    if (searchContext.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                    {
                        MotelySingleJokerFixedRarityStream stream = searchContext.CreateSoulJokerStream(1);
                        var nJoker = searchContext.NextJoker(ref stream);
                        if (nJoker.Edition > 0 && nJoker.Type == MotelyItemType.Perkeo)
                            if (((int)nJoker.Edition << Motely.ItemEditionOffset) == (int)MotelyItemEdition.Negative)
                            {
                                return nJoker.Type == MotelyItemType.Perkeo;
                            }
                    }
                }

                boosterPackStream = searchContext.CreateBoosterPackStream(2);
                bool tarotStreamInit = false;

                for (int i = 0; i < 3; i++)
                {
                    pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        if (!tarotStreamInit)
                        {
                            tarotStreamInit = true;
                            tarotStream = searchContext.CreateArcanaPackTarotStream(2);
                        }

                        if (searchContext.GetArcanaPackContents(ref tarotStream, pack.GetPackSize()).Contains(MotelyItemType.Soul))
                        {
                            MotelySingleJokerFixedRarityStream stream = searchContext.CreateSoulJokerStream(2);
                            var nJoker = searchContext.NextJoker(ref stream);

                            if (((int)nJoker.Edition << Motely.ItemEditionOffset) == (int)MotelyItemEdition.Negative)
                            {
                                return nJoker.Type == MotelyItemType.Perkeo;
                            }
                        }
                    }
                }

                return false;

            });
        }
    }
}
