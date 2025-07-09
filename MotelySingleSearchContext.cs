
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct MotelySinglePrngStream(double state)
{
    public double State = state;
}

public ref struct MotelySingleResampleStream(MotelySinglePrngStream initialPrngStream, bool isCached)
{
    public const int StackResampleCount = 16;

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
}

public ref struct MotelySingleItemSet
{
    public const int MaxLength = 5;

    [InlineArray(MaxLength)]
    public struct MotelyItems
    {
        public MotelyItem Card;
    }

    public MotelyItems Items;
    public int Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref MotelyItem GetItemRef(ref MotelySingleItemSet set, int index)
    {
#if MOTELY_SAFE
        return ref set.Items[index];
#else
        // Be fast and skip the bounds check
        return ref Unsafe.Add<MotelyItem>(ref Unsafe.As<MotelyItems, MotelyItem>(ref set.Items), index);
#endif
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItem GetItem(int index)
    {
        return GetItemRef(ref this, index);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public void Append(MotelyItem item)
    {
        GetItemRef(ref this, Length++) = item;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public bool Contains(MotelyItemType item)
    {
        for (int i = 0; i < Length; i++)
        {
            if (GetItemRef(ref this, i).Type == item)
            {
                return true;
            }
        }

        return false;
    }
}

public unsafe ref partial struct MotelySingleSearchContext
{
    public readonly int VectorLane;

    private ref MotelySearchContextParams _params;

    private ref SeedHashCache SeedHashCache => ref _params.SeedHashCache;
    private readonly int SeedLength => _params.SeedLength;
    public readonly int SeedFirstCharactersLength => _params.SeedFirstCharactersLength;
    private readonly char* SeedFirstCharacters => _params.SeedFirstCharacters;
    private readonly Vector512<double>* SeedLastCharacters => _params.SeedLastCharacters;
    public string GetCurrentSeed()
    {
#if MOTELY_SAFE
        if (SeedFirstCharacters == null || SeedLastCharacters == null)
            throw new InvalidOperationException("Seed characters are not initialized.");
#endif

        
        // Sequential search stores:
        // - First character: in SeedLastCharacters[0][VectorLane]
        // - Characters 1-7: in SeedFirstCharacters[0..6]

        if (SeedFirstCharactersLength == SeedLength - 1)
        {
            // Sequential search layout
            Span<char> seedBuffer = stackalloc char[SeedLength];
            
            // First character comes from the vector
            seedBuffer[0] = (char)SeedLastCharacters[0][VectorLane];
            
            // Rest come from SeedFirstCharacters
            for (int i = 0; i < SeedFirstCharactersLength; i++)
            {
                seedBuffer[i + 1] = SeedFirstCharacters[i];
                if (SeedFirstCharacters[i] == '\0') 
                {
                    // Null terminator found, actual seed is shorter
                    return new string(seedBuffer[..(i + 1)]);
                }
            }
            
            return new string(seedBuffer);
        }
        else if (SeedFirstCharactersLength == 0)
        {
            // Provider search layout - all chars in vectors
            Span<char> seedBuffer = stackalloc char[SeedLength];
            int actualLength = SeedLength;
            
            for (int i = 0; i < SeedLength; i++)
            {
                char c = (char)SeedLastCharacters[i][VectorLane];
                if (c == '\0')
                {
                    actualLength = i;
                    break;
                }
                seedBuffer[i] = c;
            }
            
            return new string(seedBuffer[..actualLength]);
        }
        else
        {
            // Generic case - shouldn't happen in practice
            throw new NotImplementedException($"Unexpected layout: FirstCharactersLength={SeedFirstCharactersLength}, SeedLength={SeedLength}");
        }
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    internal MotelySingleSearchContext(ref MotelySearchContextParams @params, int lane)
    {
        _params = ref @params;
        VectorLane = lane;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public double PseudoHashCached(string key)
    {
#if MOTELY_SAFE
        if (!_seedHashCache.HasPartialHash(key.Length))
            throw new KeyNotFoundException("Cache does not contain key :c");
#endif

        double num = SeedHashCache.GetPartialHash(key.Length, VectorLane);

        for (int i = key.Length - 1; i >= 0; i--)
        {
            num = (1.1239285023 / num * key[i] * Math.PI + (i + 1) * Math.PI) % 1;
        }

        return num;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public double PseudoHash(string key)
    {
        if (SeedHashCache.HasPartialHash(key.Length))
        {
            return PseudoHashCached(key);
        }

        double num = 1;
        
        // Handle different layouts
        if (SeedFirstCharactersLength == SeedLength - 1)
        {
            // Sequential search layout: first char in vector, rest in array
            // Process in correct order: full seed from end to beginning
            
            // Process the seed backwards from the last character
            for (int i = SeedLength - 1; i >= 0; i--)
            {
                char ch;
                if (i == 0)
                {
                    // First character is in the vector
                    ch = (char)SeedLastCharacters[0][VectorLane];
                }
                else
                {
                    // Rest are in SeedFirstCharacters (offset by 1)
                    ch = SeedFirstCharacters[i - 1];
                }
                num = (1.1239285023 / num * ch * Math.PI + Math.PI * (i + key.Length + 1)) % 1;
            }
        }
        else if (SeedFirstCharactersLength == 0)
        {
            // Provider search layout: all chars in vectors
            for (int i = SeedLength - 1; i >= 0; i--)
            {
                char ch = (char)SeedLastCharacters[i][VectorLane];
                num = (1.1239285023 / num * ch * Math.PI + Math.PI * (i + key.Length + 1)) % 1;
            }
        }
        else
        {
            // Generic layout (original code)
            int seedLastCharacterLength = SeedLength - SeedFirstCharactersLength;
            
            // First we do the first characters of the seed which are the same between all vector lanes
            for (int i = SeedFirstCharactersLength - 1; i >= 0; i--)
            {
                num = (1.1239285023 / num * SeedFirstCharacters[i] * Math.PI + Math.PI * (i + key.Length + seedLastCharacterLength + 1)) % 1;
            }

            // Then we get the characters for our lane
            for (int i = seedLastCharacterLength - 1; i >= 0; i--)
            {
                num = (1.1239285023 / num * SeedLastCharacters[i][VectorLane] * Math.PI + Math.PI * (key.Length + i + 1)) % 1;
            }
        }

        // Then the actual key
        for (int i = key.Length - 1; i >= 0; i--)
        {
            num = (1.1239285023 / num * key[i] * Math.PI + (i + 1) * Math.PI) % 1;
        }

        return num;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static double IteratePRNG(double state)
    {
        return Math.Round((state * 1.72431234 + 2.134453429141) % 1, 13);
    }


#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySinglePrngStream CreatePrngStreamCached(string key)
    {
        return new(PseudoHashCached(key));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySinglePrngStream CreatePrngStream(string key)
    {
        return new(PseudoHash(key));
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public double GetNextPrngState(ref MotelySinglePrngStream stream)
    {
        return stream.State = IteratePRNG(stream.State);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public double GetNextPseudoSeed(ref MotelySinglePrngStream stream)
    {
        return (GetNextPrngState(ref stream) + SeedHashCache.GetSeedHash(VectorLane)) / 2d;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public LuaRandom GetNextLuaRandom(ref MotelySinglePrngStream stream)
    {
        return new LuaRandom(GetNextPseudoSeed(ref stream));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public double GetNextRandom(ref MotelySinglePrngStream stream)
    {
        return LuaRandom.Random(GetNextPseudoSeed(ref stream));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public int GetNextRandomInt(ref MotelySinglePrngStream stream, int min, int max)
    {
        return LuaRandom.RandInt(GetNextPseudoSeed(ref stream), min, max);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public T GetNextRandomElement<T>(ref MotelySinglePrngStream stream, T[] choices)
    {
        return choices[GetNextRandomInt(ref stream, 0, choices.Length)];
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelySinglePrngStream CreateResamplePrngStreamCached(string key, int resample)
    {
        // We don't cache resamples > 8 because they'd use an extra digit
        if (resample < 8) return CreatePrngStreamCached(key + MotelyPrngKeys.Resample + (resample + 2));
        return CreateResamplePrngStream(key, resample);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelySinglePrngStream CreateResamplePrngStream(string key, int resample)
    {
        return CreatePrngStream(key + MotelyPrngKeys.Resample + (resample + 2));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelySingleResampleStream CreateResampleStreamCached(string key)
    {
        return new(CreatePrngStreamCached(key), true);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelySingleResampleStream CreateResampleStream(string key)
    {
        return new(CreatePrngStream(key), false);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private ref MotelySinglePrngStream GetResamplePrngStream(ref MotelySingleResampleStream resampleStream, string key, int resample)
    {
        if (resample < MotelySingleResampleStream.StackResampleCount)
        {
            ref MotelySinglePrngStream prngStream = ref resampleStream.ResamplePrngStreams[resample];

            if (resample == resampleStream.ResamplePrngStreamInitCount)
            {
                ++resampleStream.ResamplePrngStreamInitCount;
                if (resampleStream.IsCached) prngStream = CreateResamplePrngStreamCached(key, resample);
                else prngStream = CreateResamplePrngStream(key, resample);
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
                return ref Unsafe.Unbox<MotelySinglePrngStream>(resampleStream.HighResamplePrngStreams[resample]);
            }

            object prngStreamObject = new MotelySinglePrngStream();

            resampleStream.HighResamplePrngStreams.Add(prngStreamObject);

            ref MotelySinglePrngStream prngStream = ref Unsafe.Unbox<MotelySinglePrngStream>(prngStreamObject);

            if (resampleStream.IsCached) prngStream = CreateResamplePrngStreamCached(key, resample);
            else prngStream = CreateResamplePrngStream(key, resample);

            return ref prngStream;
        }
    }

}