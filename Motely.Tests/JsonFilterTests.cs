using System.Text.Json;
using Motely.Filters;

namespace Motely.Tests;

public sealed class JsonFilterTests
{
    [Fact]
    public void JsonConfig_CanDeserialize()
    {
        var config = new MotelyJsonConfig
        {
            Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>(),
            Should = new List<MotelyJsonConfig.MotleyJsonFilterClause>(),
            MustNot = new List<MotelyJsonConfig.MotleyJsonFilterClause>()
        };
        
        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<MotelyJsonConfig>(json);
        
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Must);
        Assert.NotNull(deserialized.Should);
        Assert.NotNull(deserialized.MustNot);
    }
    
    [Fact]
    public void MotelySeedScoreTally_NoAllocation()
    {
        var tally = new MotelySeedScoreTally("TEST", 100);
        tally.AddTally(1);
        tally.AddTally(2);
        tally.AddTally(3);
        
        Assert.Equal("TEST", tally.Seed);
        Assert.Equal(100, tally.Score);
        Assert.Equal(3, tally.TallyCount);
        Assert.Equal(1, tally.GetTally(0));
        Assert.Equal(2, tally.GetTally(1));
        Assert.Equal(3, tally.GetTally(2));
    }
    
    [Fact]
    public void MotelySeedScoreTally_TallyColumnsProperty()
    {
        var tally = new MotelySeedScoreTally("TEST", 50);
        tally.AddTally(10);
        tally.AddTally(20);
        tally.AddTally(30);
        
        var columns = tally.TallyColumns;
        Assert.NotNull(columns);
        Assert.Equal(3, columns.Count);
        Assert.Equal(10, columns[0]);
        Assert.Equal(20, columns[1]);
        Assert.Equal(30, columns[2]);
    }
    
    [Fact]
    public void MotelySeedScoreTally_MaxCapacity()
    {
        var tally = new MotelySeedScoreTally("TEST", 0);
        
        for (int i = 0; i < 40; i++)
        {
            tally.AddTally(i);
        }
        
        Assert.Equal(32, tally.TallyCount);
        Assert.Equal(0, tally.GetTally(0));
        Assert.Equal(31, tally.GetTally(31));
        Assert.Equal(0, tally.GetTally(32));
    }
}