using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class JamlFilterTypeTests
{
    private void TestFilterCompilesAndRuns(string jaml)
    {
        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2)
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        search.AwaitCompletion();

        Assert.True(search.TotalSeedsSearched > 0, "Should have searched some seeds");
        Assert.True(search.IsCompleted, "Search should be completed");
    }

    [Theory]
    [InlineData("joker: Showman")]
    [InlineData("joker: Showman\nedition: Negative")]
    [InlineData("joker: Showman\nedition: Polychrome\nstickers: [Eternal]")]
    [InlineData("jokers: [Showman, Blueprint]")]
    [InlineData("type: Joker\nvalue: Showman")]
    public void JokerFilter_SyntaxVariations(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause.Replace("\n", "\n    ")}");
    }

    [Theory]
    [InlineData("commonJoker: HalfJoker")]
    [InlineData("uncommonJoker: Showman")]
    [InlineData("rareJoker: Blueprint")]
    [InlineData("mixedJoker: Blueprint")] // Maps to any joker type
    [InlineData("legendaryJoker: Perkeo")]
    [InlineData("soulJoker: Perkeo")] // Alias for legendaryJoker internally
    public void JokerRarityFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause}");
    }

    [Theory]
    [InlineData("voucher: Telescope")]
    [InlineData("vouchers: [Telescope, Observatory]")]
    [InlineData("type: Voucher\nvalue: Telescope")]
    public void VoucherFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause.Replace("\n", "\n    ")}");
    }

    [Theory]
    [InlineData("tarot: TheEmperor")]
    [InlineData("tarotCard: TheFool")]
    [InlineData("spectral: Familiar")]
    [InlineData("spectralCard: Aura")]
    [InlineData("planet: Earth")]
    [InlineData("planetCard: Pluto")]
    public void ConsumableFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause}");
    }

    [Theory]
    [InlineData("boss: TheArm")]
    [InlineData("type: Boss\nvalue: TheArm")]
    [InlineData("type: BossBlind\nvalue: TheWall")]
    public void BossFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause.Replace("\n", "\n    ")}");
    }

    [Theory]
    [InlineData("tag: CouponTag")] // Defaults to SmallBlindTag
    [InlineData("smallBlindTag: CouponTag")]
    [InlineData("bigBlindTag: RareTag")]
    [InlineData("type: Tag\nvalue: CouponTag")]
    public void TagFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause.Replace("\n", "\n    ")}");
    }

    [Theory]
    [InlineData("standardCard: HA")] // Ace of Hearts
    [InlineData("type: StandardCard\nrank: A\nsuit: Spades\nenhancement: Lucky")]
    [InlineData("type: StandardCard\nrank: 2\nsuit: Clubs\nseal: Red\nedition: Foil")]
    public void PlayingCardFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause.Replace("\n", "\n    ")}");
    }

    [Theory]
    [InlineData("erraticRank: A")]
    [InlineData("erraticSuit: Spades")]
    [InlineData("type: ErraticCard\nrank: A\nsuit: Spades")] // Full ErraticCard requires both
    public void ErraticDeckFilters(string clause)
    {
        TestFilterCompilesAndRuns($@"deck: Erratic
must:
  - {clause.Replace("\n", "\n    ")}");
    }

    [Theory]
    [InlineData("startingDraw: HA")]
    [InlineData("type: StartingDraw\nrank: A\nsuit: Hearts")]
    public void StartingDrawFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause.Replace("\n", "\n    ")}");
    }

    [Theory]
    [InlineData("event: LuckyMoney")]
    [InlineData("event: LuckyMult")]
    [InlineData("event: MisprintMult")]
    [InlineData("event: WheelOfFortune")]
    [InlineData("event: CavendishExtinct")]
    [InlineData("event: GrosMichelExtinct")]
    public void EventFilters(string clause)
    {
        TestFilterCompilesAndRuns($"must:\n  - {clause}");
    }

    [Fact]
    public void Sources_Targeting()
    {
        var jaml = @"
must:
  - joker: Showman
    sources:
      shopItems: [1, 2]
      boosterPacks: [1, 2, 3]
      judgement: [1]
      wraith: [1]
      riffRaff: [1]
      rareTag: [1]
      uncommonTag: [1]
  - tarot: TheEmperor
    sources:
      shopItems: [1]
      boosterPacks: [1]
      emperor: [1, 2]
      purpleSealOrEightBall: [1]
  - spectral: Aura
    sources:
      sixthSense: [1]
      seance: [1]
  - standardCard: HA
    sources:
      certificate: [1]
      incantation: [1]
      familiar: [1]
      grim: [1]
      deckDraw: [1, 2, 3]";
        TestFilterCompilesAndRuns(jaml);
    }

    [Fact]
    public void LogicalCombinators_AndOr()
    {
        var jaml = @"
must:
  - or:
      - joker: Showman
      - joker: Blueprint
  - and:
      - tarot: TheFool
      - spectral: Aura
  - joker: HalfJoker
    min: 2
should:
  - boss: TheArm
mustNot:
  - joker: Vagabond";
        TestFilterCompilesAndRuns(jaml);
    }
}
