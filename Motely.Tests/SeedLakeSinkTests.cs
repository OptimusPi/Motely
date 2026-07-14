using Motely.Data;

namespace Motely.Tests;

public sealed class SeedLakeSinkTests : IDisposable
{
    private readonly string _root = "lake-test-results";

    public SeedLakeSinkTests()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static MotelyScoredSeedResult Result(string seed, int score, params int[] tallies)
    {
        var r = new MotelyScoredSeedResult();
        r.Reset(seed, score);
        foreach (var t in tallies)
            r.AddTally(t);
        return r;
    }

    [Fact]
    public void LakePath_IsPerFilter_NotShared()
    {
        Assert.Equal(
            Path.Combine(_root, "perkeo.csv"),
            SeedLakeSink.LakePath(_root, "perkeo")
        );
        Assert.NotEqual(
            SeedLakeSink.LakePath(_root, "perkeo"),
            SeedLakeSink.LakePath(_root, "observatory")
        );
    }

    [Fact]
    public void TwoDifferentFilters_GetSeparateFiles()
    {
        using (var sink = new SeedLakeSink(_root, "perkeo"))
            sink.OnScored(Result("AAAAAAAA", 42));

        using (var sink = new SeedLakeSink(_root, "observatory"))
            sink.OnScored(Result("BBBBBBBB", 7));

        var perkeoSeeds = File.ReadAllLines(SeedLakeSink.LakePath(_root, "perkeo"));
        var observatorySeeds = File.ReadAllLines(SeedLakeSink.LakePath(_root, "observatory"));

        Assert.Equal(["AAAAAAAA"], perkeoSeeds);
        Assert.Equal(["BBBBBBBB"], observatorySeeds);
    }

    [Fact]
    public void SeedSourceProvider_ReadsOnlyItsOwnFiltersSeeds()
    {
        using (var sink = new SeedLakeSink(_root, "perkeo"))
        {
            sink.OnScored(Result("AAAAAAAA", 42));
            sink.OnScored(Result("BBBBBBBB", 99));
        }
        using (var sink = new SeedLakeSink(_root, "observatory"))
        {
            sink.OnScored(Result("CCCCCCCC", 1));
        }

        var lakePath = SeedLakeSink.LakePath(_root, "perkeo");
        using var provider = SeedSourceProvider.FromLake(lakePath, "perkeo");

        Assert.Equal(2, provider.SeedCount);
        Assert.Equal("AAAAAAAA", provider.NextSeed().ToString());
        Assert.Equal("BBBBBBBB", provider.NextSeed().ToString());
    }

    [Fact]
    public void SeedSourceProvider_DistinctFlagDedupesSeeds()
    {
        var textPath = Path.Combine(_root, "seeds.txt");
        Directory.CreateDirectory(_root);
        File.WriteAllLines(textPath, ["AAAAAAAA", "BBBBBBBB", "AAAAAAAA"]);

        using var provider = new SeedSourceProvider(textPath, distinct: true);

        Assert.Equal(2, provider.SeedCount);
    }

    [Fact]
    public void SeedSourceProvider_ReadsFromJamlsOwnSeedsField()
    {
        var jamlPath = Path.Combine(_root, "filter.jaml");
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            jamlPath,
            """
            name: test
            seeds: [AAAAAAAA, BBBBBBBB, CCCCCCCC]
            must:
              - voucher: Overstock
            """
        );

        using var provider = new SeedSourceProvider(jamlPath);

        Assert.Equal(3, provider.SeedCount);
    }
}
