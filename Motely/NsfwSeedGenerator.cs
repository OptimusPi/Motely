namespace Motely;

/// <summary>
/// Generates NSFW seed names dynamically for content filter testing.
/// Produces millions of combinations by combining words with valid Balatro characters.
/// Valid chars: A-Z, 1-9 (no 0, no lowercase) - 35 characters total.
/// Max seed length: 8 characters.
/// </summary>
public static class NsfwSeedGenerator
{
    // Valid Balatro seed characters (35 chars)
    private static readonly char[] ValidChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".ToCharArray();
    
    // Severity tiers for scoring (higher = more NSFW)
    private static readonly Dictionary<string, int> SeverityTier3 = new() { // 70-100 points - Very explicit
        {"FUCK", 90}, {"FUK", 85}, {"CUNT", 95}, {"KUNT", 90}, {"CVNT", 85},
        {"COCK", 80}, {"COKK", 75}, {"DICK", 80}, {"DIK", 75}, {"DIKK", 70},
        {"PUSSY", 85}, {"PUS", 60}, {"PORN", 80}, {"CUM", 75}, {"CUMM", 80},
        {"RAPE", 100}, {"PEDO", 100}, {"NAZI", 95}, {"NIGG", 100}
    };
    
    private static readonly Dictionary<string, int> SeverityTier2 = new() { // 40-69 points - Moderate
        {"SEX", 50}, {"SEXX", 55}, {"SEXY", 45}, {"SLUT", 60}, {"WHORE", 65},
        {"HOE", 40}, {"TIT", 55}, {"TITS", 60}, {"BOOB", 50}, {"ASS", 45},
        {"MILF", 55}, {"BONER", 50}, {"HORNY", 55}, {"NUDE", 45}, {"NAKED", 40},
        {"ANAL", 60}, {"ORAL", 50}, {"FAG", 70}, {"TWAT", 65}, {"VAG", 50},
        {"NUT", 40}, {"NUTS", 45}, {"JERK", 35}, {"WANK", 50}, {"FAP", 45},
        {"BWC", 55}, {"BBC", 55}, {"DTF", 50}, {"PAWG", 55}
    };
    
    private static readonly Dictionary<string, int> SeverityTier1 = new() { // 10-39 points - Mild/Funny
        {"SHIT", 30}, {"SHT", 25}, {"CRAP", 20}, {"DAMN", 15}, {"HELL", 15},
        {"WEED", 25}, {"POT", 20}, {"LSD", 30}, {"DANK", 20}, {"HIGH", 15},
        {"DOPE", 20}, {"STONER", 25}, {"BLUNT", 20},
        {"KILL", 25}, {"EVIL", 20}, {"DEMON", 20}, {"SATAN", 25},
        {"LOL", 10}, {"LMAO", 15}, {"YEET", 10}, {"CHAD", 15}, {"SIMP", 20}
    };
    
    // Funny number patterns (bonus points) - NO ZEROS, only 1-9!
    private static readonly Dictionary<string, int> FunnyNumbers = new() {
        {"69", 30}, {"42", 25}, {"666", 25}, {"777", 15}, {"888", 15},
        {"999", 10}, {"1337", 20}, {"69696969", 50}, {"4269", 35},
        {"111", 10}, {"222", 10}, {"333", 10}, {"444", 10}, {"555", 10},
        {"88", 15}, {"99", 10}, {"11", 5}, {"123", 10}, {"321", 10}
    };
    
    // NSFW base words (all uppercase, valid chars only)
    private static readonly string[] NsfwWords = {
        // Profanity (short forms work best for 8-char limit)
        "FUK", "FUKK", "SHT", "SHIT", "ASS", "DAMN", "HELL", "CRAP",
        // Sexual terms
        "SEX", "SEXX", "SEXY", "CUM", "CUMM", "DIK", "DIKK", "COK", "COKK", 
        "COCK", "KOCK", "TIT", "TITS", "BOOB", "MILF", "SLUT", "HOE",
        "PORN", "PRN", "LEWD", "NUDE", "PAWG", "DTF", "BWC", "BBC",
        "VAG", "PUS", "NUT", "NUTS", "JERK", "WANK", "FAP",
        // Slurs
        "FAG", "KUNT", "CVNT", "CUNT", "TWAT",
        // Drugs
        "WEED", "POT", "LSD", "DANK", "HIGH", "DOPE",
        // Other
        "KILL", "EVIL", "DEMON",
        // Meme/silly
        "LOL", "LMAO", "YEET", "CHAD", "SIMP"
    };
    
    // Number patterns for padding
    private static readonly string[] NumberPatterns = {
        "1", "11", "69", "67", "88", "99", "111", "123", "321",
        "420", "666", "777", "888", "999", "1234", "4321",
        "69X", "X69", "67X", "X67","42X", "X42"
    };
    
    // Letter patterns for padding  
    private static readonly string[] LetterPatterns = {
        "X", "XX", "XXX", "Z", "ZZ", "ZZZ", "S", "SS"
    };

    /// <summary>
    /// Generate all NSFW seed combinations. Returns an IEnumerable for streaming/lazy evaluation.
    /// Estimated output: 4+ million unique seeds.
    /// </summary>
    public static IEnumerable<string> GenerateAll()
    {
        // For 4M+ seeds, we need exhaustive padding
        // 3-char word + 5 chars padding: position options * 35^remaining
        // We'll generate ALL valid padding combinations up to 8 chars
        
        // 1. For each NSFW word, generate ALL padding combinations
        foreach (var word in NsfwWords)
        {
            int wordLen = word.Length;
            int maxPad = 8 - wordLen;
            
            // Word alone
            yield return word;
            
            // Generate all padding lengths from 1 to maxPad
            for (int padLen = 1; padLen <= maxPad; padLen++)
            {
                // Generate all combinations of padding chars
                foreach (var pad in GeneratePadding(padLen))
                {
                    // Prefix: PAD + WORD
                    yield return pad + word;
                    
                    // Suffix: WORD + PAD
                    yield return word + pad;
                }
                
                // Split padding: some before, some after
                if (padLen >= 2)
                {
                    for (int prefixLen = 1; prefixLen < padLen; prefixLen++)
                    {
                        int suffixLen = padLen - prefixLen;
                        foreach (var prefix in GeneratePadding(prefixLen))
                        {
                            foreach (var suffix in GeneratePadding(suffixLen))
                            {
                                yield return prefix + word + suffix;
                            }
                        }
                    }
                }
            }
        }
        
        // 2. Special compound words with padding
        string[] compounds = {
            "BIGDIK", "BIGCOK", "HOTMILF", "SEXGOD", "CUMKING",
            "DIKHEAD", "ASSHAT", "FUKFACE", "CUMSHOT", "FUKBOY",
            "FUKBOI", "CUMSLUT", "DIKPIC", "SEXBOMB", "CUMGOD"
        };
        
        foreach (var word in compounds)
        {
            int maxPad = 8 - word.Length;
            yield return word;
            
            for (int padLen = 1; padLen <= maxPad; padLen++)
            {
                foreach (var pad in GeneratePadding(padLen))
                {
                    yield return pad + word;
                    yield return word + pad;
                }
            }
        }
    }
    
    /// <summary>
    /// Generate all valid padding strings of given length.
    /// For len=1: 35 combos, len=2: 1225, len=3: 42875, len=4: 1.5M, len=5: 52M
    /// We limit to len <= 4 for memory/time sanity (still gives millions).
    /// </summary>
    private static IEnumerable<string> GeneratePadding(int length)
    {
        if (length <= 0)
        {
            yield return "";
            yield break;
        }
        
        if (length == 1)
        {
            foreach (var c in ValidChars)
                yield return c.ToString();
            yield break;
        }
        
        if (length == 2)
        {
            foreach (var c1 in ValidChars)
                foreach (var c2 in ValidChars)
                    yield return $"{c1}{c2}";
            yield break;
        }
        
        if (length == 3)
        {
            foreach (var c1 in ValidChars)
                foreach (var c2 in ValidChars)
                    foreach (var c3 in ValidChars)
                        yield return $"{c1}{c2}{c3}";
            yield break;
        }
        
        if (length == 4)
        {
            foreach (var c1 in ValidChars)
                foreach (var c2 in ValidChars)
                    foreach (var c3 in ValidChars)
                        foreach (var c4 in ValidChars)
                            yield return $"{c1}{c2}{c3}{c4}";
            yield break;
        }
        
        // For length >= 5, only generate specific patterns to avoid explosion
        // (35^5 = 52 million per pattern - too much)
        // Use repeating chars and common patterns instead
        foreach (var c in ValidChars)
        {
            yield return new string(c, length); // XXXXX
        }
        
        // Mixed patterns for longer padding
        foreach (var c1 in ValidChars)
        {
            foreach (var c2 in ValidChars)
            {
                if (c1 != c2)
                {
                    // Alternating: ABABA
                    char[] alt = new char[length];
                    for (int i = 0; i < length; i++)
                        alt[i] = (i % 2 == 0) ? c1 : c2;
                    yield return new string(alt);
                }
            }
        }
    }

    /// <summary>
    /// Generate seeds and write to a file. Returns count written.
    /// </summary>
    public static int WriteToFile(string filePath)
    {
        int count = 0;
        using var writer = new StreamWriter(filePath);
        foreach (var seed in GenerateAll())
        {
            writer.WriteLine(seed);
            count++;
        }
        return count;
    }

    /// <summary>
    /// Generate seeds as a list (for in-memory use).
    /// </summary>
    public static List<string> GenerateList()
    {
        return GenerateAll().ToList();
    }
    
    /// <summary>
    /// Get estimated count without generating all seeds.
    /// Math: ~50 words × (35^1 + 35^2 + 35^3 + 35^4) × 2 positions + split combos ≈ 4-5 million
    /// </summary>
    public static int EstimatedCount => 4_000_000;
    
    /// <summary>
    /// Score a seed for NSFW/funny content. Returns 0-100+ scale.
    /// Higher = more NSFW/funnier. Can exceed 100 for compound matches.
    /// Negative scores indicate "boring" seeds with no matches.
    /// </summary>
    public static int ScoreSeed(string seed)
    {
        if (string.IsNullOrEmpty(seed)) return -10;
        
        seed = seed.ToUpperInvariant();
        int score = 0;
        int matchCount = 0;
        
        // Check Tier 3 (most explicit) - highest scores
        foreach (var (pattern, points) in SeverityTier3)
        {
            if (seed.Contains(pattern))
            {
                score += points;
                matchCount++;
            }
        }
        
        // Check Tier 2 (moderate)
        foreach (var (pattern, points) in SeverityTier2)
        {
            if (seed.Contains(pattern))
            {
                score += points;
                matchCount++;
            }
        }
        
        // Check Tier 1 (mild/funny)
        foreach (var (pattern, points) in SeverityTier1)
        {
            if (seed.Contains(pattern))
            {
                score += points;
                matchCount++;
            }
        }
        
        // Bonus for funny numbers
        foreach (var (pattern, points) in FunnyNumbers)
        {
            if (seed.Contains(pattern))
            {
                score += points;
                matchCount++;
            }
        }
        
        // Compound word bonus: multiple NSFW words = extra spicy
        if (matchCount >= 2) score += 20;
        if (matchCount >= 3) score += 30;
        
        // Length bonus: 8-char seeds that are fully NSFW are impressive
        if (seed.Length == 8 && matchCount > 0) score += 10;
        
        // Repeating char patterns (like XXX, 666, 888)
        if (HasRepeatingPattern(seed)) score += 5;
        
        // Leetspeak detection bonus
        if (ContainsLeetspeak(seed)) score += 15;
        
        // Palindrome bonus (reads same forwards/backwards)
        if (IsPalindrome(seed)) score += 10;
        
        // No matches = boring seed, negative score
        if (matchCount == 0) return -5;
        
        return score;
    }
    
    /// <summary>
    /// Get a text rating for the score
    /// </summary>
    public static string GetRating(int score) => score switch
    {
        >= 150 => "🔥🔥🔥 LEGENDARY",
        >= 100 => "🔥🔥 EPIC",
        >= 70 => "🔥 SPICY",
        >= 40 => "😈 NAUGHTY",
        >= 20 => "😏 CHEEKY", 
        >= 10 => "🙈 MILD",
        >= 0 => "😐 MEH",
        _ => "😴 BORING"
    };
    
    /// <summary>
    /// Get emoji indicator for quick visual
    /// </summary>
    public static string GetEmoji(int score) => score switch
    {
        >= 100 => "🔥",
        >= 70 => "🌶️",
        >= 40 => "😈",
        >= 20 => "😏",
        >= 0 => "🙈",
        _ => "😴"
    };
    
    private static bool HasRepeatingPattern(string seed)
    {
        for (int i = 0; i < seed.Length - 2; i++)
        {
            if (seed[i] == seed[i + 1] && seed[i + 1] == seed[i + 2])
                return true;
        }
        return false;
    }
    
    private static bool ContainsLeetspeak(string seed)
    {
        // Common leetspeak substitutions using ONLY valid seed chars (A-Z, 1-9, NO ZERO)
        // 1=I/L, 3=E, 4=A, 5=S, 7=T, 8=B, 9=G
        var leetspeakPatterns = new[] {
            "1337", "H4X", "L33T", "H4CK", "PH4T",
            "5EX", "4SS", "D1K", "T1T", "8OO8", "B88B",
            "PR1N", "A55", "T1TS", "B1TCH", "D1CK"
        };
        
        foreach (var pattern in leetspeakPatterns)
        {
            if (seed.Contains(pattern)) return true;
        }
        return false;
    }
    
    private static bool IsPalindrome(string seed)
    {
        if (seed.Length < 3) return false;
        for (int i = 0; i < seed.Length / 2; i++)
        {
            if (seed[i] != seed[seed.Length - 1 - i])
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// Score and rate a seed, returning a tuple of (score, rating, emoji)
    /// </summary>
    public static (int Score, string Rating, string Emoji) AnalyzeSeed(string seed)
    {
        int score = ScoreSeed(seed);
        return (score, GetRating(score), GetEmoji(score));
    }
}
