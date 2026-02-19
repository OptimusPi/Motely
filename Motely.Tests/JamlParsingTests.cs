using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class JamlParsingTests
{
    [Fact]
    public void VerifyShouldClauses_AreNotAddedToMustLists()
    {
        // ARRANGE
        string jaml = """
                      should:
                        - joker: Baron
                          score: 100
                      """;

        // ACT
        JamlConfigLoader.TryLoad(jaml, out var config, out _);

        // ASSERT
        Assert.NotNull(config);

        // Baron is in Should.Jokers
        Assert.Single(config.Should.Jokers);

        // Baron should NOT be in Must.Jokers
        Assert.Empty(config.Must.Jokers);
    }

    [Fact]
    public void MustAndShould_AreSeparated()
    {
        string jaml = """
                      must:
                        - joker: Blueprint
                      should:
                        - joker: Baron
                          score: 10
                        - joker: Mime
                          score: 5
                      """;

        JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.NotNull(config);
        Assert.Single(config.Must.Jokers);       // Blueprint in must
        Assert.Equal(2, config.Should.Jokers.Count);  // Baron + Mime in should
    }
}
