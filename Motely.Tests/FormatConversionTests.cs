using System.Linq;
using System.Text.Json;
using Motely.Filters;

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
            Assert.NotNull(config.Must);
            Assert.True(config.Must.Count >= 2, $"Expected at least 2 Must clauses, got {config.Must.Count}");

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

        [Fact]
        public void Test6_CompleteRoundTrip_JsonToJamlToJsonToJaml_Lossless()
        {
            // Arrange - Start with JSON
            var originalJson = File.ReadAllText(_testConfigPath);
            var originalConfig = ConfigFormatConverter.LoadFromJsonString(originalJson);
            Assert.NotNull(originalConfig);

            // Step 1: JSON -> JAML
            var jaml1 = originalConfig!.SaveAsJaml();
            Assert.NotNull(jaml1);
            Assert.NotEmpty(jaml1);

            var config1 = ConfigFormatConverter.LoadFromJamlString(jaml1);
            Assert.NotNull(config1);
            AssertConfigsEqual(originalConfig, config1!, "JSON→JAML (Step 1)");

            // Step 2: JAML -> JSON
            var json2 = config1!.SaveAsJson();
            Assert.NotNull(json2);
            Assert.NotEmpty(json2);

            var config2 = ConfigFormatConverter.LoadFromJsonString(json2);
            Assert.NotNull(config2);
            AssertConfigsEqual(originalConfig, config2!, "JAML→JSON (Step 2)");

            // Step 3: JSON -> JAML (again)
            var jaml3 = config2!.SaveAsJaml();
            Assert.NotNull(jaml3);
            Assert.NotEmpty(jaml3);

            var config3 = ConfigFormatConverter.LoadFromJamlString(jaml3);
            Assert.NotNull(config3);
            AssertConfigsEqual(originalConfig, config3!, "JSON→JAML (Step 3)");

            // Step 4: JAML -> JSON (again)
            var json4 = config3!.SaveAsJson();
            Assert.NotNull(json4);
            Assert.NotEmpty(json4);

            var config4 = ConfigFormatConverter.LoadFromJsonString(json4);
            Assert.NotNull(config4);
            AssertConfigsEqual(originalConfig, config4!, "JAML→JSON (Step 4)");

            // Final verification: All configs should be equivalent
            AssertConfigsEqual(config1, config2, "Config1 vs Config2");
            AssertConfigsEqual(config2, config3, "Config2 vs Config3");
            AssertConfigsEqual(config3, config4, "Config3 vs Config4");
            AssertConfigsEqual(config1, config4, "Config1 vs Config4");
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

            // Deep check all Must clauses
            if (expected.Must != null && actual.Must != null)
            {
                Assert.Equal(expected.Must.Count, actual.Must.Count);
                for (int i = 0; i < expected.Must.Count; i++)
                {
                    AssertClauseEqual(expected.Must[i], actual.Must[i], $"Must[{i}]");
                }
            }

            // Deep check all Should clauses
            if (expected.Should != null && actual.Should != null)
            {
                Assert.Equal(expected.Should.Count, actual.Should.Count);
                for (int i = 0; i < expected.Should.Count; i++)
                {
                    AssertClauseEqual(expected.Should[i], actual.Should[i], $"Should[{i}]");
                }
            }

            // Deep check all MustNot clauses
            if (expected.MustNot != null && actual.MustNot != null)
            {
                Assert.Equal(expected.MustNot.Count, actual.MustNot.Count);
                for (int i = 0; i < expected.MustNot.Count; i++)
                {
                    AssertClauseEqual(expected.MustNot[i], actual.MustNot[i], $"MustNot[{i}]");
                }
            }
        }

        private void AssertClauseEqual(MotelyJsonConfig.MotelyJsonFilterClause expected, MotelyJsonConfig.MotelyJsonFilterClause actual, string path)
        {
            Assert.Equal(expected.Type, actual.Type);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Label, actual.Label);
            Assert.Equal(expected.Score, actual.Score);
            Assert.Equal(expected.Mode, actual.Mode);
            Assert.Equal(expected.Min, actual.Min);
            Assert.Equal(expected.FilterOrder, actual.FilterOrder);
            Assert.Equal(expected.Edition, actual.Edition);

            // Arrays - check both null/empty and content
            if (expected.Antes != null && expected.Antes.Length > 0)
            {
                Assert.NotNull(actual.Antes);
                Assert.Equal(expected.Antes.Length, actual.Antes.Length);
                Assert.Equal(expected.Antes, actual.Antes);
            }
            else if (expected.Antes == null || expected.Antes.Length == 0)
            {
                // Both should be null or empty
                Assert.True(actual.Antes == null || actual.Antes.Length == 0, 
                    $"{path}: Expected null/empty Antes, got {actual.Antes?.Length ?? 0} items");
            }

            if (expected.Values != null && expected.Values.Length > 0)
            {
                Assert.NotNull(actual.Values);
                Assert.Equal(expected.Values.Length, actual.Values.Length);
                Assert.Equal(expected.Values, actual.Values);
            }
            else if (expected.Values == null || expected.Values.Length == 0)
            {
                Assert.True(actual.Values == null || actual.Values.Length == 0,
                    $"{path}: Expected null/empty Values, got {actual.Values?.Length ?? 0} items");
            }

            // Stickers - important: check if expected has stickers, actual should too
            if (expected.Stickers != null && expected.Stickers.Count > 0)
            {
                Assert.NotNull(actual.Stickers);
                Assert.Equal(expected.Stickers.Count, actual.Stickers.Count);
                if (actual.Stickers != null)
                {
                    var expectedSorted = expected.Stickers.OrderBy(s => s).ToList();
                    var actualSorted = actual.Stickers.OrderBy(s => s).ToList();
                    Assert.Equal(expectedSorted, actualSorted);
                }
            }
            else if (expected.Stickers == null || expected.Stickers.Count == 0)
            {
                // If expected has no stickers, actual can be null or empty (both are valid)
                // This is fine - empty collections may be omitted in JAML
            }

            // Nested objects
            if (expected.Sources != null)
            {
                Assert.NotNull(actual.Sources);
                Assert.Equal(expected.Sources.ShopSlots?.Length ?? 0, actual.Sources.ShopSlots?.Length ?? 0);
                if (expected.Sources.ShopSlots != null && expected.Sources.ShopSlots.Length > 0)
                {
                    Assert.Equal(expected.Sources.ShopSlots, actual.Sources.ShopSlots);
                }
                Assert.Equal(expected.Sources.PackSlots?.Length ?? 0, actual.Sources.PackSlots?.Length ?? 0);
                if (expected.Sources.PackSlots != null && expected.Sources.PackSlots.Length > 0)
                {
                    Assert.Equal(expected.Sources.PackSlots, actual.Sources.PackSlots);
                }
                Assert.Equal(expected.Sources.MinShopSlot, actual.Sources.MinShopSlot);
                Assert.Equal(expected.Sources.MaxShopSlot, actual.Sources.MaxShopSlot);
            }

            // Nested clauses (And/Or)
            if (expected.Clauses != null && expected.Clauses.Count > 0)
            {
                Assert.NotNull(actual.Clauses);
                Assert.Equal(expected.Clauses.Count, actual.Clauses.Count);
                for (int i = 0; i < expected.Clauses.Count; i++)
                {
                    AssertClauseEqual(expected.Clauses[i], actual.Clauses[i], $"{path}.Clauses[{i}]");
                }
            }
        }
    }
}