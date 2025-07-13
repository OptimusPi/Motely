using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Motely;
using Motely.Filters;

namespace Motely
{
    public class TestRunner
    {
        private readonly List<TestConfig> _tests = new();
        
        public class TestConfig
        {
            public string Name { get; set; } = "";
            public string FileName { get; set; } = "";
            public string Description { get; set; } = "";
        }
        
        public TestRunner()
        {
            LoadTests();
        }
        
        private void LoadTests()
        {
            _tests.Clear();
            
            // Add all test configs
            _tests.Add(new TestConfig 
            { 
                Name = "Simple Perkeo Test",
                FileName = "perkeo_simple.ouija.json",
                Description = "Find Perkeo soul joker in ante 1-2"
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Boss Blind Test",
                FileName = "boss_test.ouija.json",
                Description = "Find The Needle in ante 1, The Plant in antes 2-4"
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Playing Card Test",
                FileName = "playing_card_test.ouija.json",
                Description = "Find Ace of Hearts with Red Seal, any Lucky card"
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Joker Sticker Test",
                FileName = "sticker_test.ouija.json",
                Description = "Find Blueprint with Eternal sticker"
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Complete Feature Test",
                FileName = "complete_test.ouija.json",
                Description = "Test all features: Boss, Tags, Soul Jokers, Cards, etc."
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Lucky Cat Test",
                FileName = "lucky_cat.ouija.json",
                Description = "Simple Lucky Cat joker search"
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Negative Chicot Test",
                FileName = "negative_chicot_test.ouija.json",
                Description = "Find Negative edition Chicot"
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Deck & Stake Test",
                FileName = "deck_stake_test.ouija.json",
                Description = "Test specific deck and stake combinations"
            });
            
            _tests.Add(new TestConfig 
            { 
                Name = "Showman Test",
                FileName = "showman_test.ouija.json",
                Description = "Find Showman joker"
            });
        }
        
        public void Run()
        {
            while (true)
            {
                Console.Clear();
                PrintHeader();
                PrintMenu();
                
                var key = Console.ReadKey(true);
                
                if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine("\n👋 Goodbye!");
                    break;
                }
                
                if (char.IsDigit(key.KeyChar))
                {
                    int index = key.KeyChar - '1';
                    if (index >= 0 && index < _tests.Count)
                    {
                        RunTest(_tests[index]);
                    }
                }
                else if (key.Key == ConsoleKey.A)
                {
                    RunAllTests();
                }
                else if (key.Key == ConsoleKey.R)
                {
                    LoadTests(); // Reload in case files changed
                    Console.WriteLine("\n🔄 Tests reloaded!");
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }
        
        private void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║           🃏 MOTELY TEST RUNNER - OUIJA CONFIGS 🃏           ║
╚══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
        }
        
        private void PrintMenu()
        {
            Console.WriteLine("\nSelect a test to run:\n");
            
            for (int i = 0; i < _tests.Count && i < 9; i++)
            {
                var test = _tests[i];
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"  [{i + 1}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{test.Name,-25}");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($" - {test.Description}");
            }
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [A] Run ALL tests");
            Console.WriteLine("  [R] Reload test list");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  [Q] Quit");
            Console.ResetColor();
            
            Console.Write("\nYour choice: ");
        }
        
        private void RunTest(TestConfig test)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n🎯 Running: {test.Name}");
            Console.WriteLine(new string('=', 60));
            Console.ResetColor();
            
            try
            {
                string configPath = Path.Combine("JsonItemFilters", test.FileName);
                
                if (!File.Exists(configPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ Config file not found: {configPath}");
                    Console.ResetColor();
                    WaitForKey();
                    return;
                }
                
                // Load the config
                var config = OuijaConfig.LoadFromJson(configPath);
                
                // Print config summary
                PrintConfigSummary(config);
                
                // Run the actual search using the existing search infrastructure
                Console.WriteLine("\n🔍 Starting search...\n");
                
                // Use the main program's search function
                Program.RunOuijaSearch(
                    configPath,     // config path
                    0,              // start batch
                    10,             // end batch (small for testing)
                    4,              // threads
                    4,              // batch size
                    1,              // cutoff
                    false,          // debug
                    false,          // quiet
                    null,           // wordlist
                    null,           // keyword
                    false           // nofancy
                );
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✅ Test completed!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.ResetColor();
            }
            
            WaitForKey();
        }
        
        private void RunAllTests()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n🚀 Running ALL tests...");
            Console.WriteLine(new string('=', 60));
            Console.ResetColor();
            
            int passed = 0;
            int failed = 0;
            
            foreach (var test in _tests)
            {
                Console.Write($"\n{test.Name,-30} ");
                
                try
                {
                    string configPath = Path.Combine("JsonItemFilters", test.FileName);
                    
                    if (!File.Exists(configPath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[MISSING]");
                        failed++;
                        continue;
                    }
                    
                    // Just validate the config loads for "run all" mode
                    var config = OuijaConfig.LoadFromJson(configPath);
                    
                    // Quick validation
                    config.Validate();
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[PASS]");
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] {ex.Message}");
                    failed++;
                }
                finally
                {
                    Console.ResetColor();
                }
            }
            
            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"Total: {_tests.Count} | ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"Passed: {passed}");
            Console.ResetColor();
            Console.Write(" | ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed: {failed}");
            Console.ResetColor();
            
            WaitForKey();
        }
        
        private void PrintConfigSummary(OuijaConfig config)
        {
            Console.WriteLine($"\n📋 Config Summary:");
            Console.WriteLine($"  Deck: {config.Deck}");
            Console.WriteLine($"  Stake: {config.Stake}");
            Console.WriteLine($"  Max Ante: {config.MaxSearchAnte}");
            Console.WriteLine($"  Needs: {config.NumNeeds}");
            Console.WriteLine($"  Wants: {config.NumWants}");
            
            if (config.Needs?.Length > 0)
            {
                Console.WriteLine("\n  Required Items:");
                foreach (var need in config.Needs)
                {
                    Console.WriteLine($"    - {need.Type}: {need.GetDisplayString()}");
                }
            }
            
            if (config.Wants?.Length > 0)
            {
                Console.WriteLine("\n  Optional Items:");
                foreach (var want in config.Wants.Take(5)) // Show first 5
                {
                    Console.WriteLine($"    - {want.Type}: {want.GetDisplayString()} (Score: {want.Score})");
                }
                if (config.Wants.Length > 5)
                    Console.WriteLine($"    ... and {config.Wants.Length - 5} more");
            }
        }
        
        private void WaitForKey()
        {
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }
    }
    
    // Update Program.cs to include test runner option
    public partial class Program
    {
        public static void RunTestMenu()
        {
            Console.Title = "Motely Test Runner";
            
            var runner = new TestRunner();
            runner.Run();
        }
    }
}
