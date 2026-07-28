namespace Motely.SeedProviders;

/// <summary>
/// Seed-space constraints declared under top-level <c>aesthetics</c> in a JAML document.
/// Enumeration and classification: <see cref="JamlAesthetics"/>.
/// </summary>
public enum JamlAesthetic
{
    Palindrome,
    Echo,
    Mirror,
    Repeater,
    Step,
    Leet,
    Gross,
    Funny,
    Balatro,
    Nsfw,
}

/// <summary>
/// Generation and counting of JAML <see cref="JamlAesthetic"/> seed spaces over Motely's alphabet
/// and length rules. Palindrome/Echo/Mirror/Repeater/Step live here; keyword-backed aesthetics
/// (<see cref="JamlAesthetic.Gross"/>, <see cref="JamlAesthetic.Funny"/>,
/// <see cref="JamlAesthetic.Balatro"/>, <see cref="JamlAesthetic.Nsfw"/>) delegate to
/// <see cref="MotelySeedKeywordSequences"/>.
/// </summary>
public static class JamlAesthetics
{
    /// <summary>Returns how many seeds <paramref name="aesthetic"/> enumerates (Motely search space).</summary>
    public static long GetSeedCount(JamlAesthetic aesthetic) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.SeedCount,
            JamlAesthetic.Echo => EchoAestheticSeeds.SeedCount,
            JamlAesthetic.Mirror => MirrorAestheticSeeds.SeedCount,
            JamlAesthetic.Repeater => RepeaterAestheticSeeds.SeedCount,
            JamlAesthetic.Step => StepAestheticSeeds.SeedCount,
            JamlAesthetic.Gross
                or JamlAesthetic.Funny
                or JamlAesthetic.Balatro
                or JamlAesthetic.Leet
                or JamlAesthetic.Nsfw => MotelySeedKeywordSequences.GetAestheticSeedCount(aesthetic),
            _ => throw new ArgumentOutOfRangeException(nameof(aesthetic)),
        };

    /// <summary>Deterministic enumeration in the same order as <see cref="Motely.SeedProviders.MotelyPalindromeSeedProvider"/>.</summary>
    public static IEnumerable<string> EnumerateSeeds(JamlAesthetic aesthetic) =>
        aesthetic switch
        {
            JamlAesthetic.Palindrome => PalindromeAestheticSeeds.Enumerate(),
            JamlAesthetic.Echo => EchoAestheticSeeds.Enumerate(),
            JamlAesthetic.Mirror => MirrorAestheticSeeds.Enumerate(),
            JamlAesthetic.Repeater => RepeaterAestheticSeeds.Enumerate(),
            JamlAesthetic.Step => StepAestheticSeeds.Enumerate(),
            JamlAesthetic.Gross
                or JamlAesthetic.Funny
                or JamlAesthetic.Balatro
                or JamlAesthetic.Leet
                or JamlAesthetic.Nsfw => MotelySeedKeywordSequences.EnumerateAestheticSeeds(
                aesthetic
            ),
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

/// <summary>
/// Mirror-symmetric seeds: composed only of chars that look the same in a mirror
/// (A H I M O T U V W X Y 1 8). Lengths 1..MaxSeedLength.
/// </summary>
file static class MirrorAestheticSeeds
{
    private static readonly char[] MirrorChars = "AHIMOTUVWXY18".ToCharArray();

    // Mirror chars count ^ 1 + count ^ 2 + ... + count ^ 8
    // = (count ^ 9 - count) / (count - 1) with integer arithmetic
    public static long SeedCount
    {
        get
        {
            checked
            {
                long total = 0;
                int c = MirrorChars.Length; // 13
                long pow = 1;
                for (int len = 1; len <= MotelyGlobals.MaxSeedLength; len++)
                {
                    pow *= c;
                    total += pow;
                }
                return total;
            }
        }
    }

    public static IEnumerable<string> Enumerate()
    {
        char[] buf = new char[MotelyGlobals.MaxSeedLength];

        for (int len = 1; len <= MotelyGlobals.MaxSeedLength; len++)
        {
            foreach (var seed in Generate(buf, 0, len))
                yield return seed;
        }
    }

    private static IEnumerable<string> Generate(char[] buf, int pos, int length)
    {
        if (pos == length)
        {
            yield return new string(buf, 0, length);
            yield break;
        }

        foreach (char c in MirrorChars)
        {
            buf[pos] = c;
            foreach (var seed in Generate(buf, pos + 1, length))
                yield return seed;
        }
    }
}

/// <summary>
/// Repeater seeds: a base pattern repeated to fill 8 chars. Patterns are drawn from SeedDigits.
/// E.g. "A" -> AAAAAAAA, "AB" -> ABABABAB, "ABC" -> ABCABCAB, "ABCD" -> ABCDABCD, "ABCDE" -> ABCDEABC.
/// </summary>
file static class RepeaterAestheticSeeds
{
    public static long SeedCount
    {
        get
        {
            checked
            {
                char[] alphabet = MotelyGlobals.SeedDigits;
                int c = alphabet.Length;
                long total = 0;

                // Pattern lengths 1 through 7. For pattern length p there are c^p patterns, each
                // producing exactly 1 seed (always padded to 8). Length 8 is excluded on purpose:
                // a pattern that already fills the seed repeats zero times, so it is the identity —
                // all 35^8 seeds, the entire search space, arriving one heap-allocated string at a
                // time. That is not an aesthetic, and it starves every family queued behind it.
                for (int p = 1; p < MotelyGlobals.MaxSeedLength; p++)
                {
                    long patterns = 1;
                    for (int i = 0; i < p; i++)
                        patterns *= c;
                    total += patterns;
                }

                return total;
            }
        }
    }

    public static IEnumerable<string> Enumerate()
    {
        char[] alphabet = MotelyGlobals.SeedDigits;
        char[] buf = new char[MotelyGlobals.MaxSeedLength];

        // 1 through 7, matching SeedCount above — a length-8 pattern repeats zero times and would
        // enumerate the whole 35^8 space here, so the sweep would never reach the next aesthetic.
        for (int patternLen = 1; patternLen < MotelyGlobals.MaxSeedLength; patternLen++)
        {
            // Build a pattern buffer then recursively fill it
            char[] pattern = new char[patternLen];
            foreach (var filled in GeneratePatterns(pattern, 0, alphabet, buf))
                yield return filled;
        }
    }

    private static IEnumerable<string> GeneratePatterns(
        char[] pattern,
        int pos,
        char[] alphabet,
        char[] buf
    )
    {
        if (pos == pattern.Length)
        {
            // Fill buf by repeating the pattern
            for (int i = 0; i < buf.Length; i++)
                buf[i] = pattern[i % pattern.Length];
            yield return new string(buf);
            yield break;
        }

        foreach (char c in alphabet)
        {
            pattern[pos] = c;
            foreach (var seed in GeneratePatterns(pattern, pos + 1, alphabet, buf))
                yield return seed;
        }
    }
}

/// <summary>
/// Step seeds: evenly-spaced alphabet steps. Always 8 chars. Each character equals (prev + step) mod 35
/// over the SeedDigits ordering (1-9 then A-Z). Steps 1..34 produce unique sequences.
/// Also includes constant-step for step 0 (all same char) and backward steps.
/// </summary>
file static class StepAestheticSeeds
{
    // For step s starting at index i, the seed is: i, (i+s)%35, (i+2s)%35, ... repeated 8 times.
    // Steps 1..34 each produce 35 seeds, plus step 0 which produces 35 seeds (all same char).
    // But steps that are multiples of 5, 7 produce shorter cycles that still give distinct 8-char seeds.
    // Total: 35 seeds for each of 35 steps = 35 * 35 = 1225 seeds. Nice and small.
    public static long SeedCount => checked(
        MotelyGlobals.SeedDigits.Length * MotelyGlobals.SeedDigits.Length
    ); // 35 * 35 = 1225

    public static IEnumerable<string> Enumerate()
    {
        char[] alphabet = MotelyGlobals.SeedDigits;
        int n = alphabet.Length;
        char[] buf = new char[MotelyGlobals.MaxSeedLength];

        for (int step = 0; step < n; step++)
        {
            for (int start = 0; start < n; start++)
            {
                for (int i = 0; i < buf.Length; i++)
                    buf[i] = alphabet[(start + i * step) % n];
                yield return new string(buf);
            }
        }
    }
}
