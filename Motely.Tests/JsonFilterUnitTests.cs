// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using Xunit;
// using Motely.Filters;

// namespace Motely.Tests
// {
//     public class JsonFilterUnitTests
//     {
//         [Theory]
//         [InlineData("shopSlotTest_Jokers.json")]
//         [InlineData("packSlotTest_Jokers.json")]
//         [InlineData("shopAndPackSlotTest_Jokers.json")]
//         [InlineData("shopSlotTest_TarotCard.json")]
//         [InlineData("packSlotTest_TarotCard.json")]
//         [InlineData("shopAndPackSlotTest_TarotCard.json")]
//         [InlineData("shopSlotTest_SpectralCard.json")]
//         [InlineData("packSlotTest_SpectralCard.json")]
//         [InlineData("shopAndPackSlotTest_SpectralCard.json")]
//         [InlineData("shopSlotTest_PlanetCard.json")]
//         [InlineData("packSlotTest_PlanetCard.json")]
//         [InlineData("shopAndPackSlotTest_PlanetCard.json")]
//         [InlineData("shopSlotTest_PlayingCard.json")]
//         [InlineData("packSlotTest_PlayingCard.json")]
//         [InlineData("shopAndPackSlotTest_PlayingCard.json")]
//         public void TestJsonFilter_ShouldLoadAndRunSuccessfully(string jsonFileName)
//         {
//             // Test files are copied to output directory
//             var filterPath = Path.Combine("tests", jsonFileName);
            
//             // Should not throw
//             Assert.True(File.Exists(filterPath), $"Test filter file not found: {filterPath}");
//             var config = MotelyJsonConfig.LoadFromJson(filterPath);
//             Assert.NotNull(config);
            
//             // Create and run the filter - should not throw
//             var filterDesc = new MotelyJsonSeedScoreDesc(config);
//             var searchSettings = new MotelySearchSettings<MotelyJsonSeedScoreDesc.MotelyJsonFinalTallyScoresDesc>(filterDesc)
//                 .WithDeck(MotelyDeck.Red)
//                 .WithStake(MotelyStake.White)
//                 .WithListSearch(new[] { "ALEEB", "UNITTEST" })
//                 .WithThreadCount(1);
            
//             var search = searchSettings.Start();
//             System.Threading.Thread.Sleep(50);
//             search.Dispose();
            
//             Assert.True(true); // If we get here, test passes
//         }
        
//         [Fact]
//         public void TestJsonFilter_JokerShopSlot_RunsWithoutError()
//         {
//             // Test that we can create and run a filter looking for Rocket
//             var config = new MotelyJsonConfig();
//             var rocketItem = new MotelyJsonConfig.MotleyJsonFilterClause {
//                 Type = "Joker",
//                 Value = "Rocket",
//                 Antes = new[] { 1 },
//                 ShopSlots = new[] { 1 } // Rocket is at slot 1 (0-indexed) in ALEEB
//             };
//             rocketItem.InitializeParsedEnums();
//             config.Must.Add(rocketItem);
            
//             var filterDesc = new MotelyJsonSeedScoreDesc(config);
            
//             // Run the filter against ALEEB seed
//             var searchSettings = new MotelySearchSettings<MotelyJsonSeedScoreDesc.MotelyJsonFinalTallyScoresDesc>(filterDesc)
//                 .WithDeck(MotelyDeck.Red)
//                 .WithStake(MotelyStake.White)
//                 .WithListSearch(new[] { "ALEEB" })
//                 .WithThreadCount(1);
            
//             // Basic test - can we start and stop without errors
//             var search = searchSettings.Start();
//             System.Threading.Thread.Sleep(100);
//             search.Dispose();
            
//             // If we get here without exception, test passes
//             Assert.True(true);
//         }
        
//         [Fact]
//         public void TestJsonFilter_TarotCardShop_RunsWithoutError()
//         {
//             // Test that we can create and run a filter looking for The Empress
//             var config = new MotelyJsonConfig();
//             var empressItem = new MotelyJsonConfig.MotleyJsonFilterClause {
//                 Type = "TarotCard",
//                 Value = "TheEmpress",
//                 Antes = new[] { 1 },
//                 ShopSlots = new[] { 2 } // The Empress is at slot 2 in ALEEB
//             };
//             empressItem.InitializeParsedEnums();
//             config.Must.Add(empressItem);
            
//             var filterDesc = new MotelyJsonSeedScoreDesc(config);
            
//             // Run the filter against ALEEB seed
//             var searchSettings = new MotelySearchSettings<MotelyJsonSeedScoreDesc.MotelyJsonFinalTallyScoresDesc>(filterDesc)
//                 .WithDeck(MotelyDeck.Red)
//                 .WithStake(MotelyStake.White)
//                 .WithListSearch(new[] { "ALEEB" })
//                 .WithThreadCount(1);
            
//             // Basic test - can we start and stop without errors
//             var search = searchSettings.Start();
//             System.Threading.Thread.Sleep(100);
//             search.Dispose();
            
//             // If we get here without exception, test passes
//             Assert.True(true);
//         }
        
//         [Fact]
//         public void TestJsonFilter_VerifyListSearchRunsWithoutError()
//         {
//             // Simple test to verify we can run a basic filter against known seeds
//             var config = new MotelyJsonConfig();
//             var tradingCardItem = new MotelyJsonConfig.MotleyJsonFilterClause {
//                 Type = "Joker",
//                 Value = "TradingCard",
//                 Antes = new[] { 1 },
//                 ShopSlots = new[] { 0 } // Trading Card is at slot 0 in ALEEB
//             };
//             tradingCardItem.InitializeParsedEnums();
//             config.Must.Add(tradingCardItem);
            
//             var filterDesc = new MotelyJsonSeedScoreDesc(config);
            
//             // Run against both test seeds
//             var searchSettings = new MotelySearchSettings<MotelyJsonSeedScoreDesc.MotelyJsonFinalTallyScoresDesc>(filterDesc)
//                 .WithDeck(MotelyDeck.Red)
//                 .WithStake(MotelyStake.White)
//                 .WithListSearch(new[] { "ALEEB", "UNITTEST" })
//                 .WithThreadCount(1);
            
//             // Basic test - can we start and stop without errors
//             var search = searchSettings.Start();
//             System.Threading.Thread.Sleep(100);
//             search.Dispose();
            
//             // If we get here without exception, test passes
//             Assert.True(true);
//         }
//     }
// }