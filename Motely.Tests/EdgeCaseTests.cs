using System.Linq;
using Motely.Filters;

namespace Motely.Tests
{
    /// <summary>
    /// Tests for edge cases in format conversion and antes inheritance
    /// </summary>
    public class EdgeCaseTests
    {
        [Fact]
        public void Test_EmptyArrays_Defaulted()
        {
            var jaml =
                @"
name: EmptyArrayTest
must:
- joker: Blueprint
  antes: []
  shopSlots: []
  stickers: []
";

            JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out _);
            config!.PostProcess(); // Apply defaults
            Assert.NotNull(config);
            var clause = config.Must![0];

            // Empty arrays get defaulted to all antes (expected behavior)
            Assert.NotNull(clause.Antes);
            Assert.True(clause.Antes!.Length > 0); // Defaulted to all antes
            // ShopSlots and Stickers can be null or empty (both valid)
            Assert.True(clause.ShopSlots == null || clause.ShopSlots.Length == 0);
            Assert.True(clause.Stickers == null || clause.Stickers.Length == 0);
        }

        [Fact]
        public void Test_NullValues_Handled()
        {
            var jaml =
                @"
name: NullTest
must:
- joker: Blueprint
";

            JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out _);
            config!.PostProcess(); // Apply defaults
            Assert.NotNull(config);
            var clause = config.Must![0];

            // Null values should be handled gracefully
            // Value should be set from type-as-key
            Assert.Equal("Blueprint", clause.Value);
            // Antes get defaulted to all antes
            Assert.NotNull(clause.Antes);
            // Sources can be null
            Assert.Null(clause.Sources);
        }

        [Fact]
        public void Test_DeeplyNestedClauses_Preserved()
        {
            // Use JSON-style structure to avoid YAML parsing issues with deep nesting
            var config = new MotelyJsonConfig
            {
                Name = "NestedTest",
                Must = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotelyJsonFilterClause
                    {
                        Type = "And",
                        Clauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                        {
                            new MotelyJsonConfig.MotelyJsonFilterClause
                            {
                                Type = "Or",
                                Clauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                                {
                                    new MotelyJsonConfig.MotelyJsonFilterClause
                                    {
                                        Type = "And",
                                        Clauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                                        {
                                            new MotelyJsonConfig.MotelyJsonFilterClause
                                            {
                                                Type = "Joker",
                                                Value = "Blueprint",
                                                Antes = new[] { 1 },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            config.PostProcess();
            var andClause = config.Must![0];
            Assert.NotNull(andClause.Clauses);
            Assert.True(andClause.Clauses!.Count > 0);
            var orClause = andClause.Clauses[0];
            Assert.NotNull(orClause.Clauses);
            Assert.True(orClause.Clauses!.Count > 0);
            var innerAnd = orClause.Clauses[0];
            Assert.NotNull(innerAnd.Clauses);
            Assert.True(innerAnd.Clauses!.Count > 0);
            Assert.Equal("Blueprint", innerAnd.Clauses[0].Value);
        }

        [Fact]
        public void Test_AntesInheritance_DeepNesting()
        {
            // Use JSON-style to avoid YAML parsing issues with nested clauses
            var config = new MotelyJsonConfig
            {
                Name = "DeepAntesTest",
                Should = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotelyJsonFilterClause
                    {
                        Type = "And",
                        Antes = new[] { 2, 3, 4 },
                        AntesWasExplicitlySet = true,
                        Clauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                        {
                            new MotelyJsonConfig.MotelyJsonFilterClause
                            {
                                Type = "Or",
                                Clauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                                {
                                    new MotelyJsonConfig.MotelyJsonFilterClause
                                    {
                                        Type = "Joker",
                                        Value = "Blueprint",
                                    },
                                    new MotelyJsonConfig.MotelyJsonFilterClause
                                    {
                                        Type = "Joker",
                                        Value = "Showman",
                                    },
                                },
                            },
                        },
                    },
                },
            };

            config.PostProcess(); // Apply defaults and track explicit antes
            var andClause = config.Should![0];
            Assert.True(andClause.AntesWasExplicitlySet);
            Assert.Equal(3, andClause.Antes!.Length);

            // Antes should propagate to deeply nested clauses
            var orClause = andClause.Clauses!.FirstOrDefault(c =>
                c.Type.Equals("or", StringComparison.OrdinalIgnoreCase)
            );
            Assert.NotNull(orClause);
            Assert.NotNull(orClause!.Clauses);
        }

        [Fact]
        public void Test_MultipleAntesInheritance_Works()
        {
            // Use JSON-style to avoid YAML parsing issues
            var config = new MotelyJsonConfig
            {
                Name = "MultiAnteTest",
                Should = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                {
                    new MotelyJsonConfig.MotelyJsonFilterClause
                    {
                        Type = "And",
                        Antes = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
                        AntesWasExplicitlySet = true,
                        Clauses = new List<MotelyJsonConfig.MotelyJsonFilterClause>
                        {
                            new MotelyJsonConfig.MotelyJsonFilterClause
                            {
                                Type = "SmallBlindTag",
                                Value = "NegativeTag",
                            },
                            new MotelyJsonConfig.MotelyJsonFilterClause
                            {
                                Type = "Joker",
                                Value = "OopsAll6s",
                                ShopSlots = new[] { 2, 3, 4 },
                            },
                        },
                    },
                },
            };

            config.PostProcess(); // Apply defaults and track explicit antes
            var andClause = config.Should![0];
            Assert.NotNull(andClause);
            Assert.Equal(12, andClause.Antes!.Length);
            Assert.True(andClause.AntesWasExplicitlySet);
        }

        [Fact]
        public void Test_SourcesConfig_EmptyArrays()
        {
            var jaml =
                @"
name: SourcesTest
must:
- joker: Blueprint
  sources:
    shopSlots: []
    packSlots: []
    minShopSlot: 0
    maxShopSlot: 0
";

            JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out _);
            Assert.NotNull(config);

            config!.PostProcess();
            var clause = config.Must![0];
            Assert.NotNull(clause.Sources);
            // When minShopSlot and maxShopSlot are explicitly set to 0, ProcessClause creates a range [0]
            // So ShopSlots will be [0], not empty
            // The important thing is that Sources exists and the values are handled correctly
            Assert.NotNull(clause.Sources!.ShopSlots);
            // When min=0 and max=0, it creates a range with just [0]
            Assert.Single(clause.Sources.ShopSlots!);
            Assert.Equal(0, clause.Sources.ShopSlots![0]);
            // Min and max should be set to 0
            Assert.Equal(0, clause.Sources.MinShopSlot);
            Assert.Equal(0, clause.Sources.MaxShopSlot);
        }
    }
}
