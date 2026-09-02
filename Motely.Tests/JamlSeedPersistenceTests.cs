using Motely.DataLake;
using Motely.Filters;

namespace Motely.Tests;

/// <summary>One gate: Auto/fixed decide lake, UI, and save-back together.</summary>
public sealed class JamlSeedPersistenceTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("motely-persist-");
    private string LakeRoot => Path.Join(_temp.FullName, "Seeds");

    public void Dispose() => _temp.Delete(recursive: true);

    private static MotelyScoredSeedResult Result(string seed, int score)
    {
        var r = new MotelyScoredSeedResult();
        r.Reset(seed, score);
        return r;
    }

    [Fact]
    public void Auto_LakeAndUiAreTheSameRows()
    {
        var accepted = new List<(string Seed, int Score)>();
        using (var persistence = new JamlSeedPersistence(LakeRoot, "whimsy", MotelyScoreCutoff.Auto()))
        {
            persistence.OnScoredAccepted = t => accepted.Add((t.Seed, t.Score));

            Assert.True(persistence.OnScored(Result("AAAAAAAA", 1)));
            Assert.True(persistence.OnScored(Result("5X5", 5)));
            Assert.False(persistence.OnScored(Result("616", 3)));
            Assert.True(persistence.OnScored(Result("7H7", 5)));
            Assert.False(persistence.OnScored(Result("UNITTEST", 2)));
        }

        using var lake = SeedLake.Open(LakeRoot);
        var bySeed = lake.Results("whimsy").ToDictionary(r => r.Seed, r => r.Score);
        Assert.Equal(3, bySeed.Count);
        Assert.Equal(1, bySeed["AAAAAAAA"]);
        Assert.Equal(5, bySeed["5X5"]);
        Assert.Equal(5, bySeed["7H7"]);
        Assert.False(bySeed.ContainsKey("616"));
        Assert.False(bySeed.ContainsKey("UNITTEST"));

        Assert.Equal([("AAAAAAAA", 1), ("5X5", 5), ("7H7", 5)], accepted);
    }

    [Fact]
    public void Auto_SaveBackMatchesTheLake()
    {
        string[] saved;
        using (var persistence = new JamlSeedPersistence(LakeRoot, "whimsy", MotelyScoreCutoff.Auto()))
        {
            persistence.OnScored(Result("AAAAAAAA", 1));
            persistence.OnScored(Result("5X5", 5));
            persistence.OnScored(Result("616", 3));
            saved = persistence.SeedsToSave().ToArray();
        }

        Assert.Equal(["5X5", "AAAAAAAA"], saved);

        using var lake = SeedLake.Open(LakeRoot);
        Assert.Equal(2, lake.DistinctSeedCount("whimsy"));
    }

    [Fact]
    public void FixedFloor_DropsBelowFloorEverywhere()
    {
        var accepted = new List<string>();
        using (var persistence = new JamlSeedPersistence(LakeRoot, "whimsy", MotelyScoreCutoff.Fixed(4)))
        {
            persistence.OnScoredAccepted = t => accepted.Add(t.Seed);
            Assert.False(persistence.OnScored(Result("616", 2)));
            Assert.True(persistence.OnScored(Result("5X5", 4)));
            Assert.True(persistence.OnScored(Result("AAAAAAAA", 9)));
        }

        using var lake = SeedLake.Open(LakeRoot);
        Assert.Equal(["5X5", "AAAAAAAA"], lake.Results("whimsy").Select(r => r.Seed).ToArray());
        Assert.Equal(["5X5", "AAAAAAAA"], accepted);
    }
}
