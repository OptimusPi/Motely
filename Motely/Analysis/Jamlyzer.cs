using System.ComponentModel;

namespace Motely.Analysis;

// JAMLyzer — the structured, every-stream single-seed snapshot.
//
// Same job as MotelyLegacyTextAnalyzerFilterDesc (one seed, complete walk), but
// it materializes EVERY stream MotelySingleSearchContext exposes — not just the
// board (boss/voucher/tags/shop/packs), but the hidden grants (Soul→legendary,
// tag→joker), the consumable-triggered "would give" streams, and the gameplay
// rolls — into an immutable snapshot. Completeness is the contract: a stream
// missing here is an item the UI can never surface.

public sealed record class JamlyzerSnapshot(
    string? Error,
    MotelyDeck Deck,
    IReadOnlyList<JamlyzerAnte> Antes,
    JamlyzerRolls Rolls
);

public sealed record class JamlyzerAnte(
    int Ante,
    MotelyBossBlind Boss,
    MotelyVoucher Voucher,
    MotelyTag SmallBlindTag,
    MotelyTag BigBlindTag,
    IReadOnlyList<MotelyAnalyzedItem> ShopQueue,
    IReadOnlyList<JamlyzerPack> Packs,
    // Hidden board state: Rare/Uncommon tag → granted joker.
    MotelyAnalyzedItem? SmallBlindTagGrantedJoker,
    MotelyAnalyzedItem? BigBlindTagGrantedJoker,
    // Consumable-triggered streams — what each WOULD give if used this ante.
    IReadOnlyList<MotelyAnalyzedItem> ConsumableJokers,    // Judgement, Wraith, Riff-Raff
    IReadOnlyList<MotelyAnalyzedItem> ConsumableTarots,    // Emperor, Purple Seal
    IReadOnlyList<MotelyAnalyzedItem> ConsumableSpectrals  // Sixth Sense, Seance
);

public sealed record class JamlyzerPack(
    MotelyBoosterPack Type,
    IReadOnlyList<MotelyAnalyzedItem> Items,
    // The Soul inside an Arcana/Spectral pack → the legendary joker it grants.
    MotelyAnalyzedItem? GrantedLegendaryJoker
);

// Per-seed gameplay rolls (bool/int outcomes; one PRNG stream each, not board items).
public sealed record class JamlyzerRolls(
    int MisprintMult,
    bool LuckyMoney,
    bool LuckyMult,
    MotelyItemEdition WheelOfFortune,
    bool CavendishExtinct,
    bool GrosMichelExtinct,
    bool SpaceLevelup,
    bool BusinessPayout,
    bool BloodstoneTrigger,
    bool ParkingPayout,
    bool EightBallTarot,
    bool GlassDestroy,
    bool OmenGlobeSpectral,
    bool WheelStaysFlipped
);

/// <summary>
/// Entry point: run the every-stream JAMLyzer walk over one seed and return the snapshot.
/// </summary>
public static class Jamlyzer
{
    public static JamlyzerSnapshot Analyze(string seed, MotelyDeck deck, MotelyStake stake)
    {
        try
        {
            JamlyzerFilterDesc filterDesc = new();
            var settings = new MotelySearchSettings<JamlyzerFilterDesc.JamlyzerFilter>(filterDesc)
                .WithDeck(deck)
                .WithStake(stake)
                .WithListSearch([seed])
                .WithThreadCount(1);

            using var search = settings.CreateSearch();
            search.RunSearchUntilCompletion();

            return filterDesc.LastSnapshot
                ?? new JamlyzerSnapshot("JAMLyzer produced no snapshot.", deck, [], EmptyRolls);
        }
        catch (Exception ex)
        {
            return new JamlyzerSnapshot(ex.ToString(), deck, [], EmptyRolls);
        }
    }

    private static readonly JamlyzerRolls EmptyRolls = new(
        0, false, false, MotelyItemEdition.None,
        false, false, false, false, false, false, false, false, false, false
    );
}

/// <summary>
/// Filter descriptor that drives the JAMLyzer every-stream walk over one seed and
/// parks the result in <see cref="LastSnapshot"/>.
/// </summary>
public sealed class JamlyzerFilterDesc() : IMotelySeedFilterDesc<JamlyzerFilterDesc.JamlyzerFilter>
{
    public JamlyzerSnapshot? LastSnapshot { get; private set; }

    public JamlyzerFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(this);

    public readonly struct JamlyzerFilter(JamlyzerFilterDesc filterDesc) : IMotelySeedFilter
    {
        public JamlyzerFilterDesc FilterDesc { get; } = filterDesc;

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
            public MotelySingleJokerFixedRarityStream LegendaryStream;
            public bool HasLegendaryStream;
        }

        public readonly bool CheckSeed(ref MotelySingleSearchContext ctx)
        {
            MotelyRunState voucherState = new();
            MotelySingleBossStream bossStream = ctx.CreateBossStream();

            List<JamlyzerAnte> antes = [];

            for (int ante = 1; ante <= 8; ante++)
            {
                AnteAnalysisState state = new()
                {
                    ArcanaStream = default,
                    CelestialStream = default,
                    SpectralStream = default,
                    StandardStream = MotelySingleStandardCardStream.Invalid,
                    BuffoonStream = default,
                    LegendaryStream = default,
                    HasLegendaryStream = false,
                };

                // ── Board sources ──
                MotelyBossBlind boss = ctx.GetBossForAnte(ref bossStream, ante, ref voucherState);

                MotelyVoucher voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
                voucherState.ActivateVoucher(voucher);

                MotelySingleTagStream tagStream = ctx.CreateTagStream(ante);
                MotelyTag smallTag = ctx.GetNextTag(ref tagStream);
                MotelyTag bigTag = ctx.GetNextTag(ref tagStream);

                MotelySingleShopItemStream shopStream = ctx.CreateShopItemStream(ante);
                int maxSlots = ante == 1 ? 15 : 50;
                MotelyAnalyzedItem[] shopItems = new MotelyAnalyzedItem[maxSlots];
                for (int i = 0; i < maxSlots; i++)
                    shopItems[i] = new(ctx.GetNextShopItem(ref shopStream));

                // ── Packs (+ The Soul → legendary) ──
                var packStream = ctx.CreateBoosterPackStream(ante);
                int maxPacks = ante == 1 ? 4 : 6;
                JamlyzerPack[] packs = new JamlyzerPack[maxPacks];
                for (int i = 0; i < maxPacks; i++)
                {
                    MotelyBoosterPack pack = ctx.GetNextBoosterPack(ref packStream);
                    MotelySingleItemSet content = GetPackContents(ref ctx, ante, pack, ref state);

                    MotelyAnalyzedItem[] items = content
                        .AsArray()
                        .Select(static item => new MotelyAnalyzedItem(item))
                        .ToArray();

                    MotelyAnalyzedItem? legendary = null;
                    if (ContainsTheSoul(content))
                    {
                        if (!state.HasLegendaryStream)
                        {
                            state.LegendaryStream = ctx.CreateLegendaryJokerStream(ante);
                            state.HasLegendaryStream = true;
                        }
                        legendary = new(ctx.GetNextJoker(ref state.LegendaryStream));
                    }

                    packs[i] = new(pack, items, legendary);
                }

                // ── Hidden grants: Rare/Uncommon tag → joker ──
                MotelyAnalyzedItem? smallTagJoker = TagGrantedJoker(ref ctx, ante, smallTag);
                MotelyAnalyzedItem? bigTagJoker = TagGrantedJoker(ref ctx, ante, bigTag);

                // ── Consumable-triggered "would give" streams ──
                var judgement = ctx.CreateJudgementJokerStream(ante);
                var wraith = ctx.CreateWraithJokerStream(ante);
                var riffRaff = ctx.CreateRiffRaffJokerStream(ante);
                MotelyAnalyzedItem[] consumableJokers =
                [
                    new(ctx.GetNextJoker(ref judgement)),
                    new(ctx.GetNextJoker(ref wraith)),
                    new(ctx.GetNextJoker(ref riffRaff)),
                ];

                var emperor = ctx.CreateEmperorTarotStream(ante);
                var purpleSeal = ctx.CreatePurpleSealTarotStream(ante);
                MotelyAnalyzedItem[] consumableTarots =
                [
                    new(ctx.GetNextTarot(ref emperor)),
                    new(ctx.GetNextTarot(ref purpleSeal)),
                ];

                var sixthSense = ctx.CreateSixthSenseSpectralStream(ante);
                var seance = ctx.CreateSeanceSpectralStream(ante);
                MotelyAnalyzedItem[] consumableSpectrals =
                [
                    new(ctx.GetNextSpectral(ref sixthSense)),
                    new(ctx.GetNextSpectral(ref seance)),
                ];

                antes.Add(
                    new(
                        ante,
                        boss,
                        voucher,
                        smallTag,
                        bigTag,
                        shopItems,
                        packs,
                        smallTagJoker,
                        bigTagJoker,
                        consumableJokers,
                        consumableTarots,
                        consumableSpectrals
                    )
                );
            }

            // ── Per-seed gameplay rolls (one PRNG stream each) ──
            var misprint = ctx.CreateMisprintPrngStream();
            var luckyMoney = ctx.CreateLuckyCardMoneyStream();
            var luckyMult = ctx.CreateLuckyCardMultStream();
            var wheel = ctx.CreateWheelOfFortuneStream();
            var cavendish = ctx.CreateCavendishPrngStream();
            var grosMichel = ctx.CreateGrosMichelPrngStream();
            var space = ctx.CreateSpacePrngStream();
            var business = ctx.CreateBusinessPrngStream();
            var bloodstone = ctx.CreateBloodstonePrngStream();
            var parking = ctx.CreateParkingPrngStream();
            var eightBall = ctx.CreateEightBallPrngStream();
            var glass = ctx.CreateGlassPrngStream();
            var omenGlobe = ctx.CreateOmenGlobePrngStream();
            var theWheel = ctx.CreateTheWheelPrngStream();

            JamlyzerRolls rolls = new(
                ctx.GetNextMisprintMult(ref misprint),
                ctx.GetNextLuckyMoney(ref luckyMoney),
                ctx.GetNextLuckyMult(ref luckyMult),
                ctx.GetNextWheelOfFortune(ref wheel),
                ctx.GetNextCavendishExtinct(ref cavendish),
                ctx.GetNextGrosMichelExtinct(ref grosMichel),
                ctx.GetNextSpaceLevelup(ref space),
                ctx.GetNextBusinessPayout(ref business),
                ctx.GetNextBloodstoneTrigger(ref bloodstone),
                ctx.GetNextParkingPayout(ref parking),
                ctx.GetNextEightBallTarot(ref eightBall),
                ctx.GetNextGlassDestroy(ref glass),
                ctx.GetNextOmenGlobeSpectral(ref omenGlobe),
                ctx.GetNextWheelStaysFlipped(ref theWheel)
            );

            FilterDesc.LastSnapshot = new(null, ctx.Deck, antes, rolls);
            return false; // analyze-only
        }

        private static MotelyAnalyzedItem? TagGrantedJoker(
            ref MotelySingleSearchContext ctx,
            int ante,
            MotelyTag tag
        )
        {
            if (tag == MotelyTag.RareTag)
            {
                var s = ctx.CreateRareTagJokerStream(ante);
                return new(ctx.GetNextJoker(ref s));
            }
            if (tag == MotelyTag.UncommonTag)
            {
                var s = ctx.CreateUncommonTagJokerStream(ante);
                return new(ctx.GetNextJoker(ref s));
            }
            return null;
        }

        private static bool ContainsTheSoul(MotelySingleItemSet set)
        {
            foreach (var item in set.AsArray())
                if (item.Type == MotelyItemType.TheSoul)
                    return true;
            return false;
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
}
