using Motely.DataLake;

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
            Path.Combine(_root, "perkeo.duckdb"),
            SeedLakeSink.LakePath(_root, "perkeo")
        );
        Assert.NotEqual(
            SeedLakeSink.LakePath(_root, "perkeo"),
            SeedLakeSink.LakePath(_root, "observatory")
        );
    }

    [Fact]
    public void TwoDifferentFilters_GetSeparateDatabases()
    {
        using (var sink = new SeedLakeSink(_root, "perkeo"))
            sink.OnScored(Result("AAAAAAAA", 42));

        using (var sink = new SeedLakeSink(_root, "observatory"))
            sink.OnScored(Result("BBBBBBBB", 7));

        using var perkeoSeeds = SeedSourceProvider.FromLake(SeedLakeSink.LakePath(_root, "perkeo"), "perkeo");
        using var observatorySeeds = SeedSourceProvider.FromLake(SeedLakeSink.LakePath(_root, "observatory"), "observatory");

        Assert.Equal(1, perkeoSeeds.SeedCount);
        Assert.Equal("AAAAAAAA", perkeoSeeds.NextSeed());
        Assert.Equal(1, observatorySeeds.SeedCount);
        Assert.Equal("BBBBBBBB", observatorySeeds.NextSeed());
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
    public void SeedSourceProvider_ReadsSeedsFromARealJamlFile()
    {
        // A real corpus filter with a seeds: block — no fabricated JAML. The provider's seed count
        // must equal what the loader parses from the same file, cross-checking the two readers.
        var jamlPath = Path.Combine(AppContext.BaseDirectory, "GoldenJamlFiles", "Zerkeo_Pure.jaml");
        Assert.True(
            JamlConfigLoader.TryLoad(File.ReadAllText(jamlPath), out var config, out var error),
            error
        );
        Assert.NotEmpty(config!.Seeds);

        using var provider = new SeedSourceProvider(jamlPath);

        Assert.Equal(config.Seeds.Count, provider.SeedCount);
    }
}
