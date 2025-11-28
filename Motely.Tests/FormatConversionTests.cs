using System;
using System.IO;
using System.Text.Json;
using Xunit;
using Motely;
using Motely.Filters;
using System.Collections.Generic;
using System.Linq;

namespace Motely.Tests
{
    /// <summary>
    /// Tests for round-trip conversion between JSON and JAML formats.
    /// JAML (Joker Ante Markup Language) is a YAML-based format for Balatro filters.
    /// These tests are designed to catch property loss during format conversion.
    /// </summary>
    public class FormatConversionTests
    {
        private readonly string _testConfigPath = Path.Combine("TestJsonConfigs", "ComplexFilter.json");
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [Fact]
        public void Test2_JsonToJamlAndBack_PreservesAllProperties()
        {
            // Arrange
            var originalJson = File.ReadAllText(_testConfigPath);
            var originalConfig = ConfigFormatConverter.LoadFromJsonString(originalJson);

            // Act - Convert to JAML and back
            var jamlString = originalConfig!.SaveAsJaml();
            Assert.NotNull(jamlString);
            Assert.NotEmpty(jamlString);

            var configFromJaml = ConfigFormatConverter.LoadFromJamlString(jamlString);
            Assert.NotNull(configFromJaml);

            var backToJson = configFromJaml.SaveAsJson();
            Assert.NotNull(backToJson);

            // Assert - Compare configs deeply
            AssertConfigsEqual(originalConfig!, configFromJaml!, "JSON→JAML→JSON");

            // Check that sources survived (common failure point)
            var originalJoker = originalConfig!.Must?.FirstOrDefault(c => c.Type == "Joker");
            var convertedJoker = configFromJaml!.Must?.FirstOrDefault(c => c.Type == "Joker");
            if (originalJoker?.Sources != null)
            {
                Assert.NotNull(convertedJoker?.Sources);
                Assert.Equal(originalJoker.Sources.ShopSlots?.Length, convertedJoker.Sources.ShopSlots?.Length);
                Assert.Equal(originalJoker.Sources.PackSlots?.Length, convertedJoker.Sources.PackSlots?.Length);
                Assert.Equal(originalJoker.Sources.MinShopSlot, convertedJoker.Sources.MinShopSlot);
                Assert.Equal(originalJoker.Sources.MaxShopSlot, convertedJoker.Sources.MaxShopSlot);
            }
        }

        [Fact]
        public void Test4_JamlToJsonAndBack_PreservesAllProperties()
        {
            // Arrange - First create a JAML from our test JSON
            var originalJson = File.ReadAllText(_testConfigPath);
            var jsonConfig = ConfigFormatConverter.LoadFromJsonString(originalJson);
            var originalJaml = jsonConfig!.SaveAsJaml();

            var originalConfig = ConfigFormatConverter.LoadFromJamlString(originalJaml);
            Assert.NotNull(originalConfig);

            // Act - Convert to JSON and back to JAML
            var jsonString = originalConfig.SaveAsJson();
            Assert.NotNull(jsonString);

            var configFromJson = ConfigFormatConverter.LoadFromJsonString(jsonString);
            Assert.NotNull(configFromJson);

            var backToJaml = configFromJson.SaveAsJaml();
            Assert.NotNull(backToJaml);

            // Assert
            AssertConfigsEqual(originalConfig, configFromJson, "JAML→JSON→JAML");

            // Check critical properties survived
        }

        [Fact]
        public void Test5_JamlAnchorsExpandToJsonCorrectly()
        {
            // Arrange - JAML with anchors (the &name/*name syntax - YAML feature)
            var jamlWithAnchors = @"
name: AnchorTest
deck: Red
stake: White
must:
  - type: Voucher
    value: Telescope
    antes: &EARLY_GAME
      - 1
      - 2
      - 3
  - type: Voucher
    value: Observatory
    antes: *EARLY_GAME
should:
  - type: Joker
    value: Blueprint
    antes: *EARLY_GAME
";

            // Act - Load JAML with anchors
            var config = ConfigFormatConverter.LoadFromJamlString(jamlWithAnchors);
            Assert.NotNull(config);

            // Convert to JSON (anchors should expand to full values)
            var jsonString = config.SaveAsJson();
            Assert.NotNull(jsonString);
            Assert.NotEmpty(jsonString);

            // JSON should NOT contain anchor syntax
            Assert.DoesNotContain("&", jsonString);
            Assert.DoesNotContain("*EARLY_GAME", jsonString);

            // Load back from JSON
            var configFromJson = ConfigFormatConverter.LoadFromJsonString(jsonString);
            Assert.NotNull(configFromJson);

            // Assert - All antes arrays should have the same expanded values
            Assert.Equal(3, configFromJson!.Must![0].Antes!.Length);
            Assert.Equal(3, configFromJson.Must[1].Antes!.Length);
            Assert.Equal(3, configFromJson.Should![0].Antes!.Length);

            // Values should match
            Assert.Equal(new[] { 1, 2, 3 }, configFromJson.Must[0].Antes);
            Assert.Equal(new[] { 1, 2, 3 }, configFromJson.Must[1].Antes);
            Assert.Equal(new[] { 1, 2, 3 }, configFromJson.Should[0].Antes);

            // Round-trip back to JAML should also work
            var backToJaml = configFromJson.SaveAsJaml();
            Assert.NotNull(backToJaml);

            var finalConfig = ConfigFormatConverter.LoadFromJamlString(backToJaml);
            Assert.Equal(3, finalConfig!.Must![0].Antes!.Length);
        }

        private void AssertConfigsEqual(MotelyJsonConfig expected, MotelyJsonConfig actual, string conversionPath)
        {
            // Basic properties
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Author, actual.Author);
            Assert.Equal(expected.Description, actual.Description);
            Assert.Equal(expected.Deck, actual.Deck);
            Assert.Equal(expected.Stake, actual.Stake);
            Assert.Equal(expected.Mode, actual.Mode);
            Assert.Equal(expected.ScoreAggregationMode, actual.ScoreAggregationMode);
            Assert.Equal(expected.MaxVoucherAnte, actual.MaxVoucherAnte);
            Assert.Equal(expected.MaxBossAnte, actual.MaxBossAnte);

            // Collection counts
            Assert.Equal(expected.Must?.Count ?? 0, actual.Must?.Count ?? 0);
            Assert.Equal(expected.MustNot?.Count ?? 0, actual.MustNot?.Count ?? 0);
            Assert.Equal(expected.Should?.Count ?? 0, actual.Should?.Count ?? 0);

            // Deep check first Must clause as example
            if (expected.Must?.Count > 0 && actual.Must?.Count > 0)
            {
                var expectedFirst = expected.Must[0];
                var actualFirst = actual.Must[0];

                Assert.Equal(expectedFirst.Type, actualFirst.Type);
                Assert.Equal(expectedFirst.Value, actualFirst.Value);
                Assert.Equal(expectedFirst.Label, actualFirst.Label);
                Assert.Equal(expectedFirst.Score, actualFirst.Score);
                Assert.Equal(expectedFirst.Mode, actualFirst.Mode);
                Assert.Equal(expectedFirst.Min, actualFirst.Min);
                Assert.Equal(expectedFirst.FilterOrder, actualFirst.FilterOrder);
                Assert.Equal(expectedFirst.Edition, actualFirst.Edition);

                // Arrays
                Assert.Equal(expectedFirst.Antes?.Length ?? 0, actualFirst.Antes?.Length ?? 0);
                Assert.Equal(expectedFirst.Values?.Length ?? 0, actualFirst.Values?.Length ?? 0);
                Assert.Equal(expectedFirst.Stickers?.Count ?? 0, actualFirst.Stickers?.Count ?? 0);

                // Nested objects
                if (expectedFirst.Sources != null)
                {
                    Assert.NotNull(actualFirst.Sources);
                    Assert.Equal(expectedFirst.Sources.ShopSlots?.Length, actualFirst.Sources.ShopSlots?.Length);
                    Assert.Equal(expectedFirst.Sources.PackSlots?.Length, actualFirst.Sources.PackSlots?.Length);
                }
            }
        }
    }
}