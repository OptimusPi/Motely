using Motely.DataLake;

namespace Motely.Tests;

public sealed class MotelyResultsDbTests
{
    [Fact]
    public void BuildUniqueColumnNames_DeduplicatesDuplicateAndReservedLabels()
    {
        var names = MotelyLakeSeedSink.BuildUniqueColumnNames(["Any", "Any", "score", "", "seed"]);

        Assert.Equal(["Any", "Any_2", "score_2", "tally_4", "seed_2"], names);
    }
}
