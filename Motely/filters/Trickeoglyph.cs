
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
        ctx.CacheAnteFirstVoucher(5);
        ctx.CacheAnteFirstVoucher(6);
        ctx.CacheAnteFirstVoucher(7);
        ctx.CacheAnteFirstVoucher(8);

        ctx.CacheBoosterPackStream(1);
        ctx.CacheBoosterPackStream(8);
        return new TrickeoglyphFilter();
    }

    public struct TrickeoglyphFilter() : IMotelySeedFilter
    {
        public static bool CheckSoulJokerInAnte(int ante, MotelyItemType targetJoker, ref MotelySingleSearchContext searchContext)
        {
            var boosterPackStream = searchContext.CreateBoosterPackStream(ante, false, false);
            var soulStream = searchContext.CreateSoulJokerStream(ante);
            bool soulStreamInit = false;

            // Check pack slots 0-5 for ante 8 (Canio), 0-3 for ante 1 (Perkeo)
            int maxPackSlots = ante == 8 ? 6 : 4;
            
            for (int i = 0; i < maxPackSlots; i++)
            {
                var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    var tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                    {
                        if (!soulStreamInit) 
                        {
                            soulStreamInit = true;
                        }
                        var soulJoker = searchContext.GetNextJoker(ref soulStream);
                        if (soulJoker.Type == targetJoker)
                        {
                            // For Perkeo, also check for Negative edition
                            if (targetJoker == MotelyItemType.Perkeo)
                            {
                                return soulJoker.Edition == MotelyItemEdition.Negative;
                            }
                            return true;
                        }
                    }
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    var spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                    if (searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                    {
                        if (!soulStreamInit) 
                        {
                            soulStreamInit = true;
                        }
                        var soulJoker = searchContext.GetNextJoker(ref soulStream);
                        if (soulJoker.Type == targetJoker)
                        {
                            // For Perkeo, also check for Negative edition
                            if (targetJoker == MotelyItemType.Perkeo)
                            {
                                return soulJoker.Edition == MotelyItemEdition.Negative;
                            }
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Iterate all 8 antes of voucher and then add up the passing lanes
            var state = new MotelyVectorRunStateVoucher();
            VectorMask matchingHiero = VectorMask.NoBitsSet;
            VectorMask matchingPetro = VectorMask.NoBitsSet;
            VectorMask matchingMagic = VectorMask.NoBitsSet;

            for (int ante = 1; ante <= 8; ante++)
            {
                // Get vector of all the seeds' couchers for the given ante
                VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(ante, state);
                // Activate vouchers found in this ante
                VectorMask magicMask = VectorEnum256.Equals(vouchers, MotelyVoucher.MagicTrick);
                VectorMask hieroglyphMask = VectorEnum256.Equals(vouchers, MotelyVoucher.Hieroglyph);
                VectorMask petroglyphMask = VectorEnum256.Equals(vouchers, MotelyVoucher.Petroglyph);
                
                if (magicMask.IsPartiallyTrue())
                    state.ActivateVoucher(MotelyVoucher.MagicTrick);
                if (hieroglyphMask.IsPartiallyTrue())
                    state.ActivateVoucher(MotelyVoucher.Hieroglyph);
                if (petroglyphMask.IsPartiallyTrue())
                    state.ActivateVoucher(MotelyVoucher.Petroglyph);

                // Check for matches against the target vouchers
                matchingHiero |= hieroglyphMask;
                matchingPetro |= petroglyphMask;
                matchingMagic |= magicMask;
            }

            // All three vouchers must be found (AND logic)
            var finalMask = matchingHiero & matchingPetro & matchingMagic;

            if (finalMask.IsAllFalse())
                return VectorMask.NoBitsSet; // Missing required vouchers

            // STEP 2: Individual processing for SHOULD clauses (soul jokers)
            return searchContext.SearchIndividualSeeds(finalMask, (ref MotelySingleSearchContext searchContext) =>
            {
                // Passed MUST requirements, now check SHOULD for bonus scoring
                
                // SHOULD: Soul Perkeo Negative in ante 1
                bool hasPerkeoNegative = CheckSoulJokerInAnte(1, MotelyItemType.Perkeo, ref searchContext);

                // SHOULD: Soul Canio in ante 8  
                bool hasCanio = CheckSoulJokerInAnte(8, MotelyItemType.Canio, ref searchContext);

                // Return true for any seed that passes MUST (regardless of SHOULD results)
                return true;
            });
        }
    }
}
