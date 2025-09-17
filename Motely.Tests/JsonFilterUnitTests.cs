using System.IO;
using System.Linq;
using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Simple unit tests for JSON configuration loading - no complex filter testing
/// </summary>
public sealed class JsonFilterUnitTests
{
    [Theory]
    [InlineData("aleeb-joker-test.json", "joker", "Blueprint")]
    [InlineData("aleeb-voucher-test.json", "voucher", "Hieroglyph")]
    [InlineData("aleeb-tag-test.json", "bigblindtag", "HandyTag")]
    [InlineData("aleeb-boss-test.json", "boss", "TheArm")]
    public void JsonConfig_LoadsCorrectly_WithExpectedData(string fileName, string expectedType, string expectedValue)
    {
        // Simple test: just verify JSON files load and contain expected data
        var config = LoadTestConfig(fileName);
        
        Assert.NotNull(config.Must);
        
        if (fileName == "aleeb-boss-test.json")
        {
            // Boss test has multiple clauses, check for TheArm in the second clause
            Assert.True(config.Must.Count >= 2);
            var armClause = config.Must.FirstOrDefault(c => c.Value == expectedValue);
            Assert.NotNull(armClause);
            Assert.Equal(expectedType, armClause.Type);
            Assert.Equal(expectedValue, armClause.Value);
        }
        else
        {
            // Other tests have single clauses
            Assert.Single(config.Must);
            var clause = config.Must[0];
            Assert.Equal(expectedType, clause.Type);
            Assert.Equal(expectedValue, clause.Value);
        }
    }
    
    [Fact]
    public void JsonConfig_Comprehensive_LoadsMultipleCategories()
    {
        // Test that comprehensive config loads with multiple categories
        var config = LoadTestConfig("comprehensive-test.json");
        
        Assert.NotNull(config.Must);
        Assert.True(config.Must.Count > 1, "Comprehensive test should have multiple clauses");
        
        // Just verify we can load it - don't test complex filter logic
        Assert.NotNull(config.Name);
        Assert.NotNull(config.Description);
    }
    
    private static MotelyJsonConfig LoadTestConfig(string fileName)
    {
        var configPath = Path.Combine("TestJsonConfigs", fileName);
        
        Assert.True(File.Exists(configPath), $"Test config file not found: {configPath}");
        
        var loadSuccess = MotelyJsonConfig.TryLoadFromJsonFile(configPath, out var config, out var error);
        Assert.True(loadSuccess, $"Failed to load config {fileName}: {error}");
        Assert.NotNull(config);
        
        return config;
    }
}