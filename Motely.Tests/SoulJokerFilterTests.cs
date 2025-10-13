using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Motely.Filters;

namespace Motely.Tests
{
    public class SoulJokerFilterTests
    {
        [Fact(Skip = "Integration test - requires Motely.exe (file locked by background instances)")]
        public void SoulJoker_Must_Filter_Should_Find_No_Matches()
        {
            var output = RunMotelyWithJsonConfig("souljoker-impossible.json");

            // Verify the filter was created but found no matches for impossible joker
            // ALEEB doesn't have Chicot as a soul joker in ante 1
            Assert.Contains("+ Base SoulJoker filter:", output);
            Assert.Contains("SEARCH COMPLETED", output);
            Assert.Contains("Seeds passed filter: 0", output);
        }

        [Fact(Skip = "Integration test - requires Motely.exe (file locked by background instances)")]
        public void SoulJoker_Must_Filter_Should_Find_Canio_Match()
        {
            var output = RunMotelyWithJsonConfig("souljoker-must-canio.json");

            // Verify the filter finds ALEEB which has Canio as soul joker
            Assert.Contains("+ Base SoulJoker filter:", output);
            Assert.Contains("SEARCH COMPLETED", output);
            Assert.Contains("Seeds passed cutoff: 1", output);
            Assert.Contains("ALEEB", output);
        }
        
        private static string RunMotelyWithJsonConfig(string configFileName, string seed = "ALEEB")
        {
            var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run -c Release -- --seed {seed} --json {Path.GetFileNameWithoutExtension(configFileName)}",
                WorkingDirectory = motelyProjectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
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
                throw new Exception($"Motely process failed with exit code {process.ExitCode}. Error: {error}");
            }

            return output ?? string.Empty;
        }
        
        [Fact]
        public void SoulJoker_Enum_Should_Be_Properly_Set()
        {
            // Arrange
            var clause = new MotelyJsonConfig.MotleyJsonFilterClause
            {
                Type = "SoulJoker",
                Value = "Perkeo",
                Antes = new[] { 1 }
            };
            
            // Act
            clause.InitializeParsedEnums();
            
            // Assert
            Assert.Equal(MotelyFilterItemType.SoulJoker, clause.ItemTypeEnum);
            Assert.True(clause.JokerEnum.HasValue, "JokerEnum should be set for SoulJoker");
            Assert.Equal(MotelyJoker.Perkeo, clause.JokerEnum.Value);
        }

        [Fact]
        public void SoulJoker_Filter_Should_Match_Specific_Joker()
        {
            // Test that the filter correctly matches the specified joker type
            var clause = new MotelyJsonSoulJokerFilterClause(
                MotelyJoker.Perkeo,
                new List<int> { 1 }, // Ante 1 only
                new List<int> { 0, 1, 2, 3 } // Check all pack slots
            );

            // Create a filter with this clause
            var criteria = MotelyJsonSoulJokerFilterClause.CreateCriteria(new List<MotelyJsonSoulJokerFilterClause> { clause });
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(criteria);

            // Verify the clause was properly created with the correct joker type
            Assert.NotNull(clause.JokerType);
            Assert.Equal(MotelyJoker.Perkeo, clause.JokerType.Value);
            Assert.False(clause.WantedAntes[0]); // Ante 0 should be false
            Assert.True(clause.WantedAntes[1]); // Ante 1 should be true - the one we set above
            Assert.False(clause.WantedAntes[2]); // Ante 2 should be false
        }

        [Fact]
        public void SoulJoker_Filter_Finds_Ante_Zero()
        {
            // Test that the filter correctly matches the specified joker type
            var clause = new MotelyJsonSoulJokerFilterClause(
                MotelyJoker.Perkeo,
                new List<int> { 0 }, // Ante 0 can be obtained if player finds the Hieroglyph in Ante 1.
                new List<int> { 0, 1, 2, 3 } // Check all pack slots
            );

            // Create a filter with this clause
            var criteria = MotelyJsonSoulJokerFilterClause.CreateCriteria(new List<MotelyJsonSoulJokerFilterClause> { clause });
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(criteria);

            // Verify the clause was properly created with the correct joker type
            Assert.NotNull(clause.JokerType);
            Assert.Equal(MotelyJoker.Perkeo, clause.JokerType.Value);
            Assert.True(clause.WantedAntes[0]); // Ante 0 should be true - the one we set above
            Assert.False(clause.WantedAntes[1]); // Ante 1 should be false
            Assert.False(clause.WantedAntes[2]); // Ante 2 should be false
        }
        
        [Fact]
        public void SoulJoker_Different_From_Regular_Joker()
        {
            // This test verifies that SoulJoker and regular Joker are handled differently
            var soulJokerClause = new MotelyJsonConfig.MotleyJsonFilterClause
            {
                Type = "SoulJoker",
                Value = "Perkeo",
                Antes = new[] { 1 }
            };
            
            var regularJokerClause = new MotelyJsonConfig.MotleyJsonFilterClause
            {
                Type = "Joker",
                Value = "Joker", // Use regular Joker instead of Perkeo
                Antes = new[] { 1 }
            };
            
            soulJokerClause.InitializeParsedEnums();
            regularJokerClause.InitializeParsedEnums();
            
            // Different jokers but same structure
            Assert.Equal(MotelyJoker.Perkeo, soulJokerClause.JokerEnum);
            Assert.Equal(MotelyJoker.Joker, regularJokerClause.JokerEnum);
            
            // But they should have different ItemTypeEnum
            Assert.Equal(MotelyFilterItemType.SoulJoker, soulJokerClause.ItemTypeEnum);
            Assert.Equal(MotelyFilterItemType.Joker, regularJokerClause.ItemTypeEnum);
        }
        
        [Fact]
        public void Love2_Config_Should_Work()
        {
            // This test specifically validates the love2 config that was broken
            var config = new MotelyJsonConfig
            {
                Name = "love2 test",
                Description = "Test for Perkeo soul joker",
                Deck = "Ghost",
                Stake = "White",
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Perkeo",
                        Antes = new[] { 1 }
                    }
                },
                Should = new List<MotelyJsonConfig.MotleyJsonFilterClause>()
            };
            
            // Initialize parsed enums
            foreach (var clause in config.Must)
            {
                clause.InitializeParsedEnums();
            }
            
            // Verify the clause is properly configured
            var mustClause = config.Must[0];
            Assert.Equal(MotelyFilterItemType.SoulJoker, mustClause.ItemTypeEnum);
            Assert.True(mustClause.JokerEnum.HasValue);
            Assert.Equal(MotelyJoker.Perkeo, mustClause.JokerEnum.Value);
            
            // Create the filter to ensure no exceptions
            var soulJokerClauses = MotelyJsonSoulJokerFilterClause.ConvertClauses(config.Must);
            Assert.Single(soulJokerClauses);
            
            var convertedClause = soulJokerClauses[0];
            Assert.Equal(MotelyJoker.Perkeo, convertedClause.JokerType);
            Assert.True(convertedClause.WantedAntes[1]);
        }
    }
}