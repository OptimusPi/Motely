using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class SeedGenerator
{
    private const string VALID_CHARS = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int SEED_LENGTH = 8;

    public static List<string> GenerateSeedsFromKeyword(string keyword)
    {
        var seeds = new HashSet<string>();
        
        // Normalize keyword
        keyword = keyword.ToUpper().Trim();
        Console.WriteLine($"[SeedGenerator] Processing keyword: '{keyword}'");
        
        // Remove invalid characters
        keyword = new string(keyword.Where(c => VALID_CHARS.Contains(c)).ToArray());
        Console.WriteLine($"[SeedGenerator] After cleaning: '{keyword}' (length: {keyword.Length})");
        
        if (string.IsNullOrEmpty(keyword))
            return new List<string>();
        
        // If keyword is already 8 chars, just return it
        if (keyword.Length == SEED_LENGTH)
        {
            seeds.Add(keyword);
            return seeds.ToList();
        }
        
        // If keyword is too long, truncate and generate variations
        if (keyword.Length > SEED_LENGTH)
        {
            // Take first 8 chars
            seeds.Add(keyword.Substring(0, SEED_LENGTH));
            
            // Take last 8 chars
            if (keyword.Length > SEED_LENGTH)
                seeds.Add(keyword.Substring(keyword.Length - SEED_LENGTH));
            
            // Take middle 8 chars
            if (keyword.Length > SEED_LENGTH + 1)
            {
                int start = (keyword.Length - SEED_LENGTH) / 2;
                seeds.Add(keyword.Substring(start, SEED_LENGTH));
            }
            
            return seeds.ToList();
        }
        
        // Keyword is shorter than 8 chars - generate ALL positional variations
        Console.WriteLine($"[SeedGenerator] Generating positional variations for '{keyword}'...");
        GenerateAllPositionalVariations(keyword, seeds);
        
        Console.WriteLine($"[SeedGenerator] Total seeds generated: {seeds.Count}");
        return seeds.ToList();
    }

    private static void GenerateAllPositionalVariations(string keyword, HashSet<string> seeds)
    {
        int keywordLen = keyword.Length;
        int paddingNeeded = SEED_LENGTH - keywordLen;
        
        // Define a subset of padding characters for comprehensive but manageable generation
        string commonPadChars = "123456789"; // Just numbers for primary generation
        
        // For each position the keyword can be placed (0 to paddingNeeded)
        for (int position = 0; position <= paddingNeeded; position++)
        {
            int prefixLen = position;
            int suffixLen = paddingNeeded - position;
            
            // Generate with number padding (most common)
            foreach (char padChar in commonPadChars)
            {
                string prefix = new string(padChar, prefixLen);
                string suffix = new string(padChar, suffixLen);
                string seed = prefix + keyword + suffix;
                
                if (seed.Length == SEED_LENGTH)
                {
                    seeds.Add(seed);
                }
            }
            
            // Add some letter padding for variety (just A, X, Z for manageable size)
            foreach (char padChar in "AXZ")
            {
                string prefix = new string(padChar, prefixLen);
                string suffix = new string(padChar, suffixLen);
                seeds.Add(prefix + keyword + suffix);
            }
            
            // Mixed padding patterns (different prefix/suffix)
            if (prefixLen > 0 && suffixLen > 0)
            {
                // Try different combinations of popular padding
                foreach (char prefixChar in "1469")
                {
                    foreach (char suffixChar in "1469")
                    {
                        if (prefixChar != suffixChar) // Only mixed patterns
                        {
                            string prefix = new string(prefixChar, prefixLen);
                            string suffix = new string(suffixChar, suffixLen);
                            seeds.Add(prefix + keyword + suffix);
                        }
                    }
                }
            }
        }
        
        // Add special number combinations at specific positions
        GenerateSpecialNumberPadding(keyword, seeds);
        
        // Add some fully mixed patterns for variety
        GenerateMixedPatterns(keyword, seeds);
    }

    private static void GenerateMixedPatterns(string keyword, HashSet<string> seeds)
    {
        int paddingNeeded = SEED_LENGTH - keyword.Length;
        
        // Generate some patterns with alternating characters
        string[] patterns = { "12", "69", "42", "13", "37", "77", "88", "99" };
        
        foreach (var pattern in patterns)
        {
            // Fill padding with alternating pattern
            for (int pos = 0; pos <= paddingNeeded; pos++)
            {
                string prefix = "";
                string suffix = "";
                
                // Build prefix
                for (int i = 0; i < pos; i++)
                {
                    prefix += pattern[i % pattern.Length];
                }
                
                // Build suffix
                for (int i = 0; i < paddingNeeded - pos; i++)
                {
                    suffix += pattern[i % pattern.Length];
                }
                
                seeds.Add(prefix + keyword + suffix);
            }
        }
    }

    private static void GenerateSpecialNumberPadding(string keyword, HashSet<string> seeds)
    {
        string[] specialNumbers = { "69", "420", "666", "777", "888", "999", "1337", "1111", "2222", "3333", "4444", "5555", "6666", "7777", "8888", "9999" };
        
        foreach (var num in specialNumbers)
        {
            // Try placing keyword at different positions with this number
            if (keyword.Length + num.Length <= SEED_LENGTH)
            {
                int remaining = SEED_LENGTH - keyword.Length - num.Length;
                
                // Number at start
                if (num.Length + keyword.Length == SEED_LENGTH)
                {
                    seeds.Add(num + keyword);
                }
                else if (remaining > 0)
                {
                    // Pad the rest with 1s, 9s, etc
                    foreach (char filler in "19")
                    {
                        seeds.Add(num + keyword + new string(filler, remaining));
                        seeds.Add(num + new string(filler, remaining) + keyword);
                    }
                }
                
                // Number at end
                if (keyword.Length + num.Length == SEED_LENGTH)
                {
                    seeds.Add(keyword + num);
                }
                else if (remaining > 0)
                {
                    foreach (char filler in "19")
                    {
                        seeds.Add(keyword + num + new string(filler, remaining));
                        seeds.Add(new string(filler, remaining) + keyword + num);
                    }
                }
            }
        }
    }

    // Remove old methods that are no longer needed
    // GenerateLeetVariations and GenerateNumberCombinations are removed since we're doing exhaustive generation

    // Helper method to integrate with command line arguments
    public static List<string> ProcessKeywordArgument(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--keyword" && i + 1 < args.Length)
            {
                string keyword = args[i + 1];
                return GenerateSeedsFromKeyword(keyword);
            }
        }
        return new List<string>();
    }

    // Example usage in your main program
    // var seeds = ProcessKeywordArgument(args);
    // This class is meant to be used by Program.cs, not run standalone
}

// Extension class for easy integration into your existing SIMD searcher
public static class SeedSearcherExtensions
{
    public static void AddKeywordSeeds(this List<string> existingSeeds, string keyword)
    {
        var newSeeds = SeedGenerator.GenerateSeedsFromKeyword(keyword);
        existingSeeds.AddRange(newSeeds);
    }
    
    // For SIMD-friendly batch processing
    public static string[] GetKeywordSeedArray(string keyword)
    {
        return SeedGenerator.GenerateSeedsFromKeyword(keyword).ToArray();
    }
}