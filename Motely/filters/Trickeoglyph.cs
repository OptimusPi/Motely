
using System.Runtime.Intrinsics;

namespace Motely;

public struct TrickeoglyphFilterDesc() : IMotelySeedFilterDesc<TrickeoglyphFilterDesc.TrickeoglyphFilter>
{

    public TrickeoglyphFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheAnteFirstVoucher(1);
        ctx.CacheAnteFirstVoucher(2);
        ctx.CacheAnteFirstVoucher(3);
        ctx.CacheAnteFirstVoucher(4);
        return new TrickeoglyphFilter();
    }

    public struct TrickeoglyphFilter() : IMotelySeedFilter
    {
        public static bool CheckSoulJokerInAnte(int ante, MotelyItemType targetJoker, ref MotelySingleSearchContext searchContext, MotelyItemEdition? requiredEdition = null)
        {
            var soulStream = searchContext.CreateSoulJokerStream(ante);
            var soulJoker = searchContext.GetNextJoker(ref soulStream);
            if (soulJoker.Type != targetJoker)
                return false;
                
            // check required edition too
            if (requiredEdition.HasValue)
            {
                if (soulJoker.Edition != requiredEdition.Value)
                {
                    return false;
                }
            }
            

            var boosterPackStream = searchContext.CreateBoosterPackStream(ante, ante > 1, false);

            // Check pack slots 0-5 for ante 8 (Canio), 0-3 for ante 1 (Perkeo)
            int maxPackSlots = ante == 1 ? 4 : 6;

            for (int i = 0; i < maxPackSlots; i++)
            {
                var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    var tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                    {
                        return true;
                    }
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    var spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                    if (searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Iterate all 8 antes of voucher and then add up the passing lanes
            var state = new MotelyVectorRunState();
            VectorMask matchingHiero = VectorMask.NoBitsSet;
            VectorMask matchingMagic = VectorMask.NoBitsSet;

            for (int ante = 2; ante <= 4; ante++)
            {
                // Get vector of all the seeds' voucher for the given ante
                VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(ante, state);
                // Activate vouchers found in this ante
                matchingMagic |= VectorEnum256.Equals(vouchers, MotelyVoucher.MagicTrick);
                matchingHiero |= VectorEnum256.Equals(vouchers, MotelyVoucher.Hieroglyph);

                state.ActivateVoucher(vouchers);
            }

            // All three vouchers must be found by the SAME seed (AND logic)
            var finalMask = VectorMask.AllBitsSet;

            


            // STEP 2: Individual processing for SHOULD clauses (soul jokers)
            return searchContext.SearchIndividualSeeds(finalMask, (ref MotelySingleSearchContext searchContext) =>
            {
                // Passed MUST requirements, now check SHOULD for bonus scoring

                // SHOULD: Soul Perkeo Negative in ante 1
                bool hasPerkeoNegative = CheckSoulJokerInAnte(1, MotelyItemType.Perkeo, ref searchContext);

                // SHOULD: Soul Canio in ante 8
                bool hasCanio = CheckSoulJokerInAnte(8, MotelyItemType.Canio, ref searchContext);


                return hasCanio && hasPerkeoNegative;
            });
        }
    }
}
