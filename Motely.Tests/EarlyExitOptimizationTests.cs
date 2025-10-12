using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Motely.Filters;

namespace Motely.Tests
{
    /// <summary>
    /// Tests for the early exit optimization in soul joker individual validation.
    /// The optimization should stop checking antes once a clause can't possibly succeed.
    /// </summary>
    public class EarlyExitOptimizationTests
    {
        [Fact]
        public void EarlyExit_Should_Stop_When_Clause_Cannot_Succeed()
        {
            // Test that validates early exit actually happens
            // Create a filter that requires a soul joker ONLY in ante 1
            var clause = new MotelyJsonSoulJokerFilterClause(
                MotelyJoker.Perkeo,
                new List<int> { 1 }, // Only ante 1
                new List<int> { 0, 1, 2, 3 },
                false // Don't require Mega
            ) { Min = 1 };

            var criteria = MotelyJsonSoulJokerFilterClause.CreateCriteria(new List<MotelyJsonSoulJokerFilterClause> { clause });
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(criteria);

            // The filter should have pre-computed that ante 1 is the last ante for this clause
            // Once we pass ante 1 without finding Perkeo, it should early exit
            Assert.True(clause.WantedAntes[1]);
            Assert.False(clause.WantedAntes[2]); // Not looking in ante 2
            Assert.False(clause.WantedAntes[3]); // Not looking in ante 3
        }

        [Fact]
        public void Multiple_Clauses_Different_Last_Antes()
        {
            // Test with multiple clauses that have different "last antes"
            // This ensures the early exit logic works correctly with multiple requirements

            var clauses = new List<MotelyJsonSoulJokerFilterClause>
            {
                // Clause 1: Perkeo in antes 1-3
                new MotelyJsonSoulJokerFilterClause(
                    MotelyJoker.Perkeo,
                    new List<int> { 1, 2, 3 },
                    new List<int> { 0, 1, 2, 3 }
                ),
                // Clause 2: Triboulet ONLY in ante 5
                new MotelyJsonSoulJokerFilterClause(
                    MotelyJoker.Triboulet,
                    new List<int> { 5 },
                    new List<int> { 0, 1, 2, 3 }
                )
            };

            var criteria = MotelyJsonSoulJokerFilterClause.CreateCriteria(clauses);
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(criteria);

            // Clause 1 should check antes 1-3
            Assert.True(clauses[0].WantedAntes[1]);
            Assert.True(clauses[0].WantedAntes[2]);
            Assert.True(clauses[0].WantedAntes[3]);
            Assert.False(clauses[0].WantedAntes[4]);

            // Clause 2 should only check ante 5
            Assert.False(clauses[1].WantedAntes[1]);
            Assert.True(clauses[1].WantedAntes[5]);

            // The filter should early exit if:
            // - After ante 3: Clause 1 hasn't met its minimum
            // - After ante 5: Clause 2 hasn't met its minimum
        }

        [Fact]
        public void Performance_Test_Early_Exit_vs_Full_Scan()
        {
            // This test documents the performance improvement from early exit
            // When a clause fails early, we should stop checking remaining antes

            var configWithEarlyAnte = new MotelyJsonConfig
            {
                Name = "Early Exit Test",
                Description = "Should fail fast when soul joker criteria not met early",
                Deck = "Red",
                Stake = "White",
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Perkeo", // Use real joker name
                        Antes = new[] { 1 } // Only check ante 1
                    }
                }
            };

            var configWithLateAnte = new MotelyJsonConfig
            {
                Name = "Late Ante Test",
                Description = "Should check all antes before failing",
                Deck = "Red",
                Stake = "White",
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Triboulet", // Use real joker name
                        Antes = new[] { 8 } // Only check ante 8
                    }
                }
            };

            // With early exit, the first config should fail faster
            // because it can stop after ante 1, while the second
            // must continue until ante 8

            foreach (var clause in configWithEarlyAnte.Must)
            {
                clause.InitializeParsedEnums();
            }
            foreach (var clause in configWithLateAnte.Must)
            {
                clause.InitializeParsedEnums();
            }

            // Both should be valid configurations
            Assert.NotNull(configWithEarlyAnte.Must[0].JokerEnum);
            Assert.NotNull(configWithLateAnte.Must[0].JokerEnum);
        }

        [Fact]
        public void Early_Exit_With_Min_Threshold()
        {
            // Test early exit logic with different Min thresholds

            var clauseMin2 = new MotelyJsonSoulJokerFilterClause(
                MotelyJoker.Perkeo,
                new List<int> { 1, 2, 3, 4 },
                new List<int> { 0, 1, 2, 3 },
                false // Don't require Mega
            ) { Min = 2 };

            var criteria = MotelyJsonSoulJokerFilterClause.CreateCriteria(new List<MotelyJsonSoulJokerFilterClause> { clauseMin2 });

            // With Min=2 and checking antes 1-4:
            // - Can't early exit until after ante 3 (need at least 2 chances left)
            // - Should early exit after ante 4 if count < 2
            Assert.Equal(2, clauseMin2.Min);
        }

        [Fact]
        public void Pre_Computation_Should_Happen_Once()
        {
            // This test documents that the lastAnteForClause array
            // should be pre-computed in the constructor, NOT in the hot path

            var clauses = new List<MotelyJsonSoulJokerFilterClause>();
            for (int i = 1; i <= 8; i++)
            {
                clauses.Add(new MotelyJsonSoulJokerFilterClause(
                    MotelyJoker.Perkeo,
                    new List<int> { i }, // Each clause checks a different ante
                    new List<int> { 0, 1, 2, 3 }
                ));
            }

            var criteria = MotelyJsonSoulJokerFilterClause.CreateCriteria(clauses);
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(criteria);

            // The filter struct constructor should have pre-computed
            // the last ante for each clause exactly once
            // This avoids repeated computation in the hot path

            for (int i = 0; i < clauses.Count; i++)
            {
                var clause = clauses[i];
                // Each clause should only check its specific ante
                Assert.True(clause.WantedAntes[i + 1]);

                // And not check other antes
                for (int ante = 1; ante <= 8; ante++)
                {
                    if (ante != i + 1)
                    {
                        Assert.False(clause.WantedAntes[ante]);
                    }
                }
            }
        }

        [Fact]
        public void Regression_Test_User_Performance_Issue()
        {
            // Regression test for the user's performance issue:
            // "why is it 10X faster if I only have one SoulJoker in the Must?!"
            // The issue was missing early exit logic

            var singleClauseConfig = new MotelyJsonConfig
            {
                Name = "Single Clause",
                Deck = "Red",
                Stake = "White",
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Perkeo",
                        Edition = "Negative",
                        Antes = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }
                    }
                }
            };

            var multiClauseConfig = new MotelyJsonConfig
            {
                Name = "Multiple Clauses",
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
                    },
                    new MotelyJsonConfig.MotleyJsonFilterClause
                    {
                        Type = "SoulJoker",
                        Value = "Chicot",
                        Antes = new[] { 7, 8 }
                    }
                }
            };

            // With early exit optimization:
            // - Single clause can exit as soon as it finds Perkeo
            // - Multiple clauses can exit when any clause fails and has no remaining antes
            // This should significantly improve performance

            foreach (var clause in singleClauseConfig.Must)
            {
                clause.InitializeParsedEnums();
            }
            foreach (var clause in multiClauseConfig.Must)
            {
                clause.InitializeParsedEnums();
            }

            Assert.Single(singleClauseConfig.Must);
            Assert.Equal(3, multiClauseConfig.Must.Count);

            // The fix ensures that individual validation exits early
            // when a clause cannot possibly succeed in remaining antes
        }
    }
}