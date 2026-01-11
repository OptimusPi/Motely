namespace Motely;

/// <summary>
/// Generates and scores SFW (Safe For Work) seed names that are fun/cool.
/// Examples: COOLBR5, HOTMALT7, CHIPS88, EPICWIN, TACOBEL
/// Valid chars: A-Z, 1-9 (no 0, no lowercase) - 35 characters total.
/// </summary>
public static class SfwSeedGenerator
{
    // Valid Balatro seed characters (35 chars)
    private static readonly char[] ValidChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".ToCharArray();
    
    // Cool/Awesome words (high score)
    private static readonly Dictionary<string, int> CoolWords = new() {
        {"EPIC", 40}, {"MEGA", 35}, {"SUPER", 40}, {"ULTRA", 35}, {"HYPER", 30},
        {"COOL", 35}, {"BEST", 30}, {"BOSS", 35}, {"KING", 35}, {"QUEEN", 35},
        {"HERO", 30}, {"LEGEND", 40}, {"PRIME", 30}, {"ELITE", 35}, {"PRO", 25},
        {"ACE", 30}, {"STAR", 30}, {"GOLD", 30}, {"PLAT", 25}, {"DIAMOND", 35},
        {"FLASH", 25}, {"BLAZE", 25}, {"FIRE", 25}, {"ICE", 20}, {"STORM", 25},
        {"TITAN", 30}, {"GIANT", 25}, {"POWER", 30}, {"FORCE", 25}, {"TURBO", 30}
    };
    
    // Fun food words (everyone loves food)
    private static readonly Dictionary<string, int> FoodWords = new() {
        {"TACO", 35}, {"PIZZA", 35}, {"CHIPS", 30}, {"BACON", 35}, {"STEAK", 30},
        {"BURGER", 35}, {"FRIES", 25}, {"NACHO", 30}, {"SALSA", 25}, {"BEANS", 20},
        {"CANDY", 25}, {"CAKE", 25}, {"PIE", 25}, {"DONUT", 30}, {"WAFFLE", 30},
        {"TOAST", 20}, {"BREAD", 15}, {"CHEESE", 25}, {"HAM", 20}, {"EGG", 15},
        {"APPLE", 20}, {"GRAPE", 20}, {"LEMON", 20}, {"LIME", 20}, {"MANGO", 25},
        {"SOUP", 15}, {"RICE", 15}, {"NOODLE", 25}, {"PASTA", 20}, {"MALT", 20}
    };
    
    // Positive/Lucky words
    private static readonly Dictionary<string, int> LuckyWords = new() {
        {"WIN", 35}, {"LUCK", 35}, {"LUCKY", 40}, {"RICH", 30}, {"CASH", 30},
        {"GOLD", 30}, {"GEMS", 25}, {"LOOT", 25}, {"PRIZE", 30}, {"BONUS", 30},
        {"JACKPOT", 40}, {"SCORE", 25}, {"BLING", 25}, {"COIN", 20}, {"MINT", 20},
        {"BANK", 20}, {"VAULT", 25}, {"CHEST", 25}, {"HOARD", 20}, {"STACK", 20}
    };
    
    // Meme/Internet culture (SFW ones)
    private static readonly Dictionary<string, int> MemeWords = new() {
        {"YEET", 30}, {"BASED", 35}, {"SIGMA", 35}, {"ALPHA", 30}, {"BETA", 15},
        {"GAMER", 30}, {"NOOB", 20}, {"NEWB", 15}, {"NERD", 20}, {"GEEK", 20},
        {"MEME", 25}, {"DANK", 25}, {"SWAG", 25}, {"DRIP", 30}, {"FLEX", 30},
        {"VIBE", 25}, {"MOOD", 20}, {"BRO", 25}, {"DUDE", 20}, {"HOMIE", 25},
        {"GOAT", 35}, {"BEAST", 30}, {"CHAD", 30}, {"GG", 20}, {"EZ", 20},
        {"RATIO", 25}, {"COPE", 20}, {"SLAY", 25}, {"QUEEN", 30}, {"KING", 30}
    };
    
    // Nature/Animals (wholesome)
    private static readonly Dictionary<string, int> NatureWords = new() {
        {"CAT", 25}, {"DOG", 25}, {"BEAR", 25}, {"WOLF", 30}, {"LION", 30},
        {"TIGER", 30}, {"EAGLE", 30}, {"HAWK", 25}, {"OWL", 25}, {"FOX", 25},
        {"SHARK", 30}, {"WHALE", 25}, {"FISH", 20}, {"BIRD", 20}, {"BEE", 20},
        {"TREE", 15}, {"LEAF", 15}, {"ROSE", 20}, {"SUN", 25}, {"MOON", 25},
        {"STAR", 30}, {"SKY", 20}, {"WIND", 20}, {"WAVE", 20}, {"ROCK", 15}
    };
    
    // Action words
    private static readonly Dictionary<string, int> ActionWords = new() {
        {"RUN", 20}, {"JUMP", 20}, {"DASH", 25}, {"RUSH", 25}, {"ZOOM", 25},
        {"BLAST", 30}, {"SMASH", 30}, {"CRUSH", 25}, {"SLAM", 25}, {"BANG", 25},
        {"SPIN", 20}, {"FLIP", 25}, {"ROLL", 20}, {"KICK", 20}, {"PUNCH", 25},
        {"NINJA", 35}, {"PIRATE", 30}, {"WIZARD", 35}, {"KNIGHT", 30}, {"DRAGON", 35}
    };
    
    // Fun number patterns (bonus points)
    private static readonly Dictionary<string, int> FunNumbers = new() {
        {"69", 20}, {"42", 25}, {"777", 30}, {"888", 25}, {"999", 20},
        {"1337", 35}, {"111", 15}, {"222", 15}, {"333", 15}, {"444", 15},
        {"555", 15}, {"666", 20}, {"123", 20}, {"321", 20}, {"88", 15},
        {"99", 10}, {"11", 10}, {"55", 10}, {"77", 15}
    };
    
    // Words to AVOID (NSFW check - if any of these match, score is 0)
    private static readonly string[] NsfwPatterns = {
        "FUK", "FCK", "FUCK", "SHIT", "SHT", "ASS", "CUNT", "KUNT", "CVNT",
        "SEX", "CUM", "DIK", "DICK", "COK", "COCK", "TIT", "BOOB", "PORN",
        "SLUT", "WHORE", "HOE", "FAG", "TWAT", "VAG", "PUS", "NUT", "MILF",
        "ANAL", "NUDE", "NAKED", "RAPE", "PEDO", "NAZI", "NIGG",
        "WEED", "POT", "LSD", "CRACK", "METH", "DOPE"
    };

    /// <summary>
    /// Score a seed for SFW fun/cool content. Returns 0-100+ scale.
    /// Returns -100 if NSFW content detected (invalid for SFW use).
    /// </summary>
    public static int ScoreSeed(string seed)
    {
        if (string.IsNullOrEmpty(seed)) return -10;
        
        seed = seed.ToUpperInvariant();
        
        // NSFW check first - if any bad words, return negative score
        foreach (var pattern in NsfwPatterns)
        {
            if (seed.Contains(pattern))
                return -100; // Not SFW!
        }
        
        int score = 0;
        int matchCount = 0;
        
        // Check all word categories
        foreach (var (pattern, points) in CoolWords)
        {
            if (seed.Contains(pattern)) { score += points; matchCount++; }
        }
        foreach (var (pattern, points) in FoodWords)
        {
            if (seed.Contains(pattern)) { score += points; matchCount++; }
        }
        foreach (var (pattern, points) in LuckyWords)
        {
            if (seed.Contains(pattern)) { score += points; matchCount++; }
        }
        foreach (var (pattern, points) in MemeWords)
        {
            if (seed.Contains(pattern)) { score += points; matchCount++; }
        }
        foreach (var (pattern, points) in NatureWords)
        {
            if (seed.Contains(pattern)) { score += points; matchCount++; }
        }
        foreach (var (pattern, points) in ActionWords)
        {
            if (seed.Contains(pattern)) { score += points; matchCount++; }
        }
        
        // Bonus for fun numbers
        foreach (var (pattern, points) in FunNumbers)
        {
            if (seed.Contains(pattern)) { score += points; matchCount++; }
        }
        
        // Compound bonus: multiple cool words = extra awesome
        if (matchCount >= 2) score += 15;
        if (matchCount >= 3) score += 25;
        
        // Full 8-char bonus
        if (seed.Length == 8 && matchCount > 0) score += 10;
        
        // Palindrome bonus
        if (IsPalindrome(seed)) score += 15;
        
        // Repeating patterns (like XXX, 777)
        if (HasRepeatingPattern(seed)) score += 5;
        
        // No matches = boring
        if (matchCount == 0) return -5;
        
        return score;
    }
    
    /// <summary>
    /// Get a text rating for the SFW score
    /// </summary>
    public static string GetRating(int score) => score switch
    {
        <= -100 => "🚫 NSFW DETECTED",
        >= 100 => "⭐⭐⭐ LEGENDARY",
        >= 70 => "⭐⭐ EPIC",
        >= 50 => "⭐ AWESOME",
        >= 30 => "😎 COOL",
        >= 15 => "👍 NICE",
        >= 0 => "😐 MEH",
        _ => "😴 BORING"
    };
    
    /// <summary>
    /// Get emoji indicator for quick visual
    /// </summary>
    public static string GetEmoji(int score) => score switch
    {
        <= -100 => "🚫",
        >= 100 => "⭐",
        >= 70 => "🌟",
        >= 50 => "✨",
        >= 30 => "😎",
        >= 15 => "👍",
        >= 0 => "😐",
        _ => "😴"
    };
    
    /// <summary>
    /// Check if a seed is SFW (no NSFW patterns detected)
    /// </summary>
    public static bool IsSfw(string seed)
    {
        if (string.IsNullOrEmpty(seed)) return true;
        seed = seed.ToUpperInvariant();
        
        foreach (var pattern in NsfwPatterns)
        {
            if (seed.Contains(pattern))
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// Score and rate a seed, returning a tuple of (score, rating, emoji, isSfw)
    /// </summary>
    public static (int Score, string Rating, string Emoji, bool IsSfw) AnalyzeSeed(string seed)
    {
        int score = ScoreSeed(seed);
        bool isSfw = score > -100;
        return (score, GetRating(score), GetEmoji(score), isSfw);
    }
    
    private static bool HasRepeatingPattern(string seed)
    {
        for (int i = 0; i < seed.Length - 2; i++)
        {
            if (seed[i] == seed[i + 1] && seed[i + 1] == seed[i + 2])
                return true;
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
    
    // SFW base words for generation
    private static readonly string[] SfwWords = {
        // Cool
        "EPIC", "MEGA", "COOL", "BEST", "BOSS", "KING", "HERO", "ACE", "STAR", "PRO",
        // Food
        "TACO", "PIZZA", "CHIPS", "BACON", "NACHO", "CANDY", "CAKE", "PIE", "WAFFLE",
        // Lucky
        "WIN", "LUCK", "RICH", "CASH", "GOLD", "LOOT", "GEMS", "BONUS",
        // Meme
        "YEET", "BASED", "SIGMA", "GAMER", "SWAG", "DRIP", "BRO", "DUDE", "GOAT",
        // Nature
        "CAT", "DOG", "WOLF", "LION", "FOX", "BEAR", "OWL", "SUN", "MOON",
        // Action
        "DASH", "ZOOM", "BLAST", "NINJA", "WIZARD", "DRAGON"
    };
    
    /// <summary>
    /// Generate all SFW seed combinations. Returns an IEnumerable for streaming.
    /// </summary>
    public static IEnumerable<string> GenerateAll()
    {
        foreach (var word in SfwWords)
        {
            int maxPad = 8 - word.Length;
            yield return word;
            
            for (int padLen = 1; padLen <= Math.Min(maxPad, 4); padLen++)
            {
                foreach (var pad in GeneratePadding(padLen))
                {
                    yield return pad + word;
                    yield return word + pad;
                }
            }
        }
    }
    
    private static IEnumerable<string> GeneratePadding(int length)
    {
        if (length <= 0) { yield return ""; yield break; }
        
        if (length == 1)
        {
            foreach (var c in ValidChars) yield return c.ToString();
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
        
        // For longer, just do repeating patterns
        foreach (var c in ValidChars)
            yield return new string(c, length);
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
            // Double-check it's actually SFW before writing
            if (IsSfw(seed))
            {
                writer.WriteLine(seed);
                count++;
            }
        }
        return count;
    }
}
