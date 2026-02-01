using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public sealed class ChainedMustClauseSeedTests
{
   
    public void ChainedMustClauses_SingleSeed_C7AOGOYY_ShouldMatch()
    {
        var baronClause = new MotelyJsonConfig.MotelyJsonFilterClause
        {
            Type = "Joker",
            Value = "Baron",
            Antes = [1, 2, 3, 4],
        };
        baronClause.InitializeParsedEnums();

        var mimeClause = new MotelyJsonConfig.MotelyJsonFilterClause
        {
            Type = "Joker",
            Value = "Mime",
            Antes = [1, 2, 3, 4],
        };
        mimeClause.InitializeParsedEnums();

        var baronFilterDesc = new MotelyJsonJokerFilterDesc(
            MotelyJsonJokerFilterClause.CreateCriteria(
                MotelyJsonJokerFilterClause.ConvertClauses([baronClause])
            )
        );
        var mimeFilterDesc = new MotelyJsonJokerFilterDesc(
            MotelyJsonJokerFilterClause.CreateCriteria(
                MotelyJsonJokerFilterClause.ConvertClauses([mimeClause])
            )
        );

        var seedsToTest = new List<string> { "C7AOGOYY" };

        IMotelySearch search =
            new MotelySearchSettings<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>(
                baronFilterDesc
            )
                .WithAdditionalFilter(mimeFilterDesc)
                .WithDeck(MotelyDeck.Ghost)
                .WithStake(MotelyStake.Black)
                .WithQuietMode(true)
                .WithListSearch(seedsToTest)
                .Start();

        // Wait for search to complete (with timeout to prevent hanging)
        search.AwaitCompletionWithTimeout(timeoutSeconds: 2);

        Assert.Equal(MotelySearchStatus.Completed, search.Status);
        Assert.Equal(1, search.MatchingSeeds);
    }
}
