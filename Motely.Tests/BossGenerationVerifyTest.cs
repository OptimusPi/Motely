using System.Text;
using System.Threading.Tasks;
using Xunit;
using VerifyXunit;
using Motely;
using Motely.Analysis;

namespace Motely.Tests
{
    public class BossGenerationVerifyTest
    {
        [Theory]
        [InlineData("ALEEB")]
        [InlineData("12345678")]
        [InlineData("UNITTEST")]
        public async Task TestBossGeneration_AllAntes(string seed)
        {
            // Use analyzer to get boss info
            var analysis = MotelySeedAnalyzer.Analyze(new(seed, MotelyDeck.Red, MotelyStake.White));
            
            var output = new StringBuilder();
            output.AppendLine($"Boss generation for seed: {seed}");
            output.AppendLine("========================");
            
            // Extract just the boss for each ante
            foreach (var ante in analysis.Antes)
            {
                output.AppendLine($"Ante {ante.Ante}: {ante.Boss}");
            }
            
            // Verify
            await Verify(output.ToString())
                .UseParameters(seed)
                .UseFileName($"boss_generation_{seed}");
        }
    }
}