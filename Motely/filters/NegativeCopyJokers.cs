
using System.Runtime.Intrinsics;

namespace Motely;

public struct NegativeCopyFilterDesc() : IMotelySeedFilterDesc<NegativeCopyFilterDesc.NegativeCopyFilter>
{

    public NegativeCopyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
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
        ctx.CacheBoosterPackStream(2);
        ctx.CacheBoosterPackStream(3);
        ctx.CacheBoosterPackStream(4);
        ctx.CacheBoosterPackStream(5);
        ctx.CacheBoosterPackStream(6);
        ctx.CacheBoosterPackStream(7);
        ctx.CacheBoosterPackStream(8);
        return new NegativeCopyFilter();
    }

    public struct NegativeCopyFilter() : IMotelySeedFilter
    {
        public static int CheckSoulJokerInAnte(int ante, MotelyItemType targetJoker, ref MotelySingleSearchContext searchContext, MotelyItemEdition? requiredEdition = null)
        {
            var boosterPackStream = searchContext.CreateBoosterPackStream(ante, false, false);
            var soulStream = searchContext.CreateSoulJokerStream(ante);
            bool soulStreamInit = false;

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
                        if (!soulStreamInit) 
                        {
                            soulStreamInit = true;
                        }
                        var soulJoker = searchContext.GetNextJoker(ref soulStream);
                        if (soulJoker.Type == targetJoker)
                        {
                            return soulJoker.Edition == MotelyItemEdition.Negative ? 1 : 0;
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
                            // Check edition if specified
                            if (requiredEdition.HasValue)
                            {
                                return soulJoker.Edition == requiredEdition.Value ? 1 : 0;
                            }
                            return 1;
                        }
                    }
                }
            }

            return 0;
        }
        
        public static int CheckJokerInAnte(int ante, MotelyItemType targetJoker, ref MotelySingleSearchContext ctx, MotelyItemEdition? requiredEdition = null)
        {
            var boosterPackStream = ctx.CreateBoosterPackStream(ante, false, false);
            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);

            int maxPackSlots = ante == 1 ? 4 : 6;
            int score = 0;
            for (int i = 0; i < maxPackSlots; i++)
            {
                var pack = ctx.GetNextBoosterPack(ref boosterPackStream);
                var contents = ctx.GetNextBuffoonPackContents(ref buffoonStream, pack.GetPackSize());
                for (int j = 0; j < contents.Length; j++)
                {
                    var item = contents[j];
                    if (item.Type == targetJoker)
                    {
                        if (requiredEdition.HasValue)
                        {
                            if (item.Edition == requiredEdition.Value)
                                score++;
                        }
                    }
                }
            }
            return score;
        }

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Iterate all 8 antes of voucher and then add up the passing lanes
            var state = new MotelyVectorRunStateVoucher();
            VectorMask matchingBlank = VectorMask.NoBitsSet;
            VectorMask matchingAntimatter = VectorMask.NoBitsSet;

            for (int ante = 1; ante <= 8; ante++)
            {
                // Get vector of all the seeds' voucher for the given ante
                VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(ante, state);
                // Activate vouchers found in this ante
                VectorMask blankMask = VectorEnum256.Equals(vouchers, MotelyVoucher.Blank);
                VectorMask antimatterMask = VectorEnum256.Equals(vouchers, MotelyVoucher.Antimatter);

                state.ActivateVoucher(vouchers);

                // Check for matches against the target vouchers
                matchingBlank |= blankMask;
                matchingAntimatter |= antimatterMask;
            }

            // All three vouchers must be found by the SAME seed (AND logic)
            var finalMask = VectorMask.AllBitsSet;
            finalMask &= matchingBlank;
            finalMask &= matchingAntimatter;
            if (finalMask.IsAllFalse())
                return VectorMask.NoBitsSet; // Missing required vouchers

            // STEP 2: Individual processing for SHOULD clauses (soul jokers)
            return searchContext.SearchIndividualSeeds(finalMask, (ref MotelySingleSearchContext searchContext) =>
            {
                // Passed MUST requirements, now check SHOULD for bonus scoring

                // check 8 antes:
                int hasPerkeoNegative = 0;
                int hasNegativeBlueprint = 0;
                int foundAnte = 0;
                for (int ante = 1; ante <= 8; ante++)
                {
                    hasPerkeoNegative += CheckSoulJokerInAnte(ante, MotelyItemType.Perkeo, ref searchContext, MotelyItemEdition.Negative);
                    hasNegativeBlueprint += CheckJokerInAnte(ante, MotelyItemType.Blueprint, ref searchContext, MotelyItemEdition.Negative);

                    foundAnte += (hasPerkeoNegative + hasNegativeBlueprint) > 0 ? 1 : 0;
                }
                if (hasNegativeBlueprint > 0 && hasPerkeoNegative > 0)
                {
                    Console.Write($"Found negative jokers in ante {foundAnte}: Perkeo({hasPerkeoNegative}), Blueprint({hasNegativeBlueprint}) ");
                    return true;
                }
                return false;
            });
        }
    }
}
