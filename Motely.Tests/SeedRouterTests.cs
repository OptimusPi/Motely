namespace Motely.Tests;

public sealed class SeedRouterTests
{
    [Fact]
    public void TestSeedRouter_CapturesSingleSearchContext()
    {
        using var router = new MotelySeedRouterDesc("1AAAAAAA", MotelyDeck.Red, MotelyStake.White);

        var ctx = router.Instance();

        Assert.Equal("1AAAAAAA", ctx.GetSeed());
        var bossStream = ctx.CreateBossStream();
        var runState = new MotelyRunState();
        var boss = ctx.GetBossForAnte(ref bossStream, 1, ref runState);
        Assert.NotEqual(default, boss);
    }
}
