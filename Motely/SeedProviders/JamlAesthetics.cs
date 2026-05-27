namespace Motely.SeedProviders;

/// <summary>
/// Generation and counting of JAML <see cref="JamlAesthetic"/> seed spaces over Motely’s alphabet
/// and length rules. Palindrome/Psychosis live here; keyword-backed aesthetics
/// (<see cref="JamlAesthetic.Gross"/>, <see cref="JamlAesthetic.Funny"/>,
/// <see cref="JamlAesthetic.Balatro"/>) delegate to <see cref="MotelySeedKeywordSequences"/>.
/// </summary>
public static class JamlAesthetics
{
    /// <summary>Returns how many seeds <paramref name="aesthetic"/> enumerates (Motely search space).</summary>
    public static long GetSeedCount(JamlAesthetic aesthetic) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.SeedCount,
            JamlAesthetic.Psychosis => PsychosisAestheticSeeds.SeedCount,
            JamlAesthetic.Gross
            or JamlAesthetic.Funny
            or JamlAesthetic.Balatro => MotelySeedKeywordSequences.GetAestheticSeedCount(aesthetic),
            _ => throw new ArgumentOutOfRangeException(nameof(aesthetic)),
        };

    /// <summary>Deterministic enumeration in the same order as <see cref="Motely.SeedProviders.MotelyPalindromeSeedProvider"/>.</summary>
    public static IEnumerable<string> EnumerateSeeds(JamlAesthetic aesthetic) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.Enumerate(),
            JamlAesthetic.Psychosis => PsychosisAestheticSeeds.Enumerate(),
            JamlAesthetic.Gross
            or JamlAesthetic.Funny
            or JamlAesthetic.Balatro => MotelySeedKeywordSequences.EnumerateAestheticSeeds(aesthetic),
            _ => throw new ArgumentOutOfRangeException(nameof(aesthetic)),
        };
}

/// <summary>Palindrome seeds: mirror-generated halves over <see cref="MotelyGlobals.SeedDigits"/>, lengths 1..<see cref="MotelyGlobals.MaxSeedLength"/>.</summary>
file static class PalindromeAestheticSeeds
{
    public static int SeedCount => CalculateSeedCount();

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
