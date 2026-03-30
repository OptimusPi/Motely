using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct MotelySinglePrngStream(double state)
{
    public static MotelySinglePrngStream Invalid => new(-1);

    public double State = state;
    public readonly bool IsInvalid => State < 0;
}

public struct MotelySingleResampleStream(MotelySinglePrngStream initialPrngStream, bool isCached)
{
    public static MotelySingleResampleStream Invalid => new(MotelySinglePrngStream.Invalid, false);

    public const int StackResampleCount = 4;

    [InlineArray(StackResampleCount)]
    public struct MotelyResampleStreams
    {
        public MotelySinglePrngStream PrngStream;
    }

    public MotelySinglePrngStream InitialPrngStream = initialPrngStream;
    public MotelyResampleStreams ResamplePrngStreams;
    public int ResamplePrngStreamInitCount;
    public List<object>? HighResamplePrngStreams;
    public bool IsCached = isCached;

    public readonly bool IsInvalid => InitialPrngStream.IsInvalid;
}

public interface IMotelySingleSearchContext
{
    MotelyStake Stake { get; }
    MotelyDeck Deck { get; }

    MotelySingleTarotStream CreateArcanaPackTarotStream(int ante, bool soulOnly = false, bool isCached = false);
    MotelySingleBoosterPackStream CreateBoosterPackStream(int ante, bool isCached = false);
    MotelySingleBoosterPackStream CreateBoosterPackStream(int ante, bool generatedFirstPack, bool isCached = false);
    MotelySingleBossStream CreateBossStream();
    MotelySingleJokerStream CreateBuffoonPackJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false);
    MotelySinglePrngStream CreateCavendishPrngStream(bool isCached = false);
    MotelySinglePlanetStream CreateCelestialPackPlanetStream(int ante, bool isCached = false);
    MotelySingleJokerFixedRarityStream CreateCommonShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags = MotelyJokerFixedRarityStreamFlags.Default, bool isCached = false);
    MotelySingleTarotStream CreateEmperorTarotStream(int ante, bool isCached = false);
    MotelySinglePrngStream CreateErraticDeckPrngStream(bool isCached = false);
    MotelySinglePrngStream CreateGrosMichelPrngStream(bool isCached = false);
    MotelySingleJokerStream CreateJudgementJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false);
    MotelySinglePrngStream CreateLuckyCardMoneyStream(bool isCached = false);
    MotelySinglePrngStream CreateLuckyCardMultStream(bool isCached = false);
    MotelySinglePrngStream CreateMisprintPrngStream(bool isCached = false);
    MotelySinglePrngStream CreatePrngStream(string key, bool isCached = false);
    MotelySingleTarotStream CreatePurpleSealTarotStream(int ante, bool isCached = false);
    MotelySingleJokerFixedRarityStream CreateRareShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags = MotelyJokerFixedRarityStreamFlags.Default, bool isCached = false);
    MotelySingleJokerFixedRarityStream CreateRareTagJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags = MotelyJokerFixedRarityStreamFlags.Default, bool isCached = false);
    MotelySingleJokerFixedRarityStream CreateRiffRaffJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags = MotelyJokerFixedRarityStreamFlags.Default, bool isCached = false);
    MotelySingleSpectralStream CreateSeanceSpectralStream(int ante, bool isCached = false);
    MotelySingleShopItemStream CreateShopItemStream(int ante, MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default, MotelyJokerStreamFlags jokerFlags = MotelyJokerStreamFlags.Default, bool isCached = false);
    MotelySingleShopItemStream CreateShopItemStream(int ante, MotelyRunState runState, MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default, MotelyJokerStreamFlags jokerFlags = MotelyJokerStreamFlags.Default, bool isCached = false);
    MotelySingleJokerStream CreateShopJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false);
    MotelySinglePlanetStream CreateShopPlanetStream(int ante, bool isCached = false);
    MotelySingleSpectralStream CreateShopSpectralStream(int ante, bool isCached = false);
    MotelySingleTarotStream CreateShopTarotStream(int ante, bool isCached = false);
    MotelySingleSpectralStream CreateSixthSenseSpectralStream(int ante, bool isCached = false);
    MotelySingleJokerFixedRarityStream CreateSoulJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags = MotelyJokerFixedRarityStreamFlags.Default, bool isCached = false);
    MotelySingleSpectralStream CreateSpectralPackSpectralStream(int ante, bool soulOnly = false, bool isCached = false);
    MotelySingleStandardCardStream CreateStandardPackCardStream(int ante, MotelyStandardCardStreamFlags flags = MotelyStandardCardStreamFlags.Default, bool isCached = false);
    MotelySingleTagStream CreateTagStream(int ante, bool isCached = false);
    MotelySingleJokerFixedRarityStream CreateUncommonShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags = MotelyJokerFixedRarityStreamFlags.Default, bool isCached = false);
    MotelySingleJokerFixedRarityStream CreateUncommonTagJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags = MotelyJokerFixedRarityStreamFlags.Default, bool isCached = false);
    MotelySingleVoucherStream CreateVoucherStream(int ante, bool isCached = false);
    MotelySinglePrngStream CreateWheelOfFortuneStream(bool isCached = false);
    MotelySingleJokerStream CreateWraithJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false);
    MotelyVoucher GetAnteFirstVoucher(int ante, bool isCached = false);
    MotelyVoucher GetAnteFirstVoucher(int ante, in MotelyRunState voucherState, bool isCached = false);
    MotelyBossBlind GetBossForAnte(ref MotelySingleBossStream stream, int ante, ref MotelyRunState state);
    MotelySingleItemSet GetNextArcanaPackContents(ref MotelySingleTarotStream tarotStream, MotelyBoosterPackSize size);
    bool GetNextArcanaPackHasTheSoul(ref MotelySingleTarotStream tarotStream, MotelyBoosterPackSize size);
    MotelyBoosterPack GetNextBoosterPack(ref MotelySingleBoosterPackStream stream);
    MotelySingleItemSet GetNextBuffoonPackContents(ref MotelySingleJokerStream jokerStream, MotelyBoosterPackSize size);
    MotelySingleItemSet GetNextBuffoonPackContents(ref MotelySingleJokerStream jokerStream, int size);
    bool GetNextCavendishExtinct(ref MotelySinglePrngStream cavendishStream, double baseLuck = 1);
    MotelySingleItemSet GetNextCelestialPackContents(ref MotelySinglePlanetStream planetStream, MotelyBoosterPackSize size);
    (MotelyItem, MotelyItem) GetNextEmperorTarots(ref MotelySingleTarotStream tarotStream);
    MotelyItem GetNextErraticDeckCard(ref MotelySinglePrngStream erraticDeckStream);
    bool GetNextGrosMichelExtinct(ref MotelySinglePrngStream grosMichelStream, double baseLuck = 1);
    MotelyItem GetNextJoker(ref MotelySingleJokerFixedRarityStream stream);
    MotelyItem GetNextJoker(ref MotelySingleJokerStream stream, in MotelySingleItemSet itemSet);
    MotelyItem GetNextJoker(ref MotelySingleJokerStream stream);
    LuaRandom GetNextLuaRandom(ref MotelySinglePrngStream stream);
    bool GetNextLuckyMoney(ref MotelySinglePrngStream moneyStream, double baseLuck = 1);
    bool GetNextLuckyMult(ref MotelySinglePrngStream multStream, double baseLuck = 1);
    int GetNextMisprintMult(ref MotelySinglePrngStream misprintStream);
    MotelyItem GetNextPlanet(ref MotelySinglePlanetStream planetStream);
    MotelyItem GetNextPlanet(ref MotelySinglePlanetStream planetStream, in MotelySingleItemSet itemSet);
    double GetNextPrngState(ref MotelySinglePrngStream stream);
    double GetNextPseudoSeed(ref MotelySinglePrngStream stream);
    double GetNextRandom(ref MotelySinglePrngStream stream);
    T GetNextRandomElement<T>(ref MotelySinglePrngStream stream, T[] choices);
    int GetNextRandomInt(ref MotelySinglePrngStream stream, int min, int max);
    MotelyItem GetNextShopItem(ref MotelySingleShopItemStream stream);
    MotelyItem GetNextSpectral(ref MotelySingleSpectralStream spectralStream);
    MotelyItem GetNextSpectral(ref MotelySingleSpectralStream spectralStream, in MotelySingleItemSet itemSet);
    MotelySingleItemSet GetNextSpectralPackContents(ref MotelySingleSpectralStream spectralStream, MotelyBoosterPackSize size);
    MotelySingleItemSet GetNextSpectralPackContents(ref MotelySingleSpectralStream spectralStream, int size);
    bool GetNextSpectralPackHasTheSoul(ref MotelySingleSpectralStream spectralStream, MotelyBoosterPackSize size);
    MotelyItem GetNextStandardCard(ref MotelySingleStandardCardStream stream);
    MotelySingleItemSet GetNextStandardPackContents(ref MotelySingleStandardCardStream stream, MotelyBoosterPackSize size);
    MotelyTag GetNextTag(ref MotelySingleTagStream tagStream);
    MotelyItem GetNextTarot(ref MotelySingleTarotStream tarotStream);
    MotelyItem GetNextTarot(ref MotelySingleTarotStream tarotStream, in MotelySingleItemSet itemSet);
    MotelyVoucher GetNextVoucher(ref MotelySingleVoucherStream voucherStream, in MotelyRunState voucherState);
    MotelyItemEdition GetNextWheelOfFortune(ref MotelySinglePrngStream wheelStream, double baseLuck = 1);
    string GetSeed();
    unsafe int GetSeed(char* output);
    double PseudoHash(string key, bool isCached = false);
    void Shuffle(string seed, Span<MotelyItem> deck);
}

public readonly unsafe ref partial struct MotelySingleSearchContext : IMotelySingleSearchContext
{
    public readonly int VectorLane;

    private readonly ref readonly MotelySearchParameters _searchParameters;
    private readonly ref readonly MotelySearchContextParams _contextParams;

    public MotelyStake Stake => _searchParameters.Stake;
    public MotelyDeck Deck => _searchParameters.Deck;

    internal ref readonly MotelySearchParameters SearchParameters => ref _searchParameters;
    internal ref readonly MotelySearchContextParams SearchContextParams => ref _contextParams;

    private PartialSeedHashCache* SeedHashCache => _contextParams.SeedHashCache;
    private int SeedLength => _contextParams.SeedLength;
    private int SeedFirstCharactersLength => _contextParams.SeedFirstCharactersLength;
    private int SeedLastCharactersLength => _contextParams.SeedLastCharactersLength;
    private char* SeedFirstCharacters => _contextParams.SeedFirstCharacters;
    private Vector512<double>* SeedLastCharacters => _contextParams.SeedLastCharacters;
    private bool IsAdditionalFilter => _contextParams.IsAdditionalFilter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MotelySingleSearchContext(
        ref readonly MotelySearchParameters searchParameters,
        ref readonly MotelySearchContextParams contextParams,
        int lane
    )
    {
        _contextParams = ref contextParams;
        _searchParameters = ref searchParameters;
        VectorLane = lane;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetSeed() => _contextParams.GetSeed(VectorLane);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSeed(char* output) => _contextParams.GetSeed(VectorLane, output);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double PseudoHash(string key, bool isCached = false)
    {
        double partialHash;

        if ((isCached && !IsAdditionalFilter) || SeedHashCache->HasPartialHash(key.Length))
        {
            partialHash = SeedHashCache->GetPartialHash(key.Length, VectorLane);
        }
        else
        {
            partialHash = InternalPseudoHashSeed(key.Length);
        }

        return InternalPseudoHashKey(key, partialHash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InternalPseudoHashKey(string key, double partialHash)
    {
        double num = partialHash;

        for (int i = key.Length - 1; i >= 0; i--)
        {
            num = (1.1239285023 / num * key[i] * Math.PI + (i + 1) * Math.PI) % 1;
        }

        return num;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InternalPseudoHashSeed(int keyLength)
    {
        int seedLastCharacterLength = SeedLastCharactersLength;
        double num = 1;

        // First we do the first characters of the seed which are the same between all vector lanes
        for (int i = SeedFirstCharactersLength - 1; i >= 0; i--)
        {
            num =
                (
                    1.1239285023 / num * SeedFirstCharacters[i] * Math.PI
                    + Math.PI * (i + keyLength + seedLastCharacterLength + 1)
                ) % 1;
        }

        // Then we get the characters for our lane
        for (int i = seedLastCharacterLength - 1; i >= 0; i--)
        {
            num =
                (
                    1.1239285023 / num * SeedLastCharacters[i][VectorLane] * Math.PI
                    + Math.PI * (keyLength + i + 1)
                ) % 1;
        }

        return num;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Fract(double x)
    {
        if (double.IsNaN(x))
            return x;

        ref ulong xInt = ref Unsafe.As<double, ulong>(ref x);

        const ulong DblExpo = 0x7FF0000000000000;
        const ulong DblMant = 0x000FFFFFFFFFFFFF;

        const int DblMantSZ = 52;

        const int DblExpoBias = 1023;

        ulong expo = (xInt & DblExpo) >> DblMantSZ;

        if (expo < DblExpoBias)
            return x;

        // We don't have to worry about this edge case

        // const int DblExpoSZ = 11;
        // if (expo == ((1 << DblExpoSZ) - 1)) return double.NaN;

        ulong expoBiased = expo - DblExpoBias;

        if (expoBiased > DblMantSZ)
            return 0;

        ulong mant = xInt & DblMant;
        ulong fractMant = mant & ((1ul << (int)(DblMantSZ - expoBiased)) - 1);

        if (fractMant == 0)
            return 0;

        int fractLzcnt = BitOperations.LeadingZeroCount(fractMant) - (64 - DblMantSZ);
        ulong resExpo = (expo - (ulong)fractLzcnt - 1) << DblMantSZ;
        ulong resMant = (fractMant << (fractLzcnt + 1)) & DblMant;

        ulong res = resExpo | resMant;

        return Unsafe.As<ulong, double>(ref res);
    }

    private static readonly double InvPrec = Math.Pow(10.0, 13);
    private static readonly double TwoInvPrec = Math.Pow(2.0, 13);
    private static readonly double FiveInvPrec = Math.Pow(5.0, 13);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Round13(double x)
    {
        double normalCase = Math.Round(x * InvPrec, MidpointRounding.AwayFromZero) / InvPrec;

        if (
            normalCase
            == Math.Round(Math.BitDecrement(x) * InvPrec, MidpointRounding.AwayFromZero) / InvPrec
        )
            return normalCase;

        double truncated = Fract(x * TwoInvPrec) * FiveInvPrec;

        if (Fract(truncated) >= 0.5)
            return (Math.Floor(x * InvPrec) + 1) / InvPrec;

        return Math.Floor(x * InvPrec) / InvPrec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double IteratePRNG(double state)
    {
        return Round13(Fract(state * 1.72431234 + 2.134453429141));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelySinglePrngStream CreatePrngStream(string key, bool isCached = false)
    {
        return new(PseudoHash(key, isCached));
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public double GetNextPrngState(ref MotelySinglePrngStream stream)
    {
        Debug.Assert(!stream.IsInvalid, "Invalid stream.");
        return stream.State = IteratePRNG(stream.State);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetNextPseudoSeed(ref MotelySinglePrngStream stream)
    {
        return (GetNextPrngState(ref stream) + SeedHashCache->GetSeedHash(VectorLane)) / 2d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuaRandom GetNextLuaRandom(ref MotelySinglePrngStream stream)
    {
        return new LuaRandom(GetNextPseudoSeed(ref stream));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetNextRandom(ref MotelySinglePrngStream stream)
    {
        return LuaRandom.Random(GetNextPseudoSeed(ref stream));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetNextRandomInt(ref MotelySinglePrngStream stream, int min, int max)
    {
        return LuaRandom.RandInt(GetNextPseudoSeed(ref stream), min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetNextRandomElement<T>(ref MotelySinglePrngStream stream, T[] choices)
    {
        return choices[GetNextRandomInt(ref stream, 0, choices.Length)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MotelySingleResampleStream CreateResampleStream(string key, bool isCached)
    {
        return new(CreatePrngStream(key, isCached), isCached);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MotelySinglePrngStream CreateResamplePrngStream(string key, int resample, bool isCached)
    {
        // We don't cache resamples >= 8 because they'd use an extra digit
        if (isCached && resample >= 8)
            isCached = false;
        return CreatePrngStream(key + MotelyPrngKeys.Resample + (resample + 2), isCached);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref MotelySinglePrngStream GetResamplePrngStream(
        ref MotelySingleResampleStream resampleStream,
        string key,
        int resample
    )
    {
        if (resample < MotelySingleResampleStream.StackResampleCount)
        {
            ref MotelySinglePrngStream prngStream = ref resampleStream.ResamplePrngStreams[
                resample
            ];

            if (resample == resampleStream.ResamplePrngStreamInitCount)
            {
                ++resampleStream.ResamplePrngStreamInitCount;
                prngStream = CreateResamplePrngStream(key, resample, resampleStream.IsCached);
            }

            return ref prngStream;
        }
        else
        {
            if (resample == MotelySingleResampleStream.StackResampleCount)
            {
                resampleStream.HighResamplePrngStreams = [];
            }

            Debug.Assert(resampleStream.HighResamplePrngStreams != null);

            if (resample < resampleStream.HighResamplePrngStreams.Count)
            {
                return ref Unsafe.Unbox<MotelySinglePrngStream>(
                    resampleStream.HighResamplePrngStreams[resample]
                );
            }

            object prngStreamObject = new MotelySinglePrngStream();

            resampleStream.HighResamplePrngStreams.Add(prngStreamObject);

            ref MotelySinglePrngStream prngStream = ref Unsafe.Unbox<MotelySinglePrngStream>(
                prngStreamObject
            );
            prngStream = CreateResamplePrngStream(key, resample, resampleStream.IsCached);

            return ref prngStream;
        }
    }
}
