using Motely.Analysis;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Tests that IMotelySeedRouter gives a live MotelySingleSearchContext whose shop stream
/// matches deterministic expectations (same stack as analyzer / orchestration).
/// </summary>
public sealed class SeedRouterTests
{
    [Theory]
    [InlineData("ALEEB", 1, MotelyDeck.Painted, MotelyStake.White, "TradingCard", "Rocket")]
    [InlineData("ALEEB", 5, MotelyDeck.Painted, MotelyStake.White, "TheHierophant", "Venus")]
    [InlineData("ALEEB", 5, MotelyDeck.Ghost, MotelyStake.Gold, "Venus", "Ouija")]
    [InlineData("J179876", 5, MotelyDeck.Ghost, MotelyStake.Gold, "Venus", "Eternal CreditCard")]
    public void SeedRouter_GetNextShopItem(
        string seed,
        int ante,
        MotelyDeck deck,
        MotelyStake stake,
        string itemFormatted1,
        string itemFormatted2)
    {
        var expected1 = MotelyItem.Parse(itemFormatted1);
        var expected2 = MotelyItem.Parse(itemFormatted2);

        using var seedRouter = new MotelySeedRouterDesc(seed, deck, stake);

        var ctx = seedRouter.CreateContext();
        var shopStream = ctx.CreateShopItemStream(ante);

        var actual1 = ctx.GetNextShopItem(ref shopStream);
        var actual2 = ctx.GetNextShopItem(ref shopStream);

        Assert.Equal(expected1, actual1);
        Assert.Equal(expected2, actual2);
    }
}
