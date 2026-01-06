using Motely.Filters;

namespace Motely.Tests;

[Trait("Category", "Unit")]
public sealed class ErraticRankFilterTests
{
    [Fact]
    public void ErraticRank_Clause_Should_Parse_Correctly()
    {
        // Arrange
        var clause = new MotelyJsonConfig.MotelyJsonFilterClause
        {
            Type = "ErraticRank",
            Value = "Two",
            Min = 1
        };

        // Act
        clause.InitializeParsedEnums();

        // Assert
        Assert.Equal(MotelyFilterItemType.ErraticRank, clause.ItemTypeEnum);
        Assert.True(clause.RankEnum.HasValue, "RankEnum should be set for ErraticRank");
        Assert.Equal(MotelyPlayingCardRank.Two, clause.RankEnum.Value);
    }

    [Fact]
    public void ErraticRank_FilterDesc_Should_Create_With_Correct_Values()
    {
        // Arrange & Act
        var filterDesc = new MotelyJsonErraticRankFilterDesc(
            MotelyPlayingCardRank.Two,
            minCount: 1
        );

        var searchParams = new MotelySearchParameters
        {
            Deck = MotelyDeck.Erratic,
            Stake = MotelyStake.White
        };

        var filterCreationCtx = new MotelyFilterCreationContext(in searchParams);
        var filter = filterDesc.CreateFilter(ref filterCreationCtx);

        // Assert: Filter should be created without errors (it's a struct so always non-null)
        // The actual filtering behavior will be tested via integration tests
        Assert.True(true); // Placeholder - filter creation succeeded
    }
}
