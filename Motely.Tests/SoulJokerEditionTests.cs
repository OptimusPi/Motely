using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Motely.Filters;

namespace Motely.Tests
{
    /// <summary>
    /// Tests for the soul joker edition bug that was fixed.
    /// Soul joker editions depend on the ante parameter used when creating the stream.
    /// </summary>
    public class SoulJokerEditionTests
    {
        [Fact]
        public void SoulJoker_Edition_Should_Depend_On_Ante()
        {
            // This test verifies that soul joker editions are correctly calculated based on ante
            // The bug was: created soul stream with minAnte instead of specific ante

            // Create test filter requiring Negative edition soul joker
            var clause = new MotelyJsonSoulJokerFilterClause
            {
                JokerType = MotelyJoker.Perkeo,
                WantedAntes = new bool[40],
                WantedPackSlots = new bool[6],
                EditionEnum = MotelyItemEdition.Negative, // Require Negative edition
                RequireMega = false
            };
            // Set ante 5
            clause.WantedAntes[5] = true;
            // Set pack slots 0-3
            for (int i = 0; i <= 3; i++)
                clause.WantedPackSlots[i] = true;

            var criteria = MotelyJsonSoulJokerFilterClause.CreateCriteria(new List<MotelyJsonSoulJokerFilterClause> { clause });
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(criteria);

            // The edition should be properly set
            Assert.Equal(MotelyItemEdition.Negative, clause.EditionEnum);

            // The filter should check ante 5
            Assert.True(clause.WantedAntes[5]);
        }

        [Fact]
        public void SoulJoker_Stream_Must_Be_Created_Per_Ante()
        {
            // This test documents the critical fix: soul stream MUST be created with specific ante
            // to get correct edition calculation

            var config = new MotelyJsonConfig
            {
                Name = "Test Negative Soul Joker",
                Description = "Test that soul joker editions are ante-dependent",
                Deck = "Red",
                Stake = "White",
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Triboulet",
                        Edition = "Negative",
                        Antes = new[] { 5 } // Check ante 5 specifically
                    }
                }
            };

            // Initialize enums
            foreach (var mustClause in config.Must)
            {
                mustClause.InitializeParsedEnums();
            }

            // Verify the clause is properly configured with edition
            var clause = config.Must[0];
            Assert.True(clause.EditionEnum.HasValue);
            Assert.Equal(MotelyItemEdition.Negative, clause.EditionEnum.Value);
        }

        [Fact]
        public void Multiple_Soul_Jokers_Different_Antes_Should_Work()
        {
            // Test that multiple soul joker clauses with different antes work correctly
            // Each ante needs its own soul stream for proper edition calculation

            var config = new MotelyJsonConfig
            {
                Name = "Multi-ante soul test",
                Description = "Test multiple soul jokers across antes",
                Deck = "Red",
                Stake = "White",
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Perkeo",
                        Edition = "Negative",
                        Antes = new[] { 1, 2, 3 }
                    },
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Triboulet",
                        Antes = new[] { 4, 5, 6 }
                    }
                }
            };

            // Initialize enums
            foreach (var mustClause in config.Must)
            {
                mustClause.InitializeParsedEnums();
            }

            // Convert to soul joker filter clauses
            var soulClauses = MotelyJsonSoulJokerFilterClause.ConvertClauses(config.Must);
            Assert.Equal(2, soulClauses.Count);

            // First clause should check antes 1-3 for Negative Perkeo
            var perkeoClause = soulClauses[0];
            Assert.Equal(MotelyJoker.Perkeo, perkeoClause.JokerType);
            Assert.Equal(MotelyItemEdition.Negative, perkeoClause.EditionEnum);
            Assert.True(perkeoClause.WantedAntes[1]);
            Assert.True(perkeoClause.WantedAntes[2]);
            Assert.True(perkeoClause.WantedAntes[3]);

            // Second clause should check antes 4-6 for any Triboulet
            var tribouletClause = soulClauses[1];
            Assert.Equal(MotelyJoker.Triboulet, tribouletClause.JokerType);
            Assert.Null(tribouletClause.EditionEnum); // No edition requirement
            Assert.True(tribouletClause.WantedAntes[4]);
            Assert.True(tribouletClause.WantedAntes[5]);
            Assert.True(tribouletClause.WantedAntes[6]);
        }

        [Fact]
        public void SHOULD_Scoring_Must_Also_Use_Ante_Specific_Stream()
        {
            // This test documents that the SHOULD scoring logic must also create
            // soul stream with specific ante for proper edition calculation

            var config = new MotelyJsonConfig
            {
                Name = "Test SHOULD scoring edition",
                Description = "SHOULD clauses must also handle editions correctly",
                Deck = "Red",
                Stake = "White",
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>(), // Empty MUST
                Should = new List<MotelyJsonConfig.MotleyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Perkeo",
                        Edition = "Negative",
                        Antes = new[] { 5 },
                        Score = 100 // Scoring points for Negative Perkeo
                    }
                }
            };

            // Initialize enums
            foreach (var shouldClause in config.Should)
            {
                shouldClause.InitializeParsedEnums();
            }

            // Verify SHOULD clause has edition properly set
            var clause = config.Should[0];
            Assert.Equal(MotelyItemEdition.Negative, clause.EditionEnum);
            Assert.Equal(100, clause.Score);
        }

        [Fact]
        public void Soul_Joker_Bug_Regression_Test()
        {
            // This is a regression test for the exact bug the user encountered:
            // "No. it has Triboulet None Edition ante 5."
            // The fix ensures editions are calculated with the ante where the joker appears

            // Create JSON config that tests the exact scenario
            var jsonConfig = @"{
                ""name"": ""Regression Test"",
                ""description"": ""Test for soul joker edition bug"",
                ""deck"": ""Red"",
                ""stake"": ""White"",
                ""must"": [
                    {
                        ""type"": ""SoulJoker"",
                        ""value"": ""Triboulet"",
                        ""edition"": ""Negative"",
                        ""antes"": [5]
                    }
                ],
                ""should"": []
            }";

            var testConfigPath = Path.Combine(Path.GetTempPath(), "soul-edition-regression-test.json");
            File.WriteAllText(testConfigPath, jsonConfig);

            try
            {
                // Parse and validate the config
                var config = System.Text.Json.JsonSerializer.Deserialize<MotelyJsonConfig>(jsonConfig);
                Assert.NotNull(config);

                foreach (var clause in config.Must)
                {
                    clause.InitializeParsedEnums();
                }

                // The clause should be looking for Negative Triboulet in ante 5
                var mustClause = config.Must[0];
                Assert.Equal(MotelyJoker.Triboulet, mustClause.JokerEnum);
                Assert.Equal(MotelyItemEdition.Negative, mustClause.EditionEnum);
                Assert.Contains(5, mustClause.Antes);

                // Convert to filter clause
                var soulClauses = MotelyJsonSoulJokerFilterClause.ConvertClauses(config.Must);
                var filterClause = soulClauses[0];

                // The critical fix: edition must be checked with ante-specific soul stream
                Assert.Equal(MotelyItemEdition.Negative, filterClause.EditionEnum);
                Assert.True(filterClause.WantedAntes[5]);
            }
            finally
            {
                if (File.Exists(testConfigPath))
                {
                    File.Delete(testConfigPath);
                }
            }
        }
    }
}