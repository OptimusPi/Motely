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

        using var perkeoSeeds = SeedSourceProvider.FromLake(SeedLakeSink.LakePath(_root, "perkeo"));
        using var observatorySeeds = SeedSourceProvider.FromLake(SeedLakeSink.LakePath(_root, "observatory"));

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
        using var provider = SeedSourceProvider.FromLake(lakePath);

        Assert.Equal(2, provider.SeedCount);
        Assert.Equal("AAAAAAAA", provider.NextSeed().ToString());
        Assert.Equal("BBBBBBBB", provider.NextSeed().ToString());
    }

    [Fact]
    public void FromLakeRoot_DrownsInEveryFiltersSeeds_Deduped()
    {
        using (var sink = new SeedLakeSink(_root, "perkeo"))
        {
            sink.OnScored(Result("AAAAAAAA", 42));
            sink.OnScored(Result("BBBBBBBB", 99));
        }
        using (var sink = new SeedLakeSink(_root, "observatory"))
        {
            sink.OnScored(Result("BBBBBBBB", 1)); // shared with perkeo — must dedupe
            sink.OnScored(Result("CCCCCCCC", 1));
        }
        // Legacy CSV lakes sitting in the root pour in too (header row is shape-tested out).
        File.WriteAllLines(Path.Combine(_root, "old.csv"), ["Seed,Score", "DDDDDDDD,5"]);

        using var provider = SeedSourceProvider.FromLakeRoot(_root);

        Assert.Equal(4, provider.SeedCount);
        var seeds = new HashSet<string>();
        for (string s; (s = provider.NextSeed()) != string.Empty; )
            seeds.Add(s);
        Assert.Equal(["AAAAAAAA", "BBBBBBBB", "CCCCCCCC", "DDDDDDDD"], seeds.Order());
    }

    [Fact]
    public void LakeCanBeWrittenWhileAProviderReadsIt()
    {
        // The --drown contract: read the lake, then keep writing finds into that same lake
        // during the run. The provider must not hold the file lock after construction.
        using (var sink = new SeedLakeSink(_root, "perkeo"))
            sink.OnScored(Result("AAAAAAAA", 42));

        using var provider = SeedSourceProvider.FromLakeRoot(_root);
        Assert.Equal(1, provider.SeedCount);

        using (var sink = new SeedLakeSink(_root, "perkeo"))
            sink.OnScored(Result("BBBBBBBB", 7)); // throws if the provider still holds the file

        Assert.Equal("AAAAAAAA", provider.NextSeed());
        using var after = SeedSourceProvider.FromLake(SeedLakeSink.LakePath(_root, "perkeo"));
        Assert.Equal(2, after.SeedCount);
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

    [Fact]
    public void FromLakeRoot_AlsoPoursExtraSeeds_DedupedAgainstTheLake()
    {
        using (var sink = new SeedLakeSink(_root, "perkeo"))
        {
            sink.OnScored(Result("AAAAAAAA", 42));
            sink.OnScored(Result("BBBBBBBB", 99));
        }

        // The JAML's seeds: block rides along — overlap dedupes, junk is shape-tested out.
        using var provider = SeedSourceProvider.FromLakeRoot(
            _root,
            ["BBBBBBBB", "CCCCCCCC", " CCCCCCCC ", "Seed", "", "not-a-seed"]
        );

        Assert.Equal(3, provider.SeedCount);
        var seeds = new HashSet<string>();
        for (string s; (s = provider.NextSeed()) != string.Empty; )
            seeds.Add(s);
        Assert.Equal(["AAAAAAAA", "BBBBBBBB", "CCCCCCCC"], seeds.Order());
    }

    [Fact]
    public void FromLakeRoot_WithNoLakeDirectoryYet_DrownsInTheExtraSeedsAlone()
    {
        // A fresh filter's first --drown: no lake on disk, but the JAML already saved finds.
        Assert.False(Directory.Exists(_root));
        Assert.False(SeedSourceProvider.HasLakeFiles(_root));

        using var provider = SeedSourceProvider.FromLakeRoot(_root, ["AAAAAAAA", "BBBBBBBB"]);

        Assert.Equal(2, provider.SeedCount);
        Assert.Equal("AAAAAAAA", provider.NextSeed());
        Assert.Equal("BBBBBBBB", provider.NextSeed());
        Assert.Equal(string.Empty, provider.NextSeed());
    }

    [Fact]
    public void HasLakeFiles_SeesOnlyNonEmptyLakeShapedFiles()
    {
        Directory.CreateDirectory(_root);
        Assert.False(SeedSourceProvider.HasLakeFiles(_root)); // empty directory

        File.WriteAllText(Path.Combine(_root, "notes.md"), "not a lake");
        File.WriteAllText(Path.Combine(_root, "empty.csv"), "");
        Assert.False(SeedSourceProvider.HasLakeFiles(_root)); // wrong shape / zero bytes

        using (var sink = new SeedLakeSink(_root, "perkeo"))
            sink.OnScored(Result("AAAAAAAA", 1));
        Assert.True(SeedSourceProvider.HasLakeFiles(_root));
    }
}
