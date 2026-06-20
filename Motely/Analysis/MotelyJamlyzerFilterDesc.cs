using System.ComponentModel;

namespace Motely.Analysis;

public sealed class MotelyJamlyzerFilterDesc(int[] antesToAnalyze, int eventRolls = 20) : IMotelySeedFilterDesc<MotelyJamlyzerFilterDesc.JamlyzerFilter>
{
    public List<MotelyJamlyzerAnteResult> Antes { get; } = [];
    public MotelyJamlyzerEvents? Events { get; set; }

    public JamlyzerFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(this);

    public readonly struct JamlyzerFilter(MotelyJamlyzerFilterDesc filterDesc) : IMotelySeedFilter
    {
        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx) =>
            ctx.SearchIndividualSeeds(CheckSeed);

        private ref struct AnteAnalysisState
        {
            public MotelySingleTarotStream ArcanaStream;
            public readonly bool HasArcanaStream => !ArcanaStream.IsNull;
            public MotelySinglePlanetStream CelestialStream;
            public readonly bool HasCelestialStream => !CelestialStream.IsNull;
            public MotelySingleSpectralStream SpectralStream;
            public readonly bool HasSpectralStream => !SpectralStream.IsNull;
            public MotelySingleStandardCardStream StandardStream;
            public readonly bool HasStandardStream => !StandardStream.IsInvalid;
            public MotelySingleJokerStream BuffoonStream;
            public readonly bool HasBuffoonStream => !BuffoonStream.IsNull;
        }

        public readonly bool CheckSeed(ref MotelySingleSearchContext ctx)
        {
            int maxAnte = filterDesc._maxAnte;

            MotelyRunState voucherState = new();
            MotelySingleBossStream bossStream = ctx.CreateBossStream();

            for (int ante = 1; ante <= maxAnte; ante++)
            {
                MotelyBossBlind boss = ctx.GetBossForAnte(ref bossStream, ante, ref voucherState);
                MotelyVoucher voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
                voucherState.ActivateVoucher(voucher);

                AnteAnalysisState state = new()
                {
                    ArcanaStream = default,
                    CelestialStream = default,
                    SpectralStream = default,
                    StandardStream = MotelySingleStandardCardStream.Invalid,
                    BuffoonStream = default,
                };

                // Tags
                MotelySingleTagStream tagStream = ctx.CreateTagStream(ante);
                MotelyTag smallTag = ctx.GetNextTag(ref tagStream);
                MotelyTag bigTag = ctx.GetNextTag(ref tagStream);

                // Shop
                MotelySingleShopItemStream shopStream = ctx.CreateShopItemStream(ante);
                int maxSlots = ante <= 1 ? 15 : 50;
                MotelyItem[] shopItems = new MotelyItem[maxSlots];
                for (int i = 0; i < maxSlots; i++)
                    shopItems[i] = ctx.GetNextShopItem(ref shopStream);

                // Packs
                var packStream = ctx.CreateBoosterPackStream(ante);
                int maxPacks = ante <= 1 ? 4 : 6;
                MotelyJamlyzerPack[] packs = new MotelyJamlyzerPack[maxPacks];
                for (int i = 0; i < maxPacks; i++)
                {
                    MotelyBoosterPack pack = ctx.GetNextBoosterPack(ref packStream);
                    MotelyItem[] contents = GetPackContents(ref ctx, ante, pack, ref state).AsArray();
                    packs[i] = new(pack, contents);
                }

                filterDesc.Antes.Add(new(ante, boss, voucher, smallTag, bigTag, shopItems, packs));
            }

            filterDesc.Events = CollectEvents(ref ctx, filterDesc._eventRolls);

            return false;
        }

        private static MotelyJamlyzerEvents CollectEvents(ref MotelySingleSearchContext ctx, int N)
        {
            var luckyMoney   = ctx.CreateLuckyCardMoneyStream();
            var luckyMult    = ctx.CreateLuckyCardMultStream();
            var wheel        = ctx.CreateWheelOfFortuneStream();
            var cavendish    = ctx.CreateCavendishPrngStream();
            var grosMichel   = ctx.CreateGrosMichelPrngStream();
            var space        = ctx.CreateSpacePrngStream();
            var business     = ctx.CreateBusinessPrngStream();
            var bloodstone   = ctx.CreateBloodstonePrngStream();
            var parking      = ctx.CreateParkingPrngStream();
            var eightBall    = ctx.CreateEightBallPrngStream();
            var glass        = ctx.CreateGlassPrngStream();
            var omenGlobe    = ctx.CreateOmenGlobePrngStream();
            var theWheel     = ctx.CreateTheWheelPrngStream();
            var misprint     = ctx.CreateMisprintPrngStream();

            bool[]              luckyMoneyRolls  = new bool[N];
            bool[]              luckyMultRolls   = new bool[N];
            MotelyItemEdition[] wheelRolls       = new MotelyItemEdition[N];
            bool[]              cavendishRolls   = new bool[N];
            bool[]              grosMichelRolls  = new bool[N];
            bool[]              spaceRolls       = new bool[N];
            bool[]              businessRolls    = new bool[N];
            bool[]              bloodstoneRolls  = new bool[N];
            bool[]              parkingRolls     = new bool[N];
            bool[]              eightBallRolls   = new bool[N];
            bool[]              glassRolls       = new bool[N];
            bool[]              omenGlobeRolls   = new bool[N];
            bool[]              theWheelRolls    = new bool[N];
            int[]               misprintRolls    = new int[N];

            for (int i = 0; i < N; i++)
            {
                luckyMoneyRolls[i]  = ctx.GetNextLuckyMoney(ref luckyMoney);
                luckyMultRolls[i]   = ctx.GetNextLuckyMult(ref luckyMult);
                wheelRolls[i]       = ctx.GetNextWheelOfFortune(ref wheel);
                cavendishRolls[i]   = ctx.GetNextCavendishExtinct(ref cavendish);
                grosMichelRolls[i]  = ctx.GetNextGrosMichelExtinct(ref grosMichel);
                spaceRolls[i]       = ctx.GetNextSpaceLevelup(ref space);
                businessRolls[i]    = ctx.GetNextBusinessPayout(ref business);
                bloodstoneRolls[i]  = ctx.GetNextBloodstoneTrigger(ref bloodstone);
                parkingRolls[i]     = ctx.GetNextParkingPayout(ref parking);
                eightBallRolls[i]   = ctx.GetNextEightBallTarot(ref eightBall);
                glassRolls[i]       = ctx.GetNextGlassDestroy(ref glass);
                omenGlobeRolls[i]   = ctx.GetNextOmenGlobeSpectral(ref omenGlobe);
                theWheelRolls[i]    = ctx.GetNextWheelStaysFlipped(ref theWheel);
                misprintRolls[i]    = ctx.GetNextMisprintMult(ref misprint);
            }

            return new(
                luckyMoneyRolls, luckyMultRolls, wheelRolls,
                cavendishRolls, grosMichelRolls, spaceRolls,
                businessRolls, bloodstoneRolls, parkingRolls,
                eightBallRolls, glassRolls, omenGlobeRolls,
                theWheelRolls, misprintRolls
            );
        }

        private static MotelySingleItemSet GetPackContents(
            ref MotelySingleSearchContext ctx,
            int ante,
            MotelyBoosterPack pack,
            ref AnteAnalysisState state
        )
        {
            var packType = pack.GetPackType();
            var packSize = pack.GetPackSize();

            switch (packType)
            {
                case MotelyBoosterPackType.Arcana:
                    if (!state.HasArcanaStream)
                        state.ArcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                    return ctx.GetNextArcanaPackContents(ref state.ArcanaStream, packSize);

                case MotelyBoosterPackType.Celestial:
                    if (!state.HasCelestialStream)
                        state.CelestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                    return ctx.GetNextCelestialPackContents(ref state.CelestialStream, packSize);

                case MotelyBoosterPackType.Spectral:
                    if (!state.HasSpectralStream)
                        state.SpectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                    return ctx.GetNextSpectralPackContents(ref state.SpectralStream, packSize);

                case MotelyBoosterPackType.Buffoon:
                    if (!state.HasBuffoonStream)
                        state.BuffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                    return ctx.GetNextBuffoonPackContents(ref state.BuffoonStream, packSize);

                case MotelyBoosterPackType.Standard:
                    if (!state.HasStandardStream)
                        state.StandardStream = ctx.CreateStandardPackCardStream(ante);
                    return ctx.GetNextStandardPackContents(ref state.StandardStream, packSize);

                default:
                    throw new InvalidEnumArgumentException();
            }
        }
    }

    private readonly int[] _antesToAnalyze = antesToAnalyze;
    private readonly int _maxAnte = antesToAnalyze.Length > 0 ? antesToAnalyze[^1] : 8;
    private readonly int _eventRolls = eventRolls;
}
