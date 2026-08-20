using System;
using System.Collections.Generic;

namespace Motely;

/// <summary>
/// Helper for Balatro seed math (Bijective Base-35 conversion)
/// Characters: 123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ (No 0)
///
/// Bijective notation:
/// "" = 0
/// "1" = 1
/// "9" = 9
/// "A" = 10
/// ...
/// "Z" = 35
/// "11" = 36
///
/// Standard Balatro seeds are 8 characters.
/// In the global bijective space, 8-character seeds occupy indices:
/// StartOffset8 = (35^8 - 1) / 34 = 66,231,629,136
/// EndOffset8 = (35^9 - 1) / 34 = 2,318,107,019,761
/// </summary>
public static class SeedMath
{
    private const string CHARS = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly Dictionary<char, int> CharToVal = new();

    static SeedMath()
    {
        for (int i = 0; i < CHARS.Length; i++)
        {
            CharToVal[CHARS[i]] = i;
        }
    }

    /// <summary>
    /// Global Bijective Index. "" = 0, "1" = 1, "11" = 36.
    /// </summary>
    public static long SeedToTotalIndex(string seed)
    {
        seed = seed.ToUpperInvariant();
        long total = 0;
        long multiplier = 1;

        for (int i = seed.Length - 1; i >= 0; i--)
        {
            char c = seed[i];
            if (!CharToVal.TryGetValue(c, out int val))
                throw new ArgumentException($"Invalid seed character: {c}");

            // val is 0-34. Bijective uses 1-35.
            total += (val + 1) * multiplier;
            multiplier *= 35;
        }
        return total;
    }

    public static string TotalIndexToSeed(long index)
    {
        if (index == 0)
            return "";
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        char[] buffer = new char[15];
        int pos = 15;
        long current = index;

        while (current > 0)
        {
            current--; // Bijective shift
            int val = (int)(current % 35);
            buffer[--pos] = CHARS[val];
            current /= 35;
        }

        return new string(buffer, pos, 15 - pos);
    }

    /// <summary>
    /// Motely Search Index: 0-based index relative to the start of the space of a specific length.
    /// e.g. For length 8, SearchIndex 0 corresponds to "11111111".
    /// </summary>
    public static long SeedToSearchIndex(string seed)
    {
        return SeedToTotalIndex(seed) - GetFirstSeedOfLength(seed.Length);
    }

    public static string SearchIndexToSeed(long index, int length)
    {
        return TotalIndexToSeed(index + GetFirstSeedOfLength(length));
    }

    /// <summary>
    /// (35^L - 1) / 34. The first bijective index for a string of length L.
    /// </summary>
    public static long GetFirstSeedOfLength(int length)
    {
        if (length <= 0)
            return 0;
        long sum = 0;
        long pow = 1;
        for (int i = 0; i < length; i++)
        {
            sum += pow;
            if (i < length - 1)
                pow *= 35;
        }
        return sum;
    }

    /// <summary>
    /// Delegates to <see cref="SearchIndexToSeed"/>: <paramref name="index"/> is relative to the start of the length-<paramref name="length"/> space (Balatro seeds are 8 chars).
    /// </summary>
    public static string TotalIndexToSeed(long index, int length) =>
        SearchIndexToSeed(index, length);

    public static long SeedToBatchIndex(string seed, int batchSize)
    {
        int prefixLen = 8 - batchSize;
        if (prefixLen <= 0)
            return 0;

        // The engine writes batch digits into the LAST prefixLen characters, least significant
        // first — MotelySearch.SearchSequentialBatch, _digits[MaxSeedLength - i - 1]. Reading the
        // head instead landed --startSeed and the resume hint at the mirrored point in the space:
        // asking for DWIQ6W31 started the sweep among seeds ending in D.
        long index = 0;
        for (int i = seed.Length - 1; i >= batchSize; i--)
            index =
                index * MotelyGlobals.SeedDigits.Length
                + MotelyGlobals.SeedDigits.IndexOf(seed[i]);
        return index;
    }

    public static string BatchIndexToSeedPrefix(long batchIndex, int batchSize)
    {
        int prefixLen = 8 - batchSize;
        if (prefixLen <= 0)
            return "";
        // index is treated as relative
        return SearchIndexToSeed(batchIndex, prefixLen);
    }

    /// <summary>
    /// Largest Motely search index for a fixed seed length (inclusive). For length 8 this is 35^8 − 1.
    /// </summary>
    public static long MaxSearchIndexInclusive(int length)
    {
        if (length <= 0)
            return -1;
        long p = 1;
        for (int i = 0; i < length; i++)
            checked
            {
                p *= 35;
            }
        return p - 1;
    }

    /// <summary>
    /// Maps an inclusive Motely search-index range for 8-character seeds to sequential batch indices.
    /// The second value is an exclusive end batch index (same convention as <c>WithEndBatchIndex</c>).
    /// </summary>
    public static (long StartBatchIndex, long EndBatchIndexExclusive) SearchIndexRangeToBatchRange(
        long startSearchIndexInclusive,
        long stopSearchIndexInclusive,
        int batchCharCount
    )
    {
        if (batchCharCount is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(batchCharCount));

        long maxIdx = MaxSearchIndexInclusive(8);
        if (
            startSearchIndexInclusive < 0
            || stopSearchIndexInclusive < startSearchIndexInclusive
            || stopSearchIndexInclusive > maxIdx
        )
        {
            throw new ArgumentOutOfRangeException(
                $"{nameof(startSearchIndexInclusive)}..{nameof(stopSearchIndexInclusive)}",
                $"Must satisfy 0 <= start <= stop <= {maxIdx}."
            );
        }

        string startSeed = SearchIndexToSeed(startSearchIndexInclusive, 8);
        string stopSeed = SearchIndexToSeed(stopSearchIndexInclusive, 8);
        long startBatch = SeedToBatchIndex(startSeed, batchCharCount);
        long stopBatch = SeedToBatchIndex(stopSeed, batchCharCount);
        return (startBatch, stopBatch + 1);
    }
}
