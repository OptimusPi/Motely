
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
            MotelySingleTarotStream tarotStream = default;
            MotelySingleSpectralStream spectralStream = default;
            MotelySingleJokerFixedRarityStream soulStream = searchContext.CreateSoulJokerStream(ante);

            var soulJoker = searchContext.GetNextJoker(ref soulStream);
            if (soulJoker.Type == targetJoker)
            {
                if (requiredEdition.HasValue)
                {
                    if (soulJoker.Edition != requiredEdition.Value) return 0;
                }
            }
            else return 0;

            var boosterPackStream = searchContext.CreateBoosterPackStream(ante, false, ante != 1);
            bool spectralStreamInit = false;
            bool tarotStreamInit = false;

            // Check pack slots 0-5 for ante 8 (Canio), 0-3 for ante 1 (Perkeo)
            int maxPackSlots = ante == 1 ? 4 : 6;
            for (int i = 0; i < maxPackSlots; i++)
            {
                var pack = searchContext.GetNextBoosterPack(ref boosterPackStream);


                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                    }
                    if (searchContext.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                    {
                        return 1;
                    }
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {

                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    }
                    if (searchContext.GetNextArcanaPackHasTheSoul(ref tarotStream, pack.GetPackSize()))
                    {
                        return 1;
                    }
                }

            }
            // if there is no packs then I guess u can check for tags at this point lol
            var tagStream = searchContext.CreateTagStream(ante);
            var smallBlindTag = searchContext.GetNextTag(ref tagStream);
            if (smallBlindTag == MotelyTag.CharmTag) return 1;
            var bigBlindTag = searchContext.GetNextTag(ref tagStream);
            if (bigBlindTag == MotelyTag.CharmTag) return 1;

            return 0;
        }

        public static int CheckJokerInAnte(int ante, MotelyItemType targetJoker, ref MotelySingleSearchContext ctx, MotelyItemEdition? requiredEdition = null)
        {
            var boosterPackStream = ctx.CreateBoosterPackStream(ante, false, ante != 1);
            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante);

            int score = 0;
            MotelyShopStreamFlags ShopFlags = MotelyShopStreamFlags.ExcludeTarots
            | MotelyShopStreamFlags.ExcludePlanets;

            MotelyJokerStreamFlags JokerFlags = (targetJoker == MotelyItemType.Showman) ?
                MotelyJokerStreamFlags.ExcludeStickers
                | MotelyJokerStreamFlags.ExcludeCommonJokers :
                    MotelyJokerStreamFlags.ExcludeStickers
                    | MotelyJokerStreamFlags.ExcludeCommonJokers
                    | MotelyJokerStreamFlags.ExcludeUncommonJokers;
            

            // Shop Queue
            MotelySingleShopItemStream shopStream = ctx.CreateShopItemStream(ante,
                ShopFlags,
                JokerFlags);

            var slots = ante switch
            {
                1 => 4,
                2 => 12,
                3 => 25,
                _ => 35
            };

            for (int i = 0; i < slots; i++)
            {
                var shopItem = ctx.GetNextShopItem(ref shopStream);
                if (shopItem.Type == targetJoker)
                {
                    if (requiredEdition.HasValue)
                    {
                        score += shopItem.Edition == requiredEdition.Value ? 1 : 0;
                    }
                    else score++;
                }
            }

            int maxPackSlots = ante == 1 ? 4 : 6;
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
                        else
                            score++;
                    }
                }
            }
            return score;
        }

        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            MotelyVectorRunState voucherState = new();
            VectorEnum256<MotelyVoucher> vouchers = searchContext.GetAnteFirstVoucher(1);
            VectorMask matchingTele = VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
            VectorMask matchingObservatory = VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);
            voucherState.ActivateVoucher(vouchers);

            vouchers = searchContext.GetAnteFirstVoucher(2, voucherState);
            matchingTele |= VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
            matchingObservatory |= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);
            voucherState.ActivateVoucher(vouchers);

            vouchers = searchContext.GetAnteFirstVoucher(3, voucherState);
            matchingTele |= VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
            matchingObservatory |= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);
            voucherState.ActivateVoucher(vouchers);

            vouchers = searchContext.GetAnteFirstVoucher(4, voucherState);
            matchingTele |= VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
            matchingObservatory |= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);
            voucherState.ActivateVoucher(vouchers);

            vouchers = searchContext.GetAnteFirstVoucher(5, voucherState);
            matchingTele |= VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
            matchingObservatory |= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);
            voucherState.ActivateVoucher(vouchers);

            vouchers = searchContext.GetAnteFirstVoucher(6, voucherState);
            matchingTele |= VectorEnum256.Equals(vouchers, MotelyVoucher.Telescope);
            matchingObservatory |= VectorEnum256.Equals(vouchers, MotelyVoucher.Observatory);
            voucherState.ActivateVoucher(vouchers);
            
            if (matchingObservatory.IsAllFalse())
                return Vector512<double>.Zero;

            // STEP 2: Individual processing for SHOULD clauses (soul jokers)
            return searchContext.SearchIndividualSeeds(matchingObservatory, (ref MotelySingleSearchContext searchContext) =>
            {
                // Passed MUST requirements, now check SHOULD for bonus scoring

                // check 8 antes:
                int hasPerkeoNegative = 0;
                int hasPerkeo = 0;
                int Nbps = 0;
                int Nbss = 0;
                int Bps = 0;
                int Bss = 0;
                int Ninvis = 0;
                int invis = 0;
                int showman = 0;

                hasPerkeoNegative += CheckSoulJokerInAnte(1, MotelyItemType.Perkeo, ref searchContext);
                hasPerkeo += CheckSoulJokerInAnte(1, MotelyItemType.Perkeo, ref searchContext);
                Nbps += CheckJokerInAnte(1, MotelyItemType.Blueprint, ref searchContext, MotelyItemEdition.Negative);
                Nbss += CheckJokerInAnte(1, MotelyItemType.Brainstorm, ref searchContext, MotelyItemEdition.Negative);
                Bps += CheckJokerInAnte(1, MotelyItemType.Blueprint, ref searchContext);
                Bss += CheckJokerInAnte(1, MotelyItemType.Brainstorm, ref searchContext);
                Ninvis += CheckJokerInAnte(1, MotelyItemType.InvisibleJoker, ref searchContext, MotelyItemEdition.Negative);
                invis += CheckJokerInAnte(1, MotelyItemType.InvisibleJoker, ref searchContext);
                showman += CheckJokerInAnte(1, MotelyItemType.Showman, ref searchContext);



                for (int ante = 2; ante < 4; ante++)
                {
                    hasPerkeoNegative += CheckSoulJokerInAnte(ante, MotelyItemType.Perkeo, ref searchContext);
                    hasPerkeo += CheckSoulJokerInAnte(ante, MotelyItemType.Perkeo, ref searchContext);
                    Nbps += CheckJokerInAnte(ante, MotelyItemType.Blueprint, ref searchContext, MotelyItemEdition.Negative);
                    Nbss += CheckJokerInAnte(ante, MotelyItemType.Brainstorm, ref searchContext, MotelyItemEdition.Negative);
                    Bps += CheckJokerInAnte(ante, MotelyItemType.Blueprint, ref searchContext);
                    Bss += CheckJokerInAnte(ante, MotelyItemType.Brainstorm, ref searchContext);
                    Ninvis += CheckJokerInAnte(ante, MotelyItemType.InvisibleJoker, ref searchContext, MotelyItemEdition.Negative);
                    invis += CheckJokerInAnte(ante, MotelyItemType.InvisibleJoker, ref searchContext);
                    showman += CheckJokerInAnte(ante, MotelyItemType.Showman, ref searchContext);
                }

                if (showman == 0 || (invis + Bps + Bss < 1))
                    return false;


                for (int ante = 4; ante < 10; ante++)
                {
                    Nbps += CheckJokerInAnte(ante, MotelyItemType.Blueprint, ref searchContext, MotelyItemEdition.Negative);
                    Nbss += CheckJokerInAnte(ante, MotelyItemType.Brainstorm, ref searchContext, MotelyItemEdition.Negative);
                    Bps += CheckJokerInAnte(ante, MotelyItemType.Blueprint, ref searchContext);
                    Bss += CheckJokerInAnte(ante, MotelyItemType.Brainstorm, ref searchContext);
                    Ninvis += CheckJokerInAnte(ante, MotelyItemType.InvisibleJoker, ref searchContext, MotelyItemEdition.Negative);
                    invis += CheckJokerInAnte(ante, MotelyItemType.InvisibleJoker, ref searchContext);
                }

                var score = hasPerkeo + hasPerkeoNegative + Nbps + Nbss + Ninvis + invis + Bss + Bps;
                var negScore = hasPerkeoNegative + Nbps + Nbss + Ninvis;
                if (negScore > 2)
                {
                    Console.WriteLine($"pifreaklovesyou,{score},{hasPerkeo},{hasPerkeoNegative},{Nbps},{Nbss},{Ninvis},{invis},{Bss},{Bps},Seed=");
                    return true;
                }
                return false;
            });
        }
    }
}
