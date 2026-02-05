namespace Motely;

/// <summary>
/// Factory for aesthetic seed providers in Balatro (no 0; only 1-9, A-Z).
/// Each method returns an IMotelySeedProvider for a specific seed generation style.
/// </summary>
public static class AestheticSeedProvider
{
    /// <summary>Single keyword with optional padding variations.</summary>
    public static IMotelySeedProvider Keyword(
        string keyword,
        int maxPadding = 0,
        char[]? validChars = null
    )
    {
        validChars ??= Motely.SeedDigits;
        var seeds = Motely.GeneratePaddedSeeds(keyword, maxPadding, validChars).ToList();
        return new LazyEnumerableSeedProvider(seeds);
    }

    /// <summary>Multiple keywords, each processed independently.</summary>
    public static IMotelySeedProvider Keywords(params string[] keywords)
    {
        var allSeeds = new List<string>();
        foreach (var kw in keywords)
            allSeeds.AddRange(Motely.GeneratePaddedSeeds(kw, 0, Motely.SeedDigits));
        return new LazyEnumerableSeedProvider(allSeeds);
    }

    /// <summary>Palindrome seeds: reads the same forwards and backwards.</summary>
    public static IMotelySeedProvider Palindrome(int length = 8)
    {
        var seeds = new List<string>();
        int halfLen = (length + 1) / 2;
        GeneratePalindromes(halfLen, length, "").ForEach(s => seeds.Add(s));
        return new LazyEnumerableSeedProvider(seeds);
    }

    private static List<string> GeneratePalindromes(int halfLen, int targetLen, string current)
    {
        var results = new List<string>();
        if (current.Length == halfLen)
        {
            var seed =
                current + new string(current.Reverse().Skip(targetLen % 2 == 0 ? 0 : 1).ToArray());
            results.Add(seed);
            return results;
        }
        foreach (var c in Motely.SeedDigits)
        {
            results.AddRange(GeneratePalindromes(halfLen, targetLen, current + c));
        }
        return results;
    }

    /// <summary>Slide: 4-in-a-row repeated char, e.g. ABG5OOOO.</summary>
    public static IMotelySeedProvider Slide(int slideLength = 4)
    {
        var seeds = new List<string>();
        int prefixLen = Math.Max(0, 8 - slideLength);
        foreach (var c in Motely.SeedDigits)
        {
            for (int i = 0; i < Math.Pow(35, prefixLen) && seeds.Count < 10000; i++)
            {
                string prefix = ConvertToBase35(i, prefixLen);
                seeds.Add((prefix + new string(c, slideLength)).Substring(0, 8));
            }
        }
        return new LazyEnumerableSeedProvider(seeds);
    }

    /// <summary>Ditto: one 4-letter sequence repeated, e.g. ABCDABCD.</summary>
    public static IMotelySeedProvider Ditto(string fourLetterSequence)
    {
        fourLetterSequence = (fourLetterSequence + "XXXX").Substring(0, 4).ToUpperInvariant();
        var seeds = new List<string>();
        for (int reps = 1; reps <= 2; reps++)
            seeds.Add((new string(fourLetterSequence[0], 8)));
        seeds.Add(fourLetterSequence + fourLetterSequence);
        return new LazyEnumerableSeedProvider(seeds);
    }

    /// <summary>Beef: HEX-like aesthetic, 6 chars padded with O/X, e.g. OXBEEF or XBEEFX.</summary>
    public static IMotelySeedProvider Beef()
    {
        var seeds = new List<string> { "XDEADBE", "OXBEEF1", "XBEEFX1", "DEADBEE", "BEEFX11" };
        return new LazyEnumerableSeedProvider(seeds);
    }

    /// <summary>Jumble: all permutations of given characters.</summary>
    public static IMotelySeedProvider Jumble(string chars)
    {
        chars = chars.ToUpperInvariant();
        var seeds = GeneratePermutations(chars).ToList();
        return new LazyEnumerableSeedProvider(seeds);
    }

    private static IEnumerable<string> GeneratePermutations(string chars, string current = "")
    {
        if (string.IsNullOrEmpty(chars))
        {
            yield return current;
            yield break;
        }
        for (int i = 0; i < chars.Length; i++)
        {
            var remaining = chars.Remove(i, 1);
            foreach (var perm in GeneratePermutations(remaining, current + chars[i]))
                yield return perm;
        }
    }

    /// <summary>Ayy1: "Lucky" seeds with cosmic/celestial aesthetic—repeating patterns that feel fortunate.</summary>
    public static IMotelySeedProvider Ayy1()
    {
        var seeds = new List<string>();

        // Lucky repeating patterns (7s, As, Bs etc.)
        foreach (var c in new[] { '7', '8', '9', 'A', 'B', 'C', 'L', 'U' })
        {
            seeds.Add(new string(c, 8));
            seeds.Add(
                new string(c, 4)
                    + new string(
                        new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9' }[(int)c % 9],
                        4
                    )
            );
        }

        // Star patterns (corners, edges)
        seeds.Add("1234567");
        seeds.Add("1111999");
        seeds.Add("AAAAAZZZ");

        // Alternating (cosmic dance)
        seeds.Add("1A1A1A1");
        seeds.Add("5B5B5B5");
        seeds.Add("9Z9Z9Z9");

        return new LazyEnumerableSeedProvider(seeds);
    }

    private static string ConvertToBase35(int num, int minLen)
    {
        if (minLen == 0)
            return "";
        var result = "";
        for (int i = 0; i < minLen; i++)
        {
            result = Motely.SeedDigits[num % 35] + result;
            num /= 35;
        }
        return result;
    }
}

/// <summary>
/// Internal provider that wraps a list of seeds (lazy enumerable-style).
/// </summary>
internal sealed class LazyEnumerableSeedProvider : IMotelySeedProvider, IDisposable
{
    private readonly IEnumerator<string> _enumerator;
    private readonly int _totalCount;
    private int _retrieved;

    public LazyEnumerableSeedProvider(List<string> seeds)
    {
        _totalCount = seeds.Count;
        _enumerator = seeds.GetEnumerator();
        _retrieved = 0;
    }

    public int SeedCount => _totalCount;

    public ReadOnlySpan<char> NextSeed()
    {
        if (_retrieved >= _totalCount)
            return ReadOnlySpan<char>.Empty;
        if (_enumerator.MoveNext())
        {
            _retrieved++;
            return _enumerator.Current.AsSpan();
        }
        return ReadOnlySpan<char>.Empty;
    }

    public int NextSeeds(string[] seeds)
    {
        int count = 0;
        for (int i = 0; i < seeds.Length && _retrieved < _totalCount; i++)
        {
            if (_enumerator.MoveNext())
            {
                seeds[i] = _enumerator.Current;
                count++;
                _retrieved++;
            }
        }
        return count;
    }

    public void Dispose() => _enumerator?.Dispose();
}
