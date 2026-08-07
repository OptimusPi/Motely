using Motely.Filters;
using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Parent <c>antes</c> on <c>and:</c>/<c>or:</c> pass through every nested arm that
/// does not override. Neg free Oops package: and is product law (Neg + Oops), not sugar.
/// Neg never ante 1 → parent scope <c>[2..8]</c> on the and.
/// </summary>
public class JamlNegFreeOopsAndTests
{
    private const string Seed = "1F5WEAYR";

    private static readonly int[] NegScope = [2, 3, 4, 5, 6, 7, 8];

    private static (long Matching, int? Score) Run(string jaml)
    {
        Assert.True(JamlConfigLoader.TryLoad(jaml, out var config, out var error), error);
        int? score = null;
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSeedGenerator([Seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithScoredResultCallback(r => score = r.Score);
        using var search = settings.Start();
        search.AwaitCompletion();
        return (search.MatchingSeeds, score);
    }

    [Fact]
    public void And_ParentAntes_PassThrough_UnlessChildOverrides()
    {
        Assert.True(
            JamlConfigLoader.TryLoad(
                """
                name: parent-antes-pass
                deck: Anaglyph
                stake: White
                must:
                  - and:
                      antes: [2, 3, 4, 5, 6, 7, 8]
                      clauses:
                        - smallBlindTag: NegativeTag
                        - or:
                            clauses:
                              - uncommonJoker: OopsAll6s
                                sources: { shopItems: [0, 1, 2, 3, 4, 5, 6, 7] }
                              - uncommonJoker: OopsAll6s
                                sources: { shopItems: [8, 9, 10, 11, 12, 13, 14, 15] }
                                antes: [6]
                should:
                  - uncommonJoker: OopsAll6s
                    score: 10
                    sources: { shopItems: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15] }
                """,
                out var config,
                out var error
            ),
            error
        );

        _ = JamlSearchBuilder.CreateSettings(config!);

        var and = Assert.IsType<AndClause>(Assert.Single(config!.Must));
        Assert.Equal(NegScope, and.Antes);

        var neg = Assert.IsAssignableFrom<IAnteScopedClause>(and.Clauses[0]);
        Assert.Equal(NegScope, neg.Antes);

        var or = Assert.IsType<OrClause>(and.Clauses[1]);
        Assert.Equal(NegScope, or.Antes); // nested logic inherited parent scope

        var arm0 = Assert.IsAssignableFrom<IAnteScopedClause>(or.Clauses[0]);
        Assert.Equal(NegScope, arm0.Antes); // bare arm inherits

        var arm1 = Assert.IsAssignableFrom<IAnteScopedClause>(or.Clauses[1]);
        Assert.Equal([6], arm1.Antes); // override sticks
    }

    [Fact]
    public void And_ParentAntes2Through8_NegFreeOops_Hits_1F5WEAYR()
    {
        var (matching, score) = Run(
            """
            name: neg-free-oops-parent-antes
            deck: Anaglyph
            stake: White
            must:
              - and:
                  antes: [2, 3, 4, 5, 6, 7, 8]
                  clauses:
                    - smallBlindTag: NegativeTag
                    - uncommonJoker: OopsAll6s
                      min: 1
                      sources: { shopItems: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15] }
            should:
              - uncommonJoker: OopsAll6s
                score: 10
                sources: { shopItems: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15] }
            """
        );
        Assert.Equal(1, matching);
        Assert.True(score is > 0, $"expected Neg-free Oops package on {Seed}, got {score}");
    }
}
