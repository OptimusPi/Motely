using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// Standalone seed context for single-seed analysis.
/// No search infrastructure, no SIMD, no lanes — just seed + deck + stake + PRNG.
/// </summary>
public ref partial struct MotelySeedContext
{
    public readonly string Seed;
    public readonly MotelyDeck Deck;
    public readonly MotelyStake Stake;
    private readonly double _seedHash;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelySeedContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        Seed = seed;
        Deck = deck;
        Stake = stake;
        _seedHash = PseudoHashSeed(seed, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double PseudoHash(string key, bool isCached = false)
    {
        return PseudoHashKey(key, PseudoHashSeed(Seed, key.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double PseudoHashSeed(string seed, int keyLength)
    {
        double num = 1;
        for (int i = seed.Length - 1; i >= 0; i--)
        {
            num = (1.1239285023 / num * seed[i] * Math.PI + Math.PI * (keyLength + i + 1)) % 1;
        }
        return num;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double PseudoHashKey(string key, double partialHash)
    {
        double num = partialHash;
        for (int i = key.Length - 1; i >= 0; i--)
        {
            num = (1.1239285023 / num * key[i] * Math.PI + (i + 1) * Math.PI) % 1;
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
        return new(PseudoHash(key));
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
        return (GetNextPrngState(ref stream) + _seedHash) / 2d;
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
