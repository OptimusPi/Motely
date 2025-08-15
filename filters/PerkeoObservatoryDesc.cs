using System.Runtime.Intrinsics;

namespace Motely;

public struct PerkeoObservatoryFilterDesc() : IMotelySeedFilterDesc<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>
{

    public PerkeoObservatoryFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheAnteFirstVoucher(1);
        ctx.CacheAnteFirstVoucher(2);
        
        // Cache booster pack streams that are needed
        ctx.CacheBoosterPackStream(1);
        ctx.CacheBoosterPackStream(2);
        
        // Cache soul joker streams
        ctx.CacheSoulJokerStream(1);
        ctx.CacheSoulJokerStream(2);
        
        // Cache tarot and spectral streams for pack checking
        ctx.CacheArcanaPackTarotStream(1, true);
        ctx.CacheArcanaPackTarotStream(2, true);

        return new PerkeoObservatoryFilter();
    }

    public struct PerkeoObservatoryFilter() : IMotelySeedFilter
    {

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(1);

            VectorMask matching = VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);

            if (matching.IsAllFalse())
                return Vector512<double>.Zero;

            MotelyVectorRunStateVoucher voucherState = new();
            voucherState.ActivateVoucher(MotelyVoucher.Telescope);

            vouchers = searchContext.GetAnteFirstVoucher(2, voucherState);

            matching &= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);

            return searchContext.SearchIndividualSeeds(matching, (ref MotelySingleSearchContext searchContext) =>
            {
                MotelySingleTarotStream tarotStream = default;
                MotelySingleSpectralStream spectralStream = default;
                MotelySingleJokerFixedRarityStream soulStream = default;

                bool soulStreamInit = false;

                MotelySingleBoosterPackStream boosterPackStream = searchContext.CreateBoosterPackStream(1, true);

                MotelyBoosterPack pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    tarotStream = searchContext.CreateArcanaPackTarotStream(1, true);

                    if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                    {
                        if (!soulStreamInit) soulStream = searchContext.CreateSoulJokerStream(1);
                        var joker = searchContext.GetNextJoker(ref soulStream);
                        return joker.Type == MotelyItemType.Triboulet && joker.Edition == MotelyItemEdition.Negative;
                    }
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    spectralStream = searchContext.CreateSpectralPackSpectralStream(1, false);

                    if (searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                    {
                        if (!soulStreamInit) soulStream = searchContext.CreateSoulJokerStream(1);
                        var joker = searchContext.GetNextJoker(ref soulStream);
                        return joker.Type == MotelyItemType.Triboulet && joker.Edition == MotelyItemEdition.Negative;
                    }

                }

                boosterPackStream = searchContext.CreateBoosterPackStream(2);
                bool tarotStreamInit = false, spectralStreamInit = false;
                soulStreamInit = false;

                for (int i = 0; i < 3; i++)
                {
                    pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

                    if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                    {
                        if (!tarotStreamInit)
                        {
                            tarotStreamInit = true;
                            tarotStream = searchContext.CreateArcanaPackTarotStream(2, true);
                        }

                        if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                        {
                        if (!soulStreamInit) soulStream = searchContext.CreateSoulJokerStream(2);
                            return searchContext.GetNextJoker(ref soulStream).Type == MotelyItemType.Perkeo;
                        }
                    }

                    if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                    {
                        if (!spectralStreamInit)
                        {
                            spectralStreamInit = true;
                            spectralStream = searchContext.CreateSpectralPackSpectralStream(2, true);
                        }

                        if (searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                        {
                        if (!soulStreamInit) soulStream = searchContext.CreateSoulJokerStream(2);
                            return searchContext.GetNextJoker(ref soulStream).Type == MotelyItemType.Perkeo;
                        }
                    }
                }

                return false;

            });
        }
    }
}