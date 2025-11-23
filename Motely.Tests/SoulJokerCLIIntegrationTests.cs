using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Motely.Tests
{
    /// <summary>
    /// Integration tests that run the Motely CLI process to verify end-to-end functionality.
    /// These tests are slower than unit tests but verify the complete pipeline.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Category", "CLI")]
    public class SoulJokerCLIIntegrationTests
    {
        [Fact]
        public void SoulJoker_Must_Filter_Should_Find_No_Matches()
        {
            var output = RunMotelyWithJsonConfig("souljoker-impossible.json");

            // Verify the filter was created but found no matches for impossible joker
            // ALEEB doesn't have Chicot as a soul joker in ante 1
            Assert.Contains("+ Base SoulJoker filter:", output);
            Assert.Contains("SEARCH COMPLETED", output);
            Assert.Contains("Seeds passed filter: 0", output);
        }

        [Fact]
        public void SoulJoker_Must_Filter_Should_Find_Canio_Match()
        {
            var output = RunMotelyWithJsonConfig("souljoker-must-canio.json");

            // Verify the filter finds ALEEB which has Canio as soul joker
            Assert.Contains("+ Base SoulJoker filter:", output);
            Assert.Contains("SEARCH COMPLETED", output);
            Assert.Contains("Seeds passed cutoff: 1", output);
            Assert.Contains("ALEEB", output);
        }

        [Fact]
        public void MustNot_Fail_Config_Should_Exclude_Aleeb()
        {
            // Fail config: packSlots [1] matches ALEEB's Canio soul joker location
            var output = RunMotelyWithJsonConfig("mustnot-aleeb-fail.json", seed: "ALEEB");
            Assert.Contains("SEARCH COMPLETED", output);
            Assert.Contains("Seeds passed filter: 0", output);
            Assert.DoesNotContain("ALEEB,", output);
        }

        [Fact]
        public void MustNot_Pass_Config_Should_Include_Aleeb()
        {
            // Pass config: packSlots [2] does NOT match ALEEB's soul joker location, so ALEEB should pass
            var output = RunMotelyWithJsonConfig("mustnot-aleeb-pass.json", seed: "ALEEB");
            Assert.Contains("SEARCH COMPLETED", output);
            Assert.DoesNotContain("Seeds passed filter: 0", output); // Should not be zero
            Assert.Contains("ALEEB,", output); // ALEEB should be present
        }

        [Fact]
        public void Chicot_PackSlot_Regression_P1793QII_Should_Fail()
        {
            // REGRESSION TEST FOR CRITICAL BUG
            //
            // BUG DESCRIPTION:
            // Prior to the fix, the SoulJoker filter was incorrectly capping pack slot checks:
            // - Arcana packs: Checked only slots 0-2 (should be 0-3, total 4 slots)
            // - Celestial packs: Checked only slots 0-2 (should be 0-5, total 6 slots)
            //
            // This caused FALSE POSITIVES where seeds would pass the filter even when the
            // specified soul joker appeared OUTSIDE the requested pack slots.
            //
            // AFFECTED USER:
            // User searched for Chicot soul joker in pack slots [0,1,2] for antes [1,2,3].
            // Seed P1793QII was incorrectly marked as passing, but Chicot actually appeared
            // in pack slot 3 (outside the specified range). This went undetected for 3 weeks
            // of seed searching.
            //
            // THE FIX:
            // Updated FilterCandidates_SoulJoker_PackSlots to correctly check:
            // - Arcana packs: All 4 slots (0-3)
            // - Celestial packs: All 6 slots (0-5)
            //
            // THIS TEST:
            // Verifies that seed P1793QII correctly FAILS the filter config that specifies
            // Chicot in pack slots [0,1,2] for antes [1,2,3]. If this test fails, it means
            // the bug has regressed and the filter is not checking all pack slots correctly.
            //
            // Expected: Seeds passed filter: 0 (seed should be rejected)

            var output = RunMotelyWithJsonConfig("chicot-packslot-regression.json", seed: "P1793QII");

            // Verify the filter was created and executed
            Assert.Contains("+ Base SoulJoker filter:", output);
            Assert.Contains("SEARCH COMPLETED", output);

            // CRITICAL: This seed must FAIL the filter (0 seeds should pass)
            // If this assertion fails, the pack slot capping bug has regressed
            Assert.Contains("Seeds passed filter: 0", output);

            // Ensure the seed was NOT included in the results
            Assert.DoesNotContain("P1793QII,", output);
        }

        /// <summary>
        /// Helper method to run Motely CLI with a JSON config and return the combined stdout/stderr output.
        /// NOTE: This creates a dependency on the Motely project structure and should only be used
        /// for integration tests, not unit tests.
        /// </summary>
        private static string RunMotelyWithJsonConfig(string configFileName, string seed = "ALEEB")
        {
            var motelyProjectDir = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely")
            );

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments =
                    $"run -c CLI -- --seed {seed} --json {Path.GetFileNameWithoutExtension(configFileName)} --nofancy",
                WorkingDirectory = motelyProjectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new Exception("Failed to start Motely process");
            }

            string? output = process.StandardOutput?.ReadToEnd();
            string? error = process.StandardError?.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    $"Motely process failed with exit code {process.ExitCode}. Error: {error}"
                );
            }

            var outStr = output ?? string.Empty;
            var errStr = error ?? string.Empty;

            // Always return both stdout and stderr concatenated (may contain empty
            // strings). This simplifies assertions that may match logs written to
            // either stream.
            return outStr + "\r\n" + errStr;
        }
    }
}
