namespace Motely.Tests;

// Jimmolate is a per-seed PREDICATE, chained on like a normal filter (the OG Immolate
// `filter(inst) => keep?` mental model, in C#). These tests prove the predicate's bool
// actually drives filtering and that it receives a live, drivable search context —
// independent of any WASM/JS exposure (JS uses JAMLyzer's marshallable result instead).
public sealed class JimmolateFilterTests
{
    private static readonly string[] Seeds = ["12345678", "UNITTEST", "1AAAAAAA", "ALEEBOOO"];

    private static (long Matching, List<string> Matched) RunWithJimmolate(
        MotelyIndividualSeedSearcher predicate
    )
    {
        var matched = new List<string>();
        var settings = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
                new PassthroughFilterDesc()
            )
            .WithDeck(MotelyDeck.Red)
            .WithStake(MotelyStake.White)
            .WithListSearch(Seeds, Seeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithJimmolate(predicate)
            .WithSeedMatchCallback(matched.Add);

        using var search = settings.Start();
        search.AwaitCompletion();
        return (search.MatchingSeeds, matched);
    }

    [Fact]
    public void Jimmolate_AcceptAll_KeepsEverySeed()
    {
        var (matching, matched) = RunWithJimmolate(static (ref MotelySingleSearchContext _) => true);

        Assert.Equal((long)Seeds.Length, matching);
        Assert.Equal(Seeds.Length, matched.Count);
    }

    [Fact]
    public void Jimmolate_RejectAll_KeepsNothing()
    {
        var (matching, matched) = RunWithJimmolate(
            static (ref MotelySingleSearchContext _) => false
        );

        Assert.Equal(0L, matching);
        Assert.Empty(matched);
    }

    [Fact]
    public void Jimmolate_PredicateBoolDrivesFiltering_KeepsOnlyTargetSeed()
    {
        const string target = "UNITTEST";

        var (matching, matched) = RunWithJimmolate(
            static (ref MotelySingleSearchContext ctx) => ctx.GetSeed() == target
        );

        Assert.Equal(1L, matching);
        Assert.Equal(target, Assert.Single(matched));
    }

    // The OG Immolate mental model: the predicate gets a live, drivable per-seed context,
    // not just a seed string. Every seed has an ante-1 boss, so reading it must succeed for
    // each seed and keep all of them.
    [Fact]
    public void Jimmolate_ReceivesLiveSearchContext_CanDriveStreams()
    {
        var seen = new List<string>();

        var (matching, matched) = RunWithJimmolate(
            (ref MotelySingleSearchContext ctx) =>
            {
                seen.Add(ctx.GetSeed());
                var bossStream = ctx.CreateBossStream();
                var runState = new MotelyRunState();
                var boss = ctx.GetBossForAnte(ref bossStream, 1, ref runState);
                return boss != default;
            }
        );

        Assert.Equal(Seeds.Length, seen.Count); // predicate invoked once per seed
        Assert.Equal((long)Seeds.Length, matching); // every seed has a valid ante-1 boss
        Assert.Equal(Seeds.Length, matched.Count);
    }
}
