using Xunit;
using System.Collections.Generic;
using BalatroSeedOracle.ViewModels;
using System.Reflection;

namespace Motely.Tests
{
    public class DeterministicShufflerTests
    {
        // Helper to invoke private method via reflection since logic is in ViewModel
        private string InvokeGetDeterministicSeed(string gameId, List<string> seeds, int dayIndex)
        {
            // Create a dummy VM (dependencies can be null as we don't access them in this method)
            // We use reflection to create instance without DI container
            var vm = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(DailyRitualWidgetViewModel)) as DailyRitualWidgetViewModel;
            
            var method = typeof(DailyRitualWidgetViewModel).GetMethod("GetDeterministicSeed", BindingFlags.NonPublic | BindingFlags.Instance);
            return (string)method.Invoke(vm, new object[] { gameId, seeds, dayIndex });
        }

        [Fact]
        public void TestDeterministicShuffle_Consistency()
        {
            var seeds = new List<string> { "ALEEB", "ALEEC", "HALEEG", "ZEBRA", "ALPHA" };
            string gameId = "THEDAILYWEE";

            // Run 1: Get Day 0
            string day0_run1 = InvokeGetDeterministicSeed(gameId, seeds, 0);
            
            // Run 2: Get Day 0 (should be identical)
            string day0_run2 = InvokeGetDeterministicSeed(gameId, seeds, 0);

            Assert.Equal(day0_run1, day0_run2);
            
            // Verify sequence is unique (not just picking index 0)
            // "THEDAILYWEE" sum = 84+72+69+68+65+73+76+89+87+69+69 = 821
            // Day 0: (821 + 0) % 5 = 821 % 5 = 1 -> Index 1 is "ALEEC" (since list is sorted: ALEEB, ALEEC, ALPHA, HALEEG, ZEBRA)
            // Wait, input list wasn't sorted in test setup, let's sort it manually to match logic expectation
            seeds.Sort(); // ALEEB, ALEEC, ALPHA, HALEEG, ZEBRA
            
            // Re-run with sorted list
            string result = InvokeGetDeterministicSeed(gameId, seeds, 0);
            
            // Expected: (821 % 5) = 1. List[1] is ALEEC.
            Assert.Equal("ALEEC", result);
        }

        [Fact]
        public void TestDeterministicShuffle_NoRepeatsInCycle()
        {
            var seeds = new List<string> { "A", "B", "C" };
            string gameId = "TEST";
            
            var drawn = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                string seed = InvokeGetDeterministicSeed(gameId, seeds, i);
                Assert.DoesNotContain(seed, drawn);
                drawn.Add(seed);
            }
            
            Assert.Equal(3, drawn.Count); // Should have picked all 3 unique seeds
        }
    }
}
