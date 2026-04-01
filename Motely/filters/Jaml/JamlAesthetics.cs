using System.Collections.Generic;

namespace Motely.Filters;

/// <summary>
/// Generation and classification of JAML <see cref="JamlAesthetic"/> values over Motely’s seed alphabet and length rules.
/// </summary>
public static class JamlAesthetics
{
    /// <summary>Returns how many seeds <paramref name="aesthetic"/> enumerates (Motely search space).</summary>
    public static long GetSeedCount(JamlAesthetic aesthetic) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.SeedCount,
            JamlAesthetic.Psychosis => PsychosisAestheticSeeds.SeedCount,
            JamlAesthetic.Gross => ClampSeedCount(MotelyGlobals.GetPaddedSeedCountForKeywords(MotelySeedKeywordSequences.GrossKeywords)),
            JamlAesthetic.Nsfw => ClampSeedCount(MotelyGlobals.GetPaddedSeedCountForKeywords(MotelySeedKeywordSequences.NsfwKeywords)),
            JamlAesthetic.Funny => ClampSeedCount(MotelyGlobals.GetPaddedSeedCountForKeywords(MotelySeedKeywordSequences.FunnyKeywords)),
            JamlAesthetic.Balatro => ClampSeedCount(MotelyGlobals.GetPaddedSeedCountForKeywords(MotelySeedKeywordSequences.BalatroKeywords)),
            _ => throw new ArgumentOutOfRangeException(nameof(aesthetic)),
        };

    /// <summary>Deterministic enumeration in the same order as <see cref="MotelyPalindromeSeedProvider"/>.</summary>
    public static IEnumerable<string> EnumerateSeeds(JamlAesthetic aesthetic) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.Enumerate(),
            JamlAesthetic.Psychosis => PsychosisAestheticSeeds.Enumerate(),
            JamlAesthetic.Gross => KeywordAestheticSeeds.Enumerate(MotelySeedKeywordSequences.GrossKeywords),
            JamlAesthetic.Nsfw => KeywordAestheticSeeds.Enumerate(MotelySeedKeywordSequences.NsfwKeywords),
            JamlAesthetic.Funny => KeywordAestheticSeeds.Enumerate(MotelySeedKeywordSequences.FunnyKeywords),
            JamlAesthetic.Balatro => KeywordAestheticSeeds.Enumerate(MotelySeedKeywordSequences.BalatroKeywords),
            _ => throw new ArgumentOutOfRangeException(nameof(aesthetic)),
        };

    /// <summary>Whether <paramref name="seed"/> satisfies <paramref name="aesthetic"/> under Motely seed rules.</summary>
    public static bool Matches(JamlAesthetic aesthetic, ReadOnlySpan<char> seed) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.Matches(seed),
            JamlAesthetic.Psychosis => PsychosisAestheticSeeds.Matches(seed),
            JamlAesthetic.Gross => KeywordAestheticSeeds.Matches(seed, MotelySeedKeywordSequences.GrossKeywords),
            JamlAesthetic.Nsfw => KeywordAestheticSeeds.Matches(seed, MotelySeedKeywordSequences.NsfwKeywords),
            JamlAesthetic.Funny => KeywordAestheticSeeds.Matches(seed, MotelySeedKeywordSequences.FunnyKeywords),
            JamlAesthetic.Balatro => KeywordAestheticSeeds.Matches(seed, MotelySeedKeywordSequences.BalatroKeywords),
            _ => throw new ArgumentOutOfRangeException(nameof(aesthetic)),
        };

    /// <summary>Appends every <see cref="JamlAesthetic"/> that matches <paramref name="seed"/>.</summary>
    public static void CollectMatches(ReadOnlySpan<char> seed, List<JamlAesthetic> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var aesthetic in KnownAesthetics)
        {
            if (Matches(aesthetic, seed))
                destination.Add(aesthetic);
        }
    }

    private static long ClampSeedCount(ulong count) =>
        count > (ulong)long.MaxValue ? long.MaxValue : (long)count;

    /// <summary>Order used by <see cref="CollectMatches"/>; extend when new enum members ship.</summary>
    private static ReadOnlySpan<JamlAesthetic> KnownAesthetics =>
    [
        JamlAesthetic.Palindrome,
        JamlAesthetic.Psychosis,
        JamlAesthetic.Gross,
        JamlAesthetic.Nsfw,
        JamlAesthetic.Funny,
        JamlAesthetic.Balatro,
    ];
}

/// <summary>Keyword-based aesthetic seeds: padded keyword sequences via <see cref="MotelyGlobals.GeneratePaddedSeedsForKeywords"/>.</summary>
file static class KeywordAestheticSeeds
{
    public static IEnumerable<string> Enumerate(IEnumerable<string> keywords)
    {
        return MotelyGlobals.GeneratePaddedSeedsForKeywords(keywords, null);
    }

    public static bool Matches(ReadOnlySpan<char> seed, IEnumerable<string> keywords)
    {
        if (seed.Length is < 1 or > MotelyGlobals.MaxSeedLength)
            return false;

        ReadOnlySpan<char> alphabet = MotelyGlobals.SeedDigits;
        for (int i = 0; i < seed.Length; i++)
        {
            if (!alphabet.Contains(seed[i]))
                return false;
        }

        string seedStr = new string(seed);
        foreach (var keyword in keywords)
        {
            if (seedStr.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>Palindrome seeds: mirror-generated halves over <see cref="MotelyGlobals.SeedDigits"/>, lengths 1..<see cref="MotelyGlobals.MaxSeedLength"/>.</summary>
file static class PalindromeAestheticSeeds
{
    public static int SeedCount => CalculateSeedCount();

    public static bool Matches(ReadOnlySpan<char> seed)
    {
        int len = seed.Length;
        if (len is < 1 or > MotelyGlobals.MaxSeedLength)
            return false;

        ReadOnlySpan<char> alphabet = MotelyGlobals.SeedDigits;
        for (int i = 0; i < len; i++)
        {
            if (!alphabet.Contains(seed[i]))
                return false;
        }

        for (int i = 0, j = len - 1; i < j; i++, j--)
        {
            if (seed[i] != seed[j])
                return false;
        }

        return true;
    }

    public static IEnumerable<string> Enumerate()
    {
        for (int len = 1; len <= MotelyGlobals.MaxSeedLength; len++)
        {
            foreach (var palindrome in OfLength(len))
                yield return palindrome;
        }
    }

    private static IEnumerable<string> OfLength(int length)
    {
        if (length == 1)
        {
            for (int i = 0; i < MotelyGlobals.SeedDigits.Length; i++)
                yield return MotelyGlobals.SeedDigits[i].ToString();
            yield break;
        }

        int halfLen = (length + 1) / 2;
        foreach (var palindrome in GenerateRecursive(new char[length], 0, halfLen, length))
            yield return palindrome;
    }

    private static IEnumerable<string> GenerateRecursive(char[] buffer, int pos, int halfLen, int totalLen)
    {
        if (pos >= halfLen)
        {
            for (int i = 0; i < halfLen; i++)
                buffer[totalLen - 1 - i] = buffer[i];
            yield return new string(buffer, 0, totalLen);
            yield break;
        }

        for (int i = 0; i < MotelyGlobals.SeedDigits.Length; i++)
        {
            buffer[pos] = MotelyGlobals.SeedDigits[i];
            foreach (var result in GenerateRecursive(buffer, pos + 1, halfLen, totalLen))
                yield return result;
        }
    }

    private static int CalculateSeedCount()
    {
        checked
        {
            int total = 0;
            int seedDigitCount = MotelyGlobals.SeedDigits.Length;

            for (int len = 1; len <= MotelyGlobals.MaxSeedLength; len++)
            {
                int halfLen = (len + 1) / 2;
                int countForLength = 1;

                for (int i = 0; i < halfLen; i++)
                    countForLength *= seedDigitCount;

                total += countForLength;
            }

            return total;
        }
    }
}

/// <summary>Psychosis seeds: echo pattern ABAxBxxx where A,B are A-Z and x are 1-9,A-Z (always 8 chars).</summary>
file static class PsychosisAestheticSeeds
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static int SeedCount => checked(26 * 26 * 35 * 35 * 35 * 35); // ~1.01 billion

    public static bool Matches(ReadOnlySpan<char> seed)
    {
        if (seed.Length != 8)
            return false;

        ReadOnlySpan<char> alphabet = MotelyGlobals.SeedDigits;

        // Check all chars are valid
        for (int i = 0; i < seed.Length; i++)
        {
            if (!alphabet.Contains(seed[i]))
                return false;
        }

        // Check pattern: A at 0,2 | B at 1,4
        return seed[0] == seed[2] && seed[1] == seed[4];
    }

    public static IEnumerable<string> Enumerate()
    {
        string alphabet = new string(MotelyGlobals.SeedDigits);

        foreach (char a in Letters)
        {
            foreach (char b in Letters)
            {
                foreach (char x1 in alphabet)
                {
                    foreach (char x2 in alphabet)
                    {
                        foreach (char x3 in alphabet)
                        {
                            foreach (char x4 in alphabet)
                            {
                                yield return $"{a}{b}{a}{x1}{b}{x2}{x3}{x4}";
                            }
                        }
                    }
                }
            }
        }
    }
}
