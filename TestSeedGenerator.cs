using System;
using System.Linq;

class TestSeedGenerator
{
    // Test method - not a main entry point
    public static void TestGeneration()
    {
        Console.WriteLine("Testing SeedGenerator...\n");
        
        string[] testKeywords = { "LOVE", "TEST", "HI", "X" };
        
        foreach (var keyword in testKeywords)
        {
            var seeds = SeedGenerator.GenerateSeedsFromKeyword(keyword);
            Console.WriteLine($"\nKeyword: {keyword}");
            Console.WriteLine($"Generated: {seeds.Count} seeds");
            
            if (seeds.Count > 0)
            {
                Console.WriteLine("First 20 seeds:");
                foreach (var seed in seeds.Take(20))
                {
                    Console.WriteLine($"  {seed}");
                }
                
                if (seeds.Count > 20)
                {
                    Console.WriteLine($"  ... and {seeds.Count - 20} more");
                }
                
                // Verify all seeds are 8 chars
                var invalid = seeds.Where(s => s.Length != 8).ToList();
                if (invalid.Any())
                {
                    Console.WriteLine($"WARNING: Found {invalid.Count} invalid seeds!");
                }
            }
        }
    }
}