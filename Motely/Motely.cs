namespace Motely;

public static partial class MotelyGlobals
{
    public const int MaxCachedPseudoHashKeyLength = 32;

    public static readonly char[] SeedDigits = [.. "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"];
    public const int MaxSeedLength = 8;
    public const int MaxVectorWidth = 8;

    public const int ItemTypeMask = 0xFFFF;

    public const int PlayingCardRankMask = 0b1111;
    public const int PlayingCardSuitOffset = 4;
    public const int PlayingCardSuitMask = 0b11 << PlayingCardSuitOffset;

    public const int ItemTypeCategoryOffset = 12;
    public const int ItemTypeCategoryMask = 0b1111 << ItemTypeCategoryOffset;

    public const int JokerRarityOffset = 10;
    public const int JokerRarityMask = 0b11 << JokerRarityOffset;

    public const int ItemSealOffset = 16;
    public const int ItemSealMask = 0b111 << ItemSealOffset;

    public const int ItemEnhancementOffset = 19;
    public const int ItemEnhancementMask = 0b1111 << ItemEnhancementOffset;

    public const int ItemEditionOffset = 23;
    public const int ItemEditionMask = 0b111 << ItemEditionOffset;

    public const int BoosterPackTypeOffset = 2;
    public const int BoosterPackTypeMask = 0b11 << BoosterPackTypeOffset;
    public const int BoosterPackSizeMask = 0b11;

    public const int PerishableStickerOffset = 31;
    public const int EternalStickerOffset = 30;
    public const int RentalStickerOffset = 29;

    public const int BossTypeOffset = 31;
    public const int BossTypeMask = 0b1 << BossTypeOffset;
    public const int BossRequiredAnteOffset = 28;
    public const int BossRequiredAnteMask = 0b111 << BossRequiredAnteOffset;

    public const int JokerMisprintMin = 0;
    public const int JokerMisprintMax = 23;
    public const double JokerCavendishChance = 1000;
    public const double JokerGrosMichelChance = 6;

    public const double EnhancementLuckyMoneyChance = 15;
    public const double EnhancementLuckyMultChance = 5;

    public const double TarrotWheelChance = 4;

    public static readonly MotelyItem[] StandardCardPool =
    [
        // Spades
        new(MotelyPlayingCard.S2),
        new(MotelyPlayingCard.S3),
        new(MotelyPlayingCard.S4),
        new(MotelyPlayingCard.S5),
        new(MotelyPlayingCard.S6),
        new(MotelyPlayingCard.S7),
        new(MotelyPlayingCard.S8),
        new(MotelyPlayingCard.S9),
        new(MotelyPlayingCard.S10),
        new(MotelyPlayingCard.SJ),
        new(MotelyPlayingCard.SQ),
        new(MotelyPlayingCard.SK),
        new(MotelyPlayingCard.SA),
        // Hearts
        new(MotelyPlayingCard.H2),
        new(MotelyPlayingCard.H3),
        new(MotelyPlayingCard.H4),
        new(MotelyPlayingCard.H5),
        new(MotelyPlayingCard.H6),
        new(MotelyPlayingCard.H7),
        new(MotelyPlayingCard.H8),
        new(MotelyPlayingCard.H9),
        new(MotelyPlayingCard.H10),
        new(MotelyPlayingCard.HJ),
        new(MotelyPlayingCard.HQ),
        new(MotelyPlayingCard.HK),
        new(MotelyPlayingCard.HA),
        // Clubs
        new(MotelyPlayingCard.C2),
        new(MotelyPlayingCard.C3),
        new(MotelyPlayingCard.C4),
        new(MotelyPlayingCard.C5),
        new(MotelyPlayingCard.C6),
        new(MotelyPlayingCard.C7),
        new(MotelyPlayingCard.C8),
        new(MotelyPlayingCard.C9),
        new(MotelyPlayingCard.C10),
        new(MotelyPlayingCard.CJ),
        new(MotelyPlayingCard.CQ),
        new(MotelyPlayingCard.CK),
        new(MotelyPlayingCard.CA),
        // Diamonds
        new(MotelyPlayingCard.D2),
        new(MotelyPlayingCard.D3),
        new(MotelyPlayingCard.D4),
        new(MotelyPlayingCard.D5),
        new(MotelyPlayingCard.D6),
        new(MotelyPlayingCard.D7),
        new(MotelyPlayingCard.D8),
        new(MotelyPlayingCard.D9),
        new(MotelyPlayingCard.D10),
        new(MotelyPlayingCard.DJ),
        new(MotelyPlayingCard.DQ),
        new(MotelyPlayingCard.DK),
        new(MotelyPlayingCard.DA),
    ];

    /// <summary>
    /// Parse a padding string like "67Z" into the specific chars to use for padding.
    /// Filters to valid seed digits, deduplicates, uppercases. Returns null if empty/invalid.
    /// </summary>
    public static char[]? ParsePaddingChars(string? padding)
    {
        if (string.IsNullOrEmpty(padding))
            return null;

        var chars = padding.ToUpperInvariant()
            .Where(c => Array.IndexOf(SeedDigits, c) >= 0)
            .Distinct()
            .ToArray();

        return chars.Length > 0 ? chars : null;
    }

    /// <summary>
    /// Generate seeds for multiple keywords, combining their padded variations lazily.
    /// All keywords use the same padding chars and pad length derived from the longest keyword.
    /// </summary>
    public static IEnumerable<string> GeneratePaddedSeedsForKeywords(
        IEnumerable<string> keywords,
        char[]? validChars = null
    )
    {
        foreach (var keyword in keywords)
        {
            if (string.IsNullOrEmpty(keyword)) continue;
            int padLen = MaxSeedLength - keyword.Length;
            if (padLen < 0) continue; // keyword too long — skip silently
            foreach (var seed in GeneratePaddedSeeds(keyword, padLen, validChars))
                yield return seed;
        }
    }

    /// <summary>
    /// Total seed count for multiple keywords with the given padding chars.
    /// </summary>
    public static ulong GetPaddedSeedCountForKeywords(
        IEnumerable<string> keywords,
        char[]? validChars = null
    )
    {
        ulong total = 0;
        foreach (var keyword in keywords)
        {
            if (string.IsNullOrEmpty(keyword)) continue;
            int padLen = MaxSeedLength - keyword.Length;
            if (padLen < 0) continue;
            total += GetPaddedSeedCount(keyword, padLen, validChars);
        }
        return total;
    }

    /// <summary>
    /// Generate all seed variations by padding a keyword with the given valid characters.
    /// Pads 0-3 characters at all positions (prefix, suffix, infix).
    /// </summary>
    public static ulong GetPaddedSeedCount(string keyword, int padLen, char[]? validChars = null)
    {
        validChars ??= SeedDigits;

        if (validChars.Length == 0)
            throw new ArgumentException("validChars cannot be empty", nameof(validChars));
        if (string.IsNullOrEmpty(keyword))
            throw new ArgumentException("keyword cannot be null or empty", nameof(keyword));

        if (padLen <= 0)
            return 1;

        checked
        {
            ulong combinations = 1;
            for (int i = 0; i < padLen; i++)
                combinations *= (ulong)validChars.Length;

            return padLen switch
            {
                1 => combinations * 2,
                2 => combinations * 3,
                3 => combinations * 4,
                _ => combinations * (ulong)(keyword.Length + 1),
            };
        }
    }

    public static IEnumerable<string> GeneratePaddedSeeds(
        string keyword,
        int padLen,
        char[]? validChars = null
    )
    {
        validChars ??= SeedDigits;

        if (validChars.Length == 0)
            throw new ArgumentException("validChars cannot be empty", nameof(validChars));
        if (string.IsNullOrEmpty(keyword))
            throw new ArgumentException("keyword cannot be null or empty", nameof(keyword));

        if (padLen <= 0)
        {
            yield return keyword;
            yield break;
        }

        if (padLen == 1)
        {
            foreach (var c in validChars)
            {
                yield return c + keyword;
                yield return keyword + c;
            }
        }
        else if (padLen == 2)
        {
            foreach (var c1 in validChars)
            {
                foreach (var c2 in validChars)
                {
                    yield return $"{c1}{c2}{keyword}";
                    yield return $"{keyword}{c1}{c2}";
                    yield return $"{c1}{keyword}{c2}";
                }
            }
        }
        else if (padLen == 3)
        {
            foreach (var c1 in validChars)
            {
                foreach (var c2 in validChars)
                {
                    foreach (var c3 in validChars)
                    {
                        yield return $"{c1}{c2}{c3}{keyword}";
                        yield return $"{keyword}{c1}{c2}{c3}";
                        yield return $"{c1}{keyword}{c2}{c3}";
                        yield return $"{c1}{c2}{keyword}{c3}";
                    }
                }
            }
        }
        else
        {
            // For padLen > 3, generate combinations recursively
            foreach (var seed in GenerateNPadVariations(keyword, padLen, validChars, ""))
                yield return seed;
        }
    }

    private static IEnumerable<string> GenerateNPadVariations(
        string keyword,
        int padLen,
        char[] validChars,
        string current
    )
    {
        if (current.Length == padLen)
        {
            // Insert at all positions: prefix, suffix, and infix
            yield return current + keyword;
            yield return keyword + current;
            for (int i = 1; i < keyword.Length; i++)
            {
                yield return keyword.Substring(0, i) + current + keyword.Substring(i);
            }
            yield break;
        }

        foreach (var c in validChars)
        {
            foreach (var seed in GenerateNPadVariations(keyword, padLen, validChars, current + c))
                yield return seed;
        }
    }
}