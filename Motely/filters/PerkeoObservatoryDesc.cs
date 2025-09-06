using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct PerkeoObservatoryFilterDesc() : IMotelySeedFilterDesc<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>
{

    public PerkeoObservatoryFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache vouchers for all antes we'll check
        for (int ante = 1; ante <= 5; ante++)
        {
            ctx.CacheAnteFirstVoucher(ante);
            ctx.CacheBoosterPackStream(ante);
        }
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

   
            bool boosterPackStreamInit = false;


            bool tarotStreamInit = false, spectralStreamInit = false;

            soulStream = searchContext.CreateSoulJokerStream(ante);
            var wouldBe = searchContext.GetNextJoker(ref soulStream);
            if (wouldBe.Type != MotelyItemType.Perkeo || wouldBe.Edition != MotelyItemEdition.Negative) return false;

            for (int i = 0; i < 4; i++)
            {
                if (!boosterPackStreamInit)
                {
                    boosterPackStream = searchContext.CreateBoosterPackStream(ante, true, false);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Check for Telescope and Observatory vouchers appearing in any of the first 3 antes
            VectorMask hasTelescope = VectorMask.NoBitsSet;
            VectorMask hasObservatory = VectorMask.NoBitsSet;
            MotelyVectorRunState voucherState = new();

            // Process antes in order to properly handle voucher activation chain
            for (int ante = 1; ante <= 3; ante++)
            {
                VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(ante, voucherState);
                
                // Check if Telescope appears at this ante
                VectorMask isTelescope = VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
                hasTelescope |= isTelescope;
                
                // CRITICAL: Activate Telescope BEFORE checking for Observatory
                voucherState.ActivateVoucherForMask(MotelyVoucher.Telescope, isTelescope);
                
                // Observatory can ONLY appear if Telescope is already active
                VectorMask isObservatory = VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);
                hasObservatory |= isObservatory;
            }
            
            VectorMask matching = hasTelescope & hasObservatory;
            
            if (matching.IsAllFalse())
                return VectorMask.NoBitsSet;

            return searchContext.SearchIndividualSeeds(matching, (ref MotelySingleSearchContext searchContext) =>
            {
                // We already know this seed has both vouchers from the vectorized check!
                // Just check for Perkeo in antes 1 through 3
                for (int ante = 1; ante <= 3; ante++)
                {
                    if (CheckAnteForPerkeo(ante, ref searchContext))
                        return true;
                }
                return false;
            });
        }
    }
}
