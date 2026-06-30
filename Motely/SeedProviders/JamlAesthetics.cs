namespace Motely.SeedProviders;

/// <summary>
/// Seed-space constraints declared under top-level <c>aesthetics</c> in a JAML document.
/// Enumeration and classification: <see cref="JamlAesthetics"/>.
/// </summary>
public enum JamlAesthetic
{
    Palindrome,
    Echo,
    Gross,
    Funny,
    Balatro,
}

/// <summary>
/// Generation and counting of JAML <see cref="JamlAesthetic"/> seed spaces over Motely’s alphabet
/// and length rules. Palindrome/Echo live here; keyword-backed aesthetics
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
            JamlAesthetic.Echo => EchoAestheticSeeds.SeedCount,
            JamlAesthetic.Gross or JamlAesthetic.Funny or JamlAesthetic.Balatro =>
                MotelySeedKeywordSequences.GetAestheticSeedCount(aesthetic),
            _ => throw new ArgumentOutOfRangeException(nameof(aesthetic)),
        };

    /// <summary>Deterministic enumeration in the same order as <see cref="Motely.SeedProviders.MotelyPalindromeSeedProvider"/>.</summary>
    public static IEnumerable<string> EnumerateSeeds(JamlAesthetic aesthetic) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.Enumerate(),
            JamlAesthetic.Echo => EchoAestheticSeeds.Enumerate(),
            JamlAesthetic.Gross or JamlAesthetic.Funny or JamlAesthetic.Balatro =>
                MotelySeedKeywordSequences.EnumerateAestheticSeeds(aesthetic),
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

    private static IEnumerable<string> GenerateRecursive(
        char[] buffer,
        int pos,
        int halfLen,
        int totalLen
    )
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

/// <summary>Echo seeds: echo pattern ABAxBxxx where A,B are A-Z and x are 1-9,A-Z (always 8 chars).</summary>
file static class EchoAestheticSeeds
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static int SeedCount => checked(26 * 26 * 35 * 35 * 35 * 35); // ~1.01 billion

    public static IEnumerable<string> Enumerate()
    {
        // char[] (not ReadOnlySpan) so it can be held across the `yield return`s below.
        char[] alphabet = MotelyGlobals.SeedDigits.ToArray();
        // Reuse one 8-char buffer instead of interpolating a fresh string per seed (~1B of them).
        // Pattern ABAxBxxx: positions 0,2 = a; positions 1,4 = b; positions 3,5,6,7 = x1..x4.
        char[] buffer = new char[8];

        foreach (char a in Letters)
        {
            buffer[0] = a;
            buffer[2] = a;
            foreach (char b in Letters)
            {
                buffer[1] = b;
                buffer[4] = b;
                for (int i1 = 0; i1 < alphabet.Length; i1++)
                {
                    buffer[3] = alphabet[i1];
                    for (int i2 = 0; i2 < alphabet.Length; i2++)
                    {
                        buffer[5] = alphabet[i2];
                        for (int i3 = 0; i3 < alphabet.Length; i3++)
                        {
                            buffer[6] = alphabet[i3];
                            for (int i4 = 0; i4 < alphabet.Length; i4++)
                            {
                                buffer[7] = alphabet[i4];
                                yield return new string(buffer);
                            }
                        }
                    }
                }
            }
        }
    }
}
