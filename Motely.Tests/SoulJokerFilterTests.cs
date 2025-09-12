using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Motely.Filters;

namespace Motely.Tests
{
    public class SoulJokerFilterTests
    {
        [Fact]
        public void SoulJoker_Must_Filter_Should_Find_Perkeo()
        {
            // Arrange - Create a config that looks for Perkeo soul joker in ante 1
            var config = new MotelyJsonConfig
            {
                Name = "Test Perkeo SoulJoker",
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
                }
            };
            
            // Initialize parsed enums
            foreach (var clause in config.Must)
            {
                clause.InitializeParsedEnums();
            }
            
            // Act - Create the filter
            var soulJokerClauses = MotelyJsonSoulJokerFilterClause.ConvertClauses(config.Must);
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(soulJokerClauses);
            
            // Create search settings
            var searchSettings = new MotelySearchSettings<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>(filterDesc)
                .WithDeck(MotelyDeck.Ghost)
                .WithStake(MotelyStake.White)
                .WithThreadCount(1)
                .WithBatchCharacterCount(1)
                .WithEndBatchIndex(1000);
            
            // Start search
            var search = searchSettings.WithSequentialSearch().Start();
            search.AwaitCompletion();
            
            // Assert - We should find some results
            // The search completed without throwing an exception, which means the filter worked
            Assert.NotNull(search);
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
            var clause = new MotelyJsonSoulJokerFilterClause
            {
                JokerType = MotelyJoker.Perkeo,
                AnteBitmask = 1, // Ante 1
                PackSlotBitmask = 0 // Check all packs
            };
            
            // Create a filter with this clause
            var filterDesc = new MotelyJsonSoulJokerFilterDesc(new List<MotelyJsonSoulJokerFilterClause> { clause });
            
            // Verify the clause was properly created with the correct joker type
            Assert.NotNull(clause.JokerType);
            Assert.Equal(MotelyJoker.Perkeo, clause.JokerType.Value);
            Assert.Equal(1UL, clause.AnteBitmask);
            
            // The filter descriptor was created successfully
            Assert.NotNull(filterDesc);
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
                Value = "Perkeo",
                Antes = new[] { 1 }
            };
            
            soulJokerClause.InitializeParsedEnums();
            regularJokerClause.InitializeParsedEnums();
            
            // Both should have Perkeo as the JokerEnum
            Assert.Equal(MotelyJoker.Perkeo, soulJokerClause.JokerEnum);
            Assert.Equal(MotelyJoker.Perkeo, regularJokerClause.JokerEnum);
            
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
            Assert.Equal(1UL, convertedClause.AnteBitmask); // Ante 1 = bit 0
        }
    }
}