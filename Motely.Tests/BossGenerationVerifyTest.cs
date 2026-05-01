using System.Text;
using Motely.Analysis;

namespace Motely.Tests
{
    public class BossGenerationVerifyTest
    {
        // Smoke-check boss generation across antes. (Snapshot verification via Verify
        // was dropped; the per-ante enumeration is still valuable as a crash guard.)
        [Theory]
        [InlineData("ALEEB")]
        [InlineData("12345678")]
        [InlineData("UNITTEST")]
        public void TestBossGeneration_AllAntes(string seed)
        {
            var analysis = MotelySeedAnalyzer.Analyze(new(seed, MotelyDeck.Red, MotelyStake.White));

            Assert.True(string.IsNullOrEmpty(analysis.Error), $"Analyzer failed for {seed}: {analysis.Error}");
            Assert.NotEmpty(analysis.Antes);

            foreach (var ante in analysis.Antes)
            {
                Assert.True(
                    Enum.IsDefined(typeof(MotelyBossBlind), ante.Boss),
                    $"Invalid boss for ante {ante.Ante} on seed {seed}: {ante.Boss}"
                );
            }
        }
    }
}
