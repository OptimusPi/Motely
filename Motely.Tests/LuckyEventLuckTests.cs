using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class LuckyEventLuckTests
{
    private const string DifferentialSeed = "41111111";

    private static (long SeedsSearched, long MatchingSeeds, int? Score, int? Tally) RunSingleSeedJaml(
        string jaml
    )
    {
        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}\n{jaml}"
        );

        int? score = null;
        int? tally = null;
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([DifferentialSeed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithScoredResultCallback(result =>
            {
                score = result.Score;
                tally = result.TallyCount > 0 ? result.GetTally(0) : null;
            });

        using var search = settings.Start();
        search.AwaitCompletion();
        return (search.TotalSeedsSearched, search.MatchingSeeds, score, tally);
    }

    [Fact]
    public void LuckyMoney_Roll0_UsesSourcesLuckMultiplier()
    {
        var defaultLuck = """
            name: LuckyMoneyDefaultLuck
            deck: Red
            stake: White
            must:
              - luckyMoney: [0]
            should:
              - luckyMoney: [0]
                label: lucky_money_r0_luck1
                score: 100
            """;

        var luck5 = """
            name: LuckyMoneyLuck5
            deck: Red
            stake: White
            must:
              - luckyMoney: [0]
                sources:
                  luck: 5
            should:
              - luckyMoney: [0]
                label: lucky_money_r0_luck5
                score: 100
                sources:
                  luck: 5
            """;

        var defaultResult = RunSingleSeedJaml(defaultLuck);
        var luck5Result = RunSingleSeedJaml(luck5);

        Assert.Equal(1, defaultResult.SeedsSearched);
        Assert.Equal(0, defaultResult.MatchingSeeds);
        Assert.Null(defaultResult.Score);
        Assert.Null(defaultResult.Tally);

        Assert.Equal(1, luck5Result.SeedsSearched);
        Assert.Equal(1, luck5Result.MatchingSeeds);
        Assert.Equal(100, luck5Result.Score);
        Assert.Equal(1, luck5Result.Tally);
    }
}
