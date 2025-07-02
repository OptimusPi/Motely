using System;
using Motely.Filters;

namespace Motely
{
    /// <summary>
    /// Utility class for testing and validating Ouija configurations
    /// </summary>
    public static class OuijaValidator
    {
        public static void ValidateConfig(string configPath)
        {
            try
            {
                Console.WriteLine($"🔍 Validating config: {configPath}");
                
                // Load config
                var config = OuijaConfig.Load(configPath, OuijaConfig.GetOptions());
                Console.WriteLine($"✅ Config loaded successfully");
                Console.WriteLine($"   Needs: {config.Needs?.Length ?? 0}");
                Console.WriteLine($"   Wants: {config.Wants?.Length ?? 0}");

                // Test filter creation
                var filterDesc = new OuijaJsonFilterDesc(config);
                var filterCtx = new MotelyFilterCreationContext();
                var filter = filterDesc.CreateFilter(ref filterCtx);
                Console.WriteLine($"✅ Filter created successfully");
                Console.WriteLine($"   Cached streams: {filterCtx.CachedPseudohashKeyLengths.Count}");

                // Validate needs
                foreach (var need in config.Needs ?? [])
                {
                    ValidateDesire(need, "NEED");
                }

                // Validate wants  
                foreach (var want in config.Wants ?? [])
                {
                    ValidateDesire(want, "WANT");
                }

                Console.WriteLine($"✅ Configuration is valid!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Validation failed: {ex.Message}");
                throw;
            }
        }

        private static void ValidateDesire(OuijaConfig.Desire desire, string type)
        {
            Console.WriteLine($"   {type}: {desire.Type} = {desire.Value}");
            
            switch (desire.Type)
            {
                case "SmallBlindTag":
                case "BigBlindTag":
                    if (!Enum.TryParse<MotelyTag>(desire.Value, true, out var tag))
                    {
                        throw new ArgumentException($"Invalid tag: {desire.Value}");
                    }
                    Console.WriteLine($"      ✅ Tag {tag} is valid");
                    break;

                case "Joker":
                case "SoulJoker":
                    if (!Enum.TryParse<MotelyJoker>(desire.Value, true, out var joker))
                    {
                        throw new ArgumentException($"Invalid joker: {desire.Value}");
                    }
                    Console.WriteLine($"      ✅ Joker {joker} is valid");
                    
                    if (!string.IsNullOrEmpty(desire.Edition) && desire.Edition != "None")
                    {
                        if (!Enum.TryParse<MotelyItemEdition>(desire.Edition, true, out var edition))
                        {
                            throw new ArgumentException($"Invalid edition: {desire.Edition}");
                        }
                        Console.WriteLine($"      ✅ Edition {edition} is valid");
                    }
                    break;

                case "Standard_Card":
                    Console.WriteLine($"      Card: Rank={desire.Rank}, Suit={desire.Suit}, Enhancement={desire.Enchantment}");
                    break;

                default:
                    Console.WriteLine($"      ⚠️  Unknown type: {desire.Type}");
                    break;
            }

            var antes = desire.SearchAntes ?? [desire.DesireByAnte];
            Console.WriteLine($"      Antes: [{string.Join(", ", antes)}]");
        }

        public static void TestSearch(string configPath, int maxBatches = 10)
        {
            Console.WriteLine($"🚀 Testing search with config: {configPath}");
            
            try
            {
                var config = OuijaConfig.Load(configPath, OuijaConfig.GetOptions());
                
                using var search = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(new OuijaJsonFilterDesc(config))
                    .WithThreadCount(2)
                    .WithBatchCharacterCount(2)
                    .WithStartBatchIndex(0)
                    .WithEndBatchIndex(maxBatches)
                    .Start();

                int resultCount = 0;
                var startTime = DateTime.Now;
                
                while (!search.IsCompleted && (DateTime.Now - startTime).TotalSeconds < 30)
                {
                    while (search.Results.TryDequeue(out var result))
                    {
                        if (result.Success)
                        {
                            Console.WriteLine($"   Found: {result.Seed} (Score: {result.TotalScore})");
                            resultCount++;
                            if (resultCount >= 5) break; // Just show first 5 results
                        }
                    }
                    if (resultCount >= 5) break;
                    Thread.Sleep(100);
                }

                Console.WriteLine($"✅ Test completed. Found {resultCount} results in {maxBatches} batches.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                throw;
            }
        }
    }
}
